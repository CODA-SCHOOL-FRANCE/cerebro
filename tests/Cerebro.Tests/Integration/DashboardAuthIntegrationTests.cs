using System.Net;
using System.Net.Http.Json;
using Cerebro.Server.Auth;
using Cerebro.Server.Data;
using Microsoft.AspNetCore.Builder;
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
    public async Task GetIndex_BehindReverseProxyOverHttps_ShouldRedirectToLoginWithHttpsScheme()
    {
        // TestServer (transport en mémoire, pas de vraie socket) laisse Connection.RemoteIpAddress
        // à null par défaut - hors IStartupFilter, ce test resterait vert même sans le correctif,
        // car ForwardedHeadersMiddleware ne rejette les en-têtes X-Forwarded-* qu'en comparant
        // RemoteIpAddress à KnownNetworks/KnownProxies (vérifié empiriquement). On force donc une IP
        // non loopback ici, pour reproduire fidèlement Caddy en conteneur séparé sur le réseau
        // Docker interne (docker-compose.yml) - à l'inverse du mode "Caddy natif sur le même hôte"
        // (Caddyfile, modes A/B), où le proxy est en loopback et n'a jamais déclenché ce bug.
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddTransient<IStartupFilter, FakeRemoteIpStartupFilter>()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.7");

        var response = await client.GetAsync("/index.html");

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        Check.That(response.Headers.Location!.Scheme).IsEqualTo("https");
    }

    private sealed class FakeRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
                return nextMiddleware();
            });
            next(app);
        };
    }

    [Fact]
    public async Task LoginPage_ShouldBePubliclyAccessibleWithoutAuthentication()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/login.html");

        Check.That(response.IsSuccessStatusCode).IsTrue();
    }
}
