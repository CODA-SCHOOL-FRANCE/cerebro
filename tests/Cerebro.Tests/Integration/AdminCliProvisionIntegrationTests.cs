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