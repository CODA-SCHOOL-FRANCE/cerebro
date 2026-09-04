using Cerebro.Server.Admin;
using Cerebro.Server.Data;
using NFluent;

namespace Cerebro.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class AdminCliProvisionIntegrationTests : IDisposable
{
    private readonly string _workDir;
    private readonly string _dbPath;

    private const string RosterJson =
        """
        {
          "etudiants": [
            { "nom": "Jean Dupont", "id": "FFFB5AB1" },
            { "nom": "Marie Durand", "id": "0770F2DB" }
          ]
        }
        """;

    public AdminCliProvisionIntegrationTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"cerebro-admincli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
        _dbPath = Path.Combine(_workDir, "cerebro.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task Provision_ShouldRegisterEachStudentFromTheRoster()
    {
        var inputPath = Path.Combine(_workDir, "roster-input.json");
        await File.WriteAllTextAsync(inputPath, RosterJson);

        var exitCode = await AdminCli.RunAsync(
            ["provision", "--session", "E01-2026", "--input", inputPath, "--db", _dbPath]);

        Check.That(exitCode).IsEqualTo(0);

        var repository = new SqliteExamRepository($"Data Source={_dbPath}");
        Check.That(await repository.IsCandidateRegisteredAsync("E01-2026", "FFFB5AB1", CancellationToken.None))
            .IsTrue();
        Check.That(await repository.IsCandidateRegisteredAsync("E01-2026", "0770F2DB", CancellationToken.None))
            .IsTrue();
    }

    // Format vu dans de vrais exports d'école : "etudiants" en objet indexé par email (pas un
    // tableau), avec des champs en plus (ec, date, rattrapage, correcteurs) que le roster minimal
    // n'utilise pas - voir RosterStudentsConverter.
    private const string RosterJsonWithStudentsAsObject =
        """
        {
          "ec": "E01",
          "date": "2026-10-09",
          "rattrapage": false,
          "etudiants": {
            "jean.dupont@ecole.fr": { "nom": "Jean Dupont", "id": "FFFB5AB1" },
            "marie.durand@ecole.fr": { "nom": "Marie Durand", "id": "0770F2DB" }
          },
          "correcteurs": [ { "nom": "Prof Test", "email": "prof@ecole.fr" } ]
        }
        """;

    [Fact]
    public async Task Provision_ShouldRegisterEachStudent_WhenRosterHasStudentsAsAnObjectKeyedByEmail()
    {
        var inputPath = Path.Combine(_workDir, "roster-input.json");
        await File.WriteAllTextAsync(inputPath, RosterJsonWithStudentsAsObject);

        var exitCode = await AdminCli.RunAsync(
            ["provision", "--session", "E01-2026", "--input", inputPath, "--db", _dbPath]);

        Check.That(exitCode).IsEqualTo(0);

        var repository = new SqliteExamRepository($"Data Source={_dbPath}");
        Check.That(await repository.IsCandidateRegisteredAsync("E01-2026", "FFFB5AB1", CancellationToken.None))
            .IsTrue();
        Check.That(await repository.IsCandidateRegisteredAsync("E01-2026", "0770F2DB", CancellationToken.None))
            .IsTrue();
    }

    [Fact]
    public async Task Provision_ShouldFail_WhenSessionAlreadyExists()
    {
        var inputPath = Path.Combine(_workDir, "roster-input.json");
        await File.WriteAllTextAsync(inputPath, RosterJson);

        string[] args = ["provision", "--session", "E01-2026", "--input", inputPath, "--db", _dbPath];
        await AdminCli.RunAsync(args);

        var secondExitCode = await AdminCli.RunAsync(args);

        Check.That(secondExitCode).IsEqualTo(1);
    }

    [Fact]
    public async Task Provision_ShouldFail_WhenInputFileIsMissing()
    {
        var exitCode = await AdminCli.RunAsync(
        [
            "provision",
            "--session", "E01-2026",
            "--input", Path.Combine(_workDir, "does-not-exist.json"),
            "--db", _dbPath
        ]);
        Check.That(exitCode).IsEqualTo(1);
    }
}