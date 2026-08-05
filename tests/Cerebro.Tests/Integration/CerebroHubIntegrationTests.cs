using System.Net;
using System.Net.Http.Json;
using Cerebro.Server.Auth;
using Cerebro.Server.Data;
using Cerebro.Shared.Capture;
using Cerebro.Shared.Realtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NFluent;

namespace Cerebro.Tests.Integration;

/// <summary>
/// Ces tests parlent uniquement au hub via de vraies connexions SignalR (HubConnection),
/// exactement comme le feraient l'agent et le dashboard - aucune dépendance aux classes internes du serveur,
/// à l'exception d'IExamRepository utilisé pour provisionner les candidats de test (équivalent du CLI `provision`)
/// et d'IDashboardCredentialsStore pour définir les identifiants de test (équivalent du CLI `set-password`).
/// </summary>
[Trait("Category", "Integration")]
public sealed class CerebroHubIntegrationTests : IAsyncLifetime
{
    private const string TestUsername = "surveillant-test";
    private const string TestPassword = "correct-horse-battery-staple";

    private WebApplicationFactory<Program> _factory = null!;
    private string _dbPath = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cerebro-tests-{Guid.NewGuid():N}.db");

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
        await credentials.SetCredentialsAsync(TestUsername, TestPassword, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private async Task RegisterCandidateAsync(string sessionCode, params string[] candidateIds)
    {
        var repository = _factory.Services.GetRequiredService<IExamRepository>();
        var sessionId = await repository.CreateSessionAsync(sessionCode, CancellationToken.None);
        foreach (var candidateId in candidateIds)
        {
            await repository.AddCandidateAsync(sessionId, candidateId, $"Candidat {candidateId}", CancellationToken.None);
        }
    }

    private HubConnection CreateConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/cerebro"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                // Le TestServer en mémoire ne supporte pas la mise à niveau WebSocket réelle ;
                // le long polling passe par de vraies requêtes HTTP et fonctionne avec ce host de test.
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    // Les méthodes du hub réservées au dashboard sont protégées par [Authorize] (cookie de session) :
    // une vraie connexion "dashboard" dans ces tests doit donc se logger d'abord via /account/login
    // et propager le cookie obtenu sur la connexion SignalR, exactement comme le ferait le navigateur.
    private async Task<HubConnection> CreateAuthenticatedDashboardConnectionAsync()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/account/login", new LoginRequest(TestUsername, TestPassword));
        response.EnsureSuccessStatusCode();

        var cookies = new CookieContainer();
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (var header in setCookieHeaders)
            {
                cookies.SetCookies(_factory.Server.BaseAddress, header);
            }
        }

        return new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/cerebro"), options =>
            {
                // options.Cookies seul ne suffit pas ici : SignalR ne l'applique qu'au HttpClientHandler
                // qu'il construit lui-même et passe en paramètre à HttpMessageHandlerFactory - un
                // paramètre qu'on ignore volontairement pour rediriger vers le TestServer en mémoire.
                // Il faut donc rattacher le cookie à la main sur chaque requête.
                options.HttpMessageHandlerFactory = _ => new CookieDelegatingHandler(_factory.Server.CreateHandler(), cookies);
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    private sealed class CookieDelegatingHandler(HttpMessageHandler inner, CookieContainer cookies) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cookieHeader = cookies.GetCookieHeader(request.RequestUri!);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    private static async Task<T> WaitAsync<T>(TaskCompletionSource<T> tcs, int timeoutMs = 5000)
    {
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        if (completed != tcs.Task)
        {
            throw new TimeoutException("Délai dépassé en attendant l'évènement SignalR.");
        }

        return await tcs.Task;
    }

    [Fact]
    public async Task JoinAsCandidate_ShouldNotifyDashboardAndAppearInSnapshot()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        const string candidateId = "FFFB5AB1";
        await RegisterCandidateAsync(sessionCode, candidateId);

        await using var dashboard = await CreateAuthenticatedDashboardConnectionAsync();
        await using var candidate = CreateConnection();

        var joinedTcs = new TaskCompletionSource<CandidateStatusDto>();
        dashboard.On<CandidateStatusDto>("CandidateJoined", status => joinedTcs.TrySetResult(status));

        await dashboard.StartAsync();
        await dashboard.InvokeAsync("JoinAsDashboard", sessionCode);

        await candidate.StartAsync();
        await candidate.InvokeAsync("JoinAsCandidate", sessionCode, candidateId);

        var received = await WaitAsync(joinedTcs);

        Check.That(received.CandidateId).IsEqualTo(candidateId);
        Check.That(received.IsReady).IsNull();

        var snapshot = await dashboard.InvokeAsync<List<CandidateStatusDto>>("GetSnapshot", sessionCode);
        Check.That(snapshot.Select(c => c.CandidateId)).ContainsExactly(candidateId);
    }

