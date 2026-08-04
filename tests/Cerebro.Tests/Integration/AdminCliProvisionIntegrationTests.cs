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
          "ec": "E01",
          "date": "2026-10-09",
          "rattrapage": false,
          "etudiants": {
            "yoan.thirion@outlook.fr": {
              "nom": "Jean Luc",
              "id": "FFFB5AB1",
              "promo": "B1",
              "drive_folder_id": "1GAzx9-9s84Z2dNaAV4a2MHQqdpCX0_Ib"
            },
            "yoan.thirion@gmail.com": {
              "nom": "Herr Cul",
              "id": "0770F2DB",
              "promo": "B1",
              "drive_folder_id": "1k3eIRxbfgzymoN-cvnxdQXgnrJ-2op5L"
            }
          },
          "correcteurs": [
            { "nom": "Yoan Thirion", "email": "yoan.thirion@ik.me" }
          ],
          "diplome": "RNCP39608-CDWFS"
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