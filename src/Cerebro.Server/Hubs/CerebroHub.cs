using System.Text.Json;
using Cerebro.Server.Admin;
using Cerebro.Server.Data;
using Cerebro.Server.Services;
using Cerebro.Server.Telemetry;
using Cerebro.Shared.Capture;
using Cerebro.Shared.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cerebro.Server.Hubs;

public sealed class CerebroHub : Hub<ICerebroDashboardClient>
{
    private readonly ISessionRegistry _registry;
    private readonly IScreenshotStore _screenshotStore;
    private readonly IExamRepository _examRepository;
    private readonly ISessionActivityStore _activityStore;

    public CerebroHub(
        ISessionRegistry registry,
        IScreenshotStore screenshotStore,
        IExamRepository examRepository,
        ISessionActivityStore activityStore)
    {
        _registry = registry;
        _screenshotStore = screenshotStore;
        _examRepository = examRepository;
        _activityStore = activityStore;
    }

    public async Task JoinAsCandidate(string sessionCode, string candidateId)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Candidate.Join");
        activity?.SetTag("cerebro.session_code", sessionCode);
        activity?.SetTag("cerebro.candidate_id", candidateId);

        if (await _examRepository.IsSessionEndedAsync(sessionCode, Context.ConnectionAborted))
        {
            throw new HubException("Cette session est terminée, les connexions ne sont plus acceptées.");
        }

        var isRegistered = await _examRepository.IsCandidateRegisteredAsync(
            sessionCode, candidateId, Context.ConnectionAborted);
        if (!isRegistered)
        {
            throw new HubException("Session ou identifiant candidat invalide.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionCode));

        var status = _registry.Join(sessionCode, candidateId, Context.ConnectionId);
        await _examRepository.MarkCandidateConnectedAsync(sessionCode, candidateId, Context.ConnectionAborted);

        CerebroTelemetry.CandidatesJoined.Add(1);
        await _activityStore.RecordAsync(
            sessionCode, candidateId, SessionActivityEventType.CandidateJoined, detail: null,
            Context.ConnectionAborted);

        await Clients.Group(DashboardGroup(sessionCode)).CandidateJoined(status);
    }

    // Ces méthodes ne sont invoquées que par le dashboard (jamais par les agents candidats), qui
    // s'authentifie désormais par cookie de session avant de charger la page — voir Program.cs.
    [Authorize]
    public async Task JoinAsDashboard(string sessionCode)
        => await Groups.AddToGroupAsync(Context.ConnectionId, DashboardGroup(sessionCode));

    [Authorize]
    public Task<IReadOnlyList<ExamSessionSummaryDto>> GetPlannedSessions()
        => _examRepository.GetSessionsAsync(Context.ConnectionAborted);

    // Même logique de provisioning que AdminCli.Provision (voir Admin/ExamProvisioner.cs) : le
    // dashboard accepte le même roster JSON que celui passé à `--input`, collé ou chargé depuis un
    // fichier côté navigateur, plutôt que d'exiger un accès CLI/SSH au serveur le jour de l'examen.
    [Authorize]
    public async Task<int> CreateSession(string sessionCode, string rosterJson)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Session.Create");
        activity?.SetTag("cerebro.session_code", sessionCode);

        int candidateCount;
        try
        {
            candidateCount = await ExamProvisioner.ProvisionAsync(
                _examRepository, sessionCode, rosterJson, Context.ConnectionAborted);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }

        CerebroTelemetry.SessionsCreated.Add(1);
        await _activityStore.RecordAsync(
            sessionCode, candidateId: null, SessionActivityEventType.SessionCreated,
            detail: $"{{\"candidateCount\":{candidateCount}}}", Context.ConnectionAborted);

        return candidateCount;
    }

    [Authorize]
    public Task<IReadOnlyList<CandidateRosterEntryDto>> GetCandidateRoster(string sessionCode)
        => _examRepository.GetCandidatesAsync(sessionCode, Context.ConnectionAborted);

    [Authorize]
    public Task<IReadOnlyList<SessionActivityEventDto>> GetSessionActivity(string sessionCode)
        => _activityStore.GetActivityAsync(sessionCode, Context.ConnectionAborted);

    [Authorize]
    public async Task StartSession(string sessionCode)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Session.Start");
        activity?.SetTag("cerebro.session_code", sessionCode);

        if (!await _examRepository.SessionExistsAsync(sessionCode, Context.ConnectionAborted))
        {
            throw new HubException("Session introuvable.");
        }

        await _examRepository.MarkStartedAsync(sessionCode, Context.ConnectionAborted);
        var startedAt = await _examRepository.GetStartedAtAsync(sessionCode, Context.ConnectionAborted);

