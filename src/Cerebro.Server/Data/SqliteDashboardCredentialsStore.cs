using System.Security.Cryptography;
using System.Text;
using Cerebro.Server.Security;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cerebro.Server.Data;

// Un seul compte surveillant (Id = 1) : pas de gestion multi-utilisateurs, juste un accès protégé
// au dashboard sur le réseau local de l'épreuve. Voir Admin/AdminCommands.SetPassword pour le CLI
// qui alimente cette table.
public sealed class SqliteDashboardCredentialsStore : IDashboardCredentialsStore
{
    private readonly string _connectionString;

    public SqliteDashboardCredentialsStore(string connectionString)
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
            CREATE TABLE IF NOT EXISTS DashboardCredentials (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                Username TEXT NOT NULL,
                PasswordHash TEXT NOT NULL
            );
            """);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task SetCredentialsAsync(string username, string password, CancellationToken cancellationToken)
    {
        var passwordHash = PasswordHasher.Hash(password);

        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            """
            INSERT INTO DashboardCredentials (Id, Username, PasswordHash) VALUES (1, @username, @passwordHash)
            ON CONFLICT(Id) DO UPDATE SET Username = @username, PasswordHash = @passwordHash;
            """,
            new { username, passwordHash },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<bool> ValidateAsync(string username, string password, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            "SELECT Username AS Username, PasswordHash AS PasswordHash FROM DashboardCredentials WHERE Id = 1;",
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<CredentialsRow>(command);
        if (row is null)
        {
            return false;
        }

        // Comparaison à temps constant sur le nom d'utilisateur aussi, pas seulement le mot de
        // passe : évite de distinguer "mauvais utilisateur" de "bon utilisateur, mauvais mot de
        // passe" par une différence de timing.
        var usernameMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(row.Username), Encoding.UTF8.GetBytes(username));
        var passwordMatches = PasswordHasher.Verify(password, row.PasswordHash);

        return usernameMatches && passwordMatches;
    }

    public async Task<bool> HasCredentialsAsync(CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = new CommandDefinition(
            "SELECT 1 FROM DashboardCredentials WHERE Id = 1;",
            cancellationToken: cancellationToken);

        var result = await connection.ExecuteScalarAsync<int?>(command);
        return result is not null;
    }

    private sealed record CredentialsRow
    {
        public required string Username { get; init; }
        public required string PasswordHash { get; init; }
    }
}
