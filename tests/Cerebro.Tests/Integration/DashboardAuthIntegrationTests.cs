using System.Net;
using System.Net.Http.Json;
using Cerebro.Server.Auth;
using Cerebro.Server.Data;
using Cerebro.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NFluent;

namespace Cerebro.Tests.Integration;

/// <summary>
/// Vérifie la protection HTTP du dashboard (page + endpoints de login/logout) indépendamment du
/// hub SignalR — voir CerebroHubIntegrationTests pour la protection des méthodes du hub elles-mêmes.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DashboardAuthIntegrationTests : IAsyncLifetime
{
    private const string Username = "surveillant-test";
    private const string Password = "correct-horse-battery-staple";

    // Code de session propre à chaque instance : les screenshots sont écrits dans le vrai ContentRoot
    // du serveur (pas de dossier temporaire injectable), supprimés en fin de test (DisposeAsync).
    private readonly string _screenshotSession = $"TEST-{Guid.NewGuid():N}";

    private WebApplicationFactory<Program> _factory = null!;
    private string _dbPath = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cerebro-auth-tests-{Guid.NewGuid():N}.db");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CerebroDb"] = $"Data Source={_dbPath}"
                });
            });
        });

        var credentials = _factory.Services.GetRequiredService<IDashboardCredentialsStore>();
        await credentials.SetCredentialsAsync(Username, Password, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        _factory.Services.GetRequiredService<IScreenshotStore>().DeleteSessionData(_screenshotSession);
        await _factory.DisposeAsync();

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task GetIndex_WithoutSession_ShouldRedirectToLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/index.html");

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        Check.That(response.Headers.Location!.OriginalString).Contains("/login.html");
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ShouldSucceedAndSetSessionCookie()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/account/login", new LoginRequest(Username, Password));

        Check.That(response.IsSuccessStatusCode).IsTrue();
        Check.That(response.Headers.TryGetValues("Set-Cookie", out var cookies)).IsTrue();
        Check.That(cookies!.Any(c => c.StartsWith("CerebroDashboardAuth", StringComparison.Ordinal))).IsTrue();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/account/login", new LoginRequest(Username, "mauvais-mot-de-passe"));

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownUsername_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/account/login", new LoginRequest("mallory", Password));

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetIndex_AfterLogin_ShouldServeTheDashboard()
    {
        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/account/login", new LoginRequest(Username, Password));
        loginResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/index.html");

        Check.That(response.IsSuccessStatusCode).IsTrue();
        var html = await response.Content.ReadAsStringAsync();
        Check.That(html).Contains("Cerebro");
    }


    [Fact]
    public async Task LoginPage_ShouldBePubliclyAccessibleWithoutAuthentication()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/login.html");

        Check.That(response.IsSuccessStatusCode).IsTrue();
    }

    [Fact]
    public async Task GetLatestScreenshot_WithoutSession_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/sessions/{_screenshotSession}/candidates/alice/latest-screenshot");

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLatestScreenshot_AfterLogin_ShouldServeMostRecentCaptureAsWebp()
    {
        var store = _factory.Services.GetRequiredService<IScreenshotStore>();
        var capturedAt = DateTimeOffset.UtcNow;
        await store.SaveAsync(_screenshotSession, "alice", [1, 1, 1], capturedAt.AddMinutes(-1));
        await store.SaveAsync(_screenshotSession, "alice", [2, 2, 2], capturedAt);

        using var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/account/login", new LoginRequest(Username, Password));
        loginResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/sessions/{_screenshotSession}/candidates/alice/latest-screenshot");

        Check.That(response.IsSuccessStatusCode).IsTrue();
        Check.That(response.Content.Headers.ContentType!.MediaType).IsEqualTo("image/webp");
        Check.That(await response.Content.ReadAsByteArrayAsync()).ContainsExactly((byte)2, (byte)2, (byte)2);
    }

    [Fact]
    public async Task GetLatestScreenshot_AfterLogin_WhenCandidateHasNoScreenshot_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/account/login", new LoginRequest(Username, Password));
        loginResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/sessions/{_screenshotSession}/candidates/alice/latest-screenshot");

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