        CerebroTelemetry.SessionsStarted.Add(1);
        await _activityStore.RecordAsync(
            sessionCode, candidateId: null, SessionActivityEventType.SessionStarted, detail: null,
            Context.ConnectionAborted);

        await Clients.Group(DashboardGroup(sessionCode)).SessionStarted(sessionCode, startedAt!.Value);
    }

    [Authorize]
    public async Task StopSession(string sessionCode)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Session.Stop");
        activity?.SetTag("cerebro.session_code", sessionCode);

        if (!await _examRepository.SessionExistsAsync(sessionCode, Context.ConnectionAborted))
        {
            throw new HubException("Session introuvable.");
        }

        await _examRepository.MarkEndedAsync(sessionCode, Context.ConnectionAborted);

        CerebroTelemetry.SessionsEnded.Add(1);
        await _activityStore.RecordAsync(
            sessionCode, candidateId: null, SessionActivityEventType.SessionEnded, detail: null,
            Context.ConnectionAborted);

        await Clients.Group(DashboardGroup(sessionCode)).SessionEnded(sessionCode, DateTimeOffset.UtcNow);
    }

    [Authorize]
    public Task<IReadOnlyList<CandidateStatusDto>> GetSnapshot(string sessionCode)
        => Task.FromResult(_registry.GetSnapshot(sessionCode));

    public async Task Ping()
    {
        CerebroTelemetry.Pings.Add(1);

        var result = _registry.Heartbeat(Context.ConnectionId);
        if (result is null)
        {
            return;
        }

        var (sessionCode, candidate) = result.Value;
        await Clients.Group(DashboardGroup(sessionCode)).CandidateHeartbeat(candidate);
    }

    public async Task ReportReadiness(bool isReady, CaptureFailureReason? failureReason, string? failureDetail)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Candidate.ReportReadiness");

        var result = _registry.UpdateReadiness(Context.ConnectionId, isReady, failureReason, failureDetail);
        if (result is null)
        {
            return;
        }

        var (sessionCode, candidate) = result.Value;
        activity?.SetTag("cerebro.session_code", sessionCode);
        activity?.SetTag("cerebro.candidate_id", candidate.CandidateId);

        var detail = JsonSerializer.Serialize(new {isReady, failureReason, failureDetail});
        await _activityStore.RecordAsync(
            sessionCode, candidate.CandidateId, SessionActivityEventType.ReadinessReported, detail,
            Context.ConnectionAborted);

        await Clients.Group(DashboardGroup(sessionCode)).CandidateReadinessUpdated(candidate);
    }

    public async Task UploadScreenshot(byte[] pngBytes)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Candidate.UploadScreenshot");
        activity?.SetTag("cerebro.screenshot_bytes", pngBytes.Length);

        var timestamp = DateTimeOffset.UtcNow;
        var result = _registry.RecordScreenshot(Context.ConnectionId, timestamp);
        if (result is null)
        {
            return;
        }

        var (sessionCode, candidate) = result.Value;
        activity?.SetTag("cerebro.session_code", sessionCode);
        activity?.SetTag("cerebro.candidate_id", candidate.CandidateId);

        await _screenshotStore.SaveAsync(sessionCode, candidate.CandidateId, pngBytes, timestamp);

        CerebroTelemetry.ScreenshotsReceived.Add(1);
        var detail = JsonSerializer.Serialize(new {bytes = pngBytes.Length});
        await _activityStore.RecordAsync(
            sessionCode, candidate.CandidateId, SessionActivityEventType.ScreenshotReceived, detail,
            Context.ConnectionAborted);

        await Clients.Group(DashboardGroup(sessionCode)).ScreenshotReceived(candidate.CandidateId, timestamp);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Candidate.Disconnect");

        var result = _registry.Disconnect(Context.ConnectionId);
        if (result is not null)
        {
            var (sessionCode, candidate) = result.Value;
            activity?.SetTag("cerebro.session_code", sessionCode);
            activity?.SetTag("cerebro.candidate_id", candidate.CandidateId);

            CerebroTelemetry.CandidatesDisconnected.Add(1);

            // Context.ConnectionAborted est déjà déclenché ici (la connexion est en train de se fermer) :
            // utiliser CancellationToken.None pour ne pas annuler l'écriture du journal avant qu'elle démarre.
            await _activityStore.RecordAsync(
                sessionCode, candidate.CandidateId, SessionActivityEventType.CandidateDisconnected, detail: null,
                CancellationToken.None);

            await Clients.Group(DashboardGroup(sessionCode)).CandidateDisconnected(candidate);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string SessionGroup(string sessionCode) => $"session:{sessionCode}";
    private static string DashboardGroup(string sessionCode) => $"dashboard:{sessionCode}";
}