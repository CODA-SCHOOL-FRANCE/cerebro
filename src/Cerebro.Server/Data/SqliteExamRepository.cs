using Cerebro.Shared.Realtime;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cerebro.Server.Data;

public sealed class SqliteExamRepository : IExamRepository
{
    private readonly string _connectionString;

    public SqliteExamRepository(string connectionString)
    {
        DapperTypeHandlers.RegisterOnce();
        SqliteDatabaseFile.EnsureDirectoryExists(connectionString);
        _connectionString = connectionString;
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();

        connection.Execute(
            """
            CREATE TABLE IF NOT EXISTS ExamSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionCode TEXT NOT NULL UNIQUE,
                CreatedAt TEXT NOT NULL,
                StartedAt TEXT NULL,
                EndedAt TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS Candidates (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NULL,
                ExamSessionId INTEGER NOT NULL REFERENCES ExamSessions(Id),
                CandidateId TEXT NOT NULL,
                HasConnectedOnce INTEGER NOT NULL DEFAULT 0,
                UNIQUE(ExamSessionId, CandidateId)
            );
            """);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task<bool> SessionExistsAsync(string sessionCode, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            "SELECT 1 FROM ExamSessions WHERE SessionCode = @sessionCode;",
            new {sessionCode},
            cancellationToken: cancellationToken);

        var result = await connection.ExecuteScalarAsync<int?>(command);
        return result is not null;
    }

    public async Task<long> CreateSessionAsync(string sessionCode, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            """
            INSERT INTO ExamSessions (SessionCode, CreatedAt) VALUES (@sessionCode, @createdAt);
            SELECT last_insert_rowid();
            """,
            new {sessionCode, createdAt = DateTimeOffset.UtcNow},
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<long>(command);
    }

    public async Task AddCandidateAsync(
        long sessionId, string candidateId, string name, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            "INSERT INTO Candidates (ExamSessionId, CandidateId, Name) VALUES (@sessionId, @candidateId, @name);",
            new {sessionId, candidateId, name},
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task MarkCandidateConnectedAsync(
        string sessionCode, string candidateId, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            """
            UPDATE Candidates SET HasConnectedOnce = 1
            WHERE CandidateId = @candidateId
              AND ExamSessionId = (SELECT Id FROM ExamSessions WHERE SessionCode = @sessionCode);
            """,
            new {sessionCode, candidateId},
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<IReadOnlyList<CandidateRosterEntryDto>> GetCandidatesAsync(
        string sessionCode, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            """
            SELECT c.CandidateId AS CandidateId,
                   COALESCE(c.Name, c.CandidateId) AS Name,
                   c.HasConnectedOnce AS HasConnectedOnce
            FROM Candidates c
            JOIN ExamSessions s ON s.Id = c.ExamSessionId
            WHERE s.SessionCode = @sessionCode
            ORDER BY Name;
            """,
            new {sessionCode},
            cancellationToken: cancellationToken);

        var candidates = await connection.QueryAsync<CandidateRosterEntryDto>(command);
        return candidates.AsList();
    }

    public async Task MarkStartedAsync(string sessionCode, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        // EndedAt est aussi réinitialisé ici : ça permet de redémarrer une épreuve déjà arrêtée
        // (ré-autorise les connexions candidat, voir IsSessionEndedAsync).
        var command = new CommandDefinition(
            "UPDATE ExamSessions SET StartedAt = @startedAt, EndedAt = NULL WHERE SessionCode = @sessionCode;",
            new {startedAt = DateTimeOffset.UtcNow, sessionCode},
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task MarkEndedAsync(string sessionCode, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            "UPDATE ExamSessions SET EndedAt = @endedAt WHERE SessionCode = @sessionCode;",
            new {endedAt = DateTimeOffset.UtcNow, sessionCode},
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<DateTimeOffset?> GetStartedAtAsync(string sessionCode, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            "SELECT StartedAt FROM ExamSessions WHERE SessionCode = @sessionCode;",
            new {sessionCode},
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<DateTimeOffset?>(command);
    }

    public async Task<bool> IsSessionEndedAsync(string sessionCode, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            "SELECT EndedAt FROM ExamSessions WHERE SessionCode = @sessionCode;",
            new {sessionCode},
            cancellationToken: cancellationToken);

        var endedAt = await connection.ExecuteScalarAsync<DateTimeOffset?>(command);
        return endedAt is not null;
    }

    public async Task<bool> IsCandidateRegisteredAsync(
        string sessionCode, string candidateId, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            """
            SELECT 1
            FROM Candidates c
            JOIN ExamSessions s ON s.Id = c.ExamSessionId
            WHERE s.SessionCode = @sessionCode AND c.CandidateId = @candidateId;
            """,
            new {sessionCode, candidateId},
            cancellationToken: cancellationToken);

        var result = await connection.ExecuteScalarAsync<int?>(command);
        return result is not null;
    }

    public async Task<IReadOnlyList<ExamSessionSummaryDto>> GetSessionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            """
            SELECT s.SessionCode AS SessionCode,
                   COUNT(c.Id) AS CandidateCount,
                   s.CreatedAt AS CreatedAt,
                   s.StartedAt AS StartedAt,
                   s.EndedAt AS EndedAt
            FROM ExamSessions s
            LEFT JOIN Candidates c ON c.ExamSessionId = s.Id
            GROUP BY s.Id
            ORDER BY s.CreatedAt DESC;
            """,
            cancellationToken: cancellationToken);

        var sessions = await connection.QueryAsync<ExamSessionSummaryDto>(command);
        return sessions.AsList();
    }
}