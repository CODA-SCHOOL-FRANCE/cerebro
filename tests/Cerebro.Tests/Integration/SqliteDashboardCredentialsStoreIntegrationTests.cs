using Cerebro.Server.Data;
using NFluent;

namespace Cerebro.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SqliteDashboardCredentialsStoreIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteDashboardCredentialsStore _store;

    public SqliteDashboardCredentialsStoreIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cerebro-credentials-tests-{Guid.NewGuid():N}.db");
        _store = new SqliteDashboardCredentialsStore($"Data Source={_dbPath}");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task HasCredentials_ShouldBeFalse_BeforeAnyPasswordIsSet()
    {
        Check.That(await _store.HasCredentialsAsync(CancellationToken.None)).IsFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNoCredentialsHaveBeenSet()
    {
        Check.That(await _store.ValidateAsync("surveillant", "peu-importe", CancellationToken.None)).IsFalse();
    }

    [Fact]
    public async Task SetCredentials_ThenValidate_ShouldSucceedWithCorrectPassword()
    {
        await _store.SetCredentialsAsync("surveillant", "mot-de-passe-correct", CancellationToken.None);

        Check.That(await _store.HasCredentialsAsync(CancellationToken.None)).IsTrue();
        Check.That(await _store.ValidateAsync("surveillant", "mot-de-passe-correct", CancellationToken.None)).IsTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WithWrongPassword()
    {
        await _store.SetCredentialsAsync("surveillant", "mot-de-passe-correct", CancellationToken.None);

        Check.That(await _store.ValidateAsync("surveillant", "mauvais-mot-de-passe", CancellationToken.None)).IsFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WithWrongUsername()
    {
        await _store.SetCredentialsAsync("surveillant", "mot-de-passe-correct", CancellationToken.None);

        Check.That(await _store.ValidateAsync("mallory", "mot-de-passe-correct", CancellationToken.None)).IsFalse();
    }

    [Fact]
    public async Task SetCredentials_CalledTwice_ShouldReplacePreviousPassword()
    {
        await _store.SetCredentialsAsync("surveillant", "ancien-mot-de-passe", CancellationToken.None);
        await _store.SetCredentialsAsync("surveillant", "nouveau-mot-de-passe", CancellationToken.None);

        Check.That(await _store.ValidateAsync("surveillant", "ancien-mot-de-passe", CancellationToken.None)).IsFalse();
        Check.That(await _store.ValidateAsync("surveillant", "nouveau-mot-de-passe", CancellationToken.None)).IsTrue();
    }
}
