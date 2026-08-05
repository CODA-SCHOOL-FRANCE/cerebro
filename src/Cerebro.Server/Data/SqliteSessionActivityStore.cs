using Cerebro.Shared.Realtime;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cerebro.Server.Data;

// Journal d'activité persisté (mêmes fichier/connexion que SqliteExamRepository) : la source de vérité
// "qui a fait quoi, quand" côté OpenTelemetry - voir Telemetry/CerebroTelemetry.cs pour les traces/métriques
// émises en parallèle (console par défaut, exportables ailleurs sans toucher au code d'instrumentation).
public sealed class SqliteSessionActivityStore : ISessionActivityStore
{
    private readonly string _connectionString;

    public SqliteSessionActivityStore(string connectionString)
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
            CREATE TABLE IF NOT EXISTS SessionActivityEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                SessionCode TEXT NOT NULL,
                CandidateId TEXT NULL,
                EventType TEXT NOT NULL,
                Detail TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_SessionActivityEvents_SessionCode
                ON SessionActivityEvents (SessionCode, Timestamp);
            """);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task RecordAsync(
        string sessionCode, string? candidateId, string eventType, string? detail, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            """
            INSERT INTO SessionActivityEvents (Timestamp, SessionCode, CandidateId, EventType, Detail)
            VALUES (@timestamp, @sessionCode, @candidateId, @eventType, @detail);
            """,
            new { timestamp = DateTimeOffset.UtcNow, sessionCode, candidateId, eventType, detail },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<IReadOnlyList<SessionActivityEventDto>> GetActivityAsync(
        string sessionCode, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            """
            SELECT Timestamp AS Timestamp, EventType AS EventType, CandidateId AS CandidateId, Detail AS Detail
            FROM SessionActivityEvents
            WHERE SessionCode = @sessionCode
            ORDER BY Timestamp ASC;
            """,
            new { sessionCode },
            cancellationToken: cancellationToken);

        var events = await connection.QueryAsync<SessionActivityEventDto>(command);
        return events.AsList();
    }
}