    [Fact]
    public async Task JoinAsCandidate_WithUnregisteredCandidateId_ShouldRejectConnection()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        await RegisterCandidateAsync(sessionCode, "FFFB5AB1");

        await using var candidate = CreateConnection();
        await candidate.StartAsync();

        var exception = await Assert.ThrowsAsync<HubException>(
            () => candidate.InvokeAsync("JoinAsCandidate", sessionCode, "ID-INCONNU"));

        Check.That(exception.Message).Contains("invalide");
    }

    [Fact]
    public async Task JoinAsCandidate_ForUnprovisionedSession_ShouldRejectConnection()
    {
        await using var candidate = CreateConnection();
        await candidate.StartAsync();

        await Assert.ThrowsAsync<HubException>(
            () => candidate.InvokeAsync("JoinAsCandidate", "SESSION-INCONNUE", "mallory"));
    }

    [Fact]
    public async Task DashboardMethods_WithoutLogin_ShouldBeRejected()
    {
        // Une connexion qui n'est jamais passée par /account/login (donc sans cookie de session) :
        // les méthodes réservées au dashboard doivent rejeter l'appel, même si la connexion
        // SignalR elle-même s'établit sans problème ([Authorize] est posé méthode par méthode,
        // pas sur le hub entier, pour ne pas bloquer les agents candidats - voir CerebroHub.cs).
        await using var unauthenticated = CreateConnection();
        await unauthenticated.StartAsync();

        await Assert.ThrowsAsync<HubException>(
            () => unauthenticated.InvokeAsync<List<ExamSessionSummaryDto>>("GetPlannedSessions"));
    }

    [Fact]
    public async Task GetPlannedSessions_ShouldReturnProvisionedSessionsWithCandidateCount()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        await RegisterCandidateAsync(sessionCode, "FFFB5AB1", "0770F2DB");

        await using var dashboard = await CreateAuthenticatedDashboardConnectionAsync();
        await dashboard.StartAsync();

        var sessions = await dashboard.InvokeAsync<List<ExamSessionSummaryDto>>("GetPlannedSessions");
        var session = sessions.Single(s => s.SessionCode == sessionCode);

        Check.That(session.CandidateCount).IsEqualTo(2);
        Check.That(session.StartedAt).IsNull();
        Check.That(session.EndedAt).IsNull();
    }

    [Fact]
    public async Task StartSession_ShouldSetStartedAtAndNotifyDashboard()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        await RegisterCandidateAsync(sessionCode, "FFFB5AB1");

        await using var dashboard = await CreateAuthenticatedDashboardConnectionAsync();
        var startedTcs = new TaskCompletionSource<DateTimeOffset>();
        dashboard.On<string, DateTimeOffset>("SessionStarted", (code, startedAt) =>
        {
            if (code == sessionCode)
            {
                startedTcs.TrySetResult(startedAt);
            }
        });

        await dashboard.StartAsync();
        await dashboard.InvokeAsync("JoinAsDashboard", sessionCode);
        await dashboard.InvokeAsync("StartSession", sessionCode);

        await WaitAsync(startedTcs);

        var sessions = await dashboard.InvokeAsync<List<ExamSessionSummaryDto>>("GetPlannedSessions");
        Check.That(sessions.Single(s => s.SessionCode == sessionCode).StartedAt).IsNotNull();
    }

    [Fact]
    public async Task StopSession_ShouldNotifyDashboardAndRejectFurtherCandidateJoins()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        await RegisterCandidateAsync(sessionCode, "FFFB5AB1");

        await using var dashboard = await CreateAuthenticatedDashboardConnectionAsync();
        var endedTcs = new TaskCompletionSource<DateTimeOffset>();
        dashboard.On<string, DateTimeOffset>("SessionEnded", (code, endedAt) =>
        {
            if (code == sessionCode)
            {
                endedTcs.TrySetResult(endedAt);
            }
        });

        await dashboard.StartAsync();
        await dashboard.InvokeAsync("JoinAsDashboard", sessionCode);
        await dashboard.InvokeAsync("StopSession", sessionCode);

        await WaitAsync(endedTcs);

        await using var candidate = CreateConnection();
        await candidate.StartAsync();

        var exception = await Assert.ThrowsAsync<HubException>(
            () => candidate.InvokeAsync("JoinAsCandidate", sessionCode, "FFFB5AB1"));
        Check.That(exception.Message).Contains("terminée");
    }

    [Fact]
    public async Task StartSession_AfterStopped_ShouldAllowCandidatesToRejoin()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        await RegisterCandidateAsync(sessionCode, "FFFB5AB1");

        await using var dashboard = await CreateAuthenticatedDashboardConnectionAsync();
        await dashboard.StartAsync();
        await dashboard.InvokeAsync("JoinAsDashboard", sessionCode);
        await dashboard.InvokeAsync("StartSession", sessionCode);
        await dashboard.InvokeAsync("StopSession", sessionCode);

        var restartedTcs = new TaskCompletionSource<DateTimeOffset>();
        dashboard.On<string, DateTimeOffset>("SessionStarted", (code, startedAt) =>
        {
            if (code == sessionCode)
            {
                restartedTcs.TrySetResult(startedAt);
            }
        });

        await dashboard.InvokeAsync("StartSession", sessionCode);
        await WaitAsync(restartedTcs);

        var sessions = await dashboard.InvokeAsync<List<ExamSessionSummaryDto>>("GetPlannedSessions");
        Check.That(sessions.Single(s => s.SessionCode == sessionCode).EndedAt).IsNull();

        await using var candidate = CreateConnection();
        await candidate.StartAsync();
        await candidate.InvokeAsync("JoinAsCandidate", sessionCode, "FFFB5AB1");
    }

    [Fact]
    public async Task ReportReadiness_ShouldNotifyDashboardWithFailureReason()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        const string candidateId = "0770F2DB";
        await RegisterCandidateAsync(sessionCode, candidateId);

        await using var dashboard = await CreateAuthenticatedDashboardConnectionAsync();
        await using var candidate = CreateConnection();

        var readinessTcs = new TaskCompletionSource<CandidateStatusDto>();
        dashboard.On<CandidateStatusDto>("CandidateReadinessUpdated", status => readinessTcs.TrySetResult(status));

        await dashboard.StartAsync();
        await dashboard.InvokeAsync("JoinAsDashboard", sessionCode);

        await candidate.StartAsync();
        await candidate.InvokeAsync("JoinAsCandidate", sessionCode, candidateId);
        await candidate.InvokeAsync(
            "ReportReadiness", false, CaptureFailureReason.ToolMissing, "grim introuvable");

        var received = await WaitAsync(readinessTcs);

        Check.That(received.CandidateId).IsEqualTo(candidateId);
        Check.That(received.IsReady).IsEqualTo(false);
        Check.That(received.FailureReason).IsEqualTo(CaptureFailureReason.ToolMissing);
        Check.That(received.FailureDetail).IsEqualTo("grim introuvable");
    }

    [Fact]
    public async Task UploadScreenshot_ShouldNotifyDashboardAndPersistFileToDisk()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        const string candidateId = "9A1C4E7F";
        await RegisterCandidateAsync(sessionCode, candidateId);
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4];

        await using var dashboard = await CreateAuthenticatedDashboardConnectionAsync();
        await using var candidate = CreateConnection();

        var screenshotTcs = new TaskCompletionSource<string>();
        dashboard.On<string, DateTimeOffset>(
            "ScreenshotReceived", (id, _) => screenshotTcs.TrySetResult(id));

        await dashboard.StartAsync();
        await dashboard.InvokeAsync("JoinAsDashboard", sessionCode);

        await candidate.StartAsync();
        await candidate.InvokeAsync("JoinAsCandidate", sessionCode, candidateId);
        await candidate.InvokeAsync("UploadScreenshot", pngBytes);

        var receivedCandidateId = await WaitAsync(screenshotTcs);
        Check.That(receivedCandidateId).IsEqualTo(candidateId);

        var environment = _factory.Services.GetRequiredService<IWebHostEnvironment>();
        var candidateDirectory = Path.Combine(environment.ContentRootPath, "screenshots", sessionCode, candidateId);

        try
        {
            Check.That(Directory.Exists(candidateDirectory)).IsTrue();
            var savedFiles = Directory.GetFiles(candidateDirectory);
            Check.That(savedFiles).HasSize(1);
            Check.That(await File.ReadAllBytesAsync(savedFiles[0])).ContainsExactly(pngBytes);
        }
        finally
        {
            var sessionDirectory = Path.Combine(environment.ContentRootPath, "screenshots", sessionCode);
            if (Directory.Exists(sessionDirectory))
            {
                Directory.Delete(sessionDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DisconnectingCandidate_ShouldNotifyDashboardAndKeepItInSnapshotAsDisconnected()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        const string candidateId = "B2D8E610";
        await RegisterCandidateAsync(sessionCode, candidateId);

        await using var dashboard = await CreateAuthenticatedDashboardConnectionAsync();
        var candidate = CreateConnection();

        var disconnectedTcs = new TaskCompletionSource<CandidateStatusDto>();
        dashboard.On<CandidateStatusDto>("CandidateDisconnected", status => disconnectedTcs.TrySetResult(status));

        await dashboard.StartAsync();
        await dashboard.InvokeAsync("JoinAsDashboard", sessionCode);

        await candidate.StartAsync();
        await candidate.InvokeAsync("JoinAsCandidate", sessionCode, candidateId);
        await candidate.StopAsync();
        await candidate.DisposeAsync();

        var received = await WaitAsync(disconnectedTcs);
        Check.That(received.CandidateId).IsEqualTo(candidateId);
        Check.That(received.IsConnected).IsFalse();

        var snapshot = await dashboard.InvokeAsync<List<CandidateStatusDto>>("GetSnapshot", sessionCode);
        var candidateStatus = snapshot.Single(c => c.CandidateId == candidateId);
        Check.That(candidateStatus.IsConnected).IsFalse();
    }

    [Fact]
    public async Task Ping_ShouldUpdateLastSeenAtAndNotifyDashboard()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        const string candidateId = "FFFB5AB1";
        await RegisterCandidateAsync(sessionCode, candidateId);

        await using var dashboard = await CreateAuthenticatedDashboardConnectionAsync();
        await using var candidate = CreateConnection();

        var heartbeatTcs = new TaskCompletionSource<CandidateStatusDto>();
        dashboard.On<CandidateStatusDto>("CandidateHeartbeat", status => heartbeatTcs.TrySetResult(status));

        await dashboard.StartAsync();
        await dashboard.InvokeAsync("JoinAsDashboard", sessionCode);

        await candidate.StartAsync();
        await candidate.InvokeAsync("JoinAsCandidate", sessionCode, candidateId);
        await candidate.InvokeAsync("Ping");

        var received = await WaitAsync(heartbeatTcs);
        Check.That(received.CandidateId).IsEqualTo(candidateId);
        Check.That(received.IsConnected).IsTrue();
    }

    [Fact]
    public async Task SessionActivity_ShouldRecordJoinReadinessScreenshotAndSessionLifecycleEvents()
    {
        var sessionCode = $"TEST-{Guid.NewGuid():N}";
        const string candidateId = "FFFB5AB1";
        await RegisterCandidateAsync(sessionCode, candidateId);

        await using var dashboard = await CreateAuthenticatedDashboardConnectionAsync();
        await using var candidate = CreateConnection();

        await dashboard.StartAsync();
        await dashboard.InvokeAsync("JoinAsDashboard", sessionCode);
        await dashboard.InvokeAsync("StartSession", sessionCode);

        await candidate.StartAsync();
        await candidate.InvokeAsync("JoinAsCandidate", sessionCode, candidateId);
        await candidate.InvokeAsync("ReportReadiness", true, null, null);
        await candidate.InvokeAsync("UploadScreenshot", new byte[] { 1, 2, 3 });

        await dashboard.InvokeAsync("StopSession", sessionCode);

        var activity = await dashboard.InvokeAsync<List<SessionActivityEventDto>>("GetSessionActivity", sessionCode);

        Check.That(activity.Select(e => e.EventType)).ContainsExactly(
            "SessionStarted", "CandidateJoined", "ReadinessReported", "ScreenshotReceived", "SessionEnded");
        Check.That(activity.Single(e => e.EventType == "ScreenshotReceived").Detail).Contains("\"bytes\":3");
    }
}
