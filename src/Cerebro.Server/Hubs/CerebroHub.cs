using System.Text.Json;
using Cerebro.Server.Admin;
using Cerebro.Server.Data;
using Cerebro.Server.Services;
using Cerebro.Server.Telemetry;
using Cerebro.Shared.Capture;
using Cerebro.Shared.Realtime;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cerebro.Server.Hubs;

public sealed class CerebroHub(
    ISessionRegistry registry,
    IScreenshotStore screenshotStore,
    IExamRepository examRepository,
    ISessionActivityStore activityStore) : Hub<ICerebroDashboardClient>
{
    private const string CerebroSessionCodeTag = "cerebro.session_code";
    private const string CerebroCandidateIdTag = "cerebro.candidate_id";

    public async Task JoinAsCandidate(string sessionCode, string candidateId)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Candidate.Join");
        activity?.SetTag(CerebroSessionCodeTag, sessionCode);
        activity?.SetTag(CerebroCandidateIdTag, candidateId);

        if (await examRepository.IsSessionEndedAsync(sessionCode, Context.ConnectionAborted))
        {
            throw new HubException("Cette session est terminée, les connexions ne sont plus acceptées.");
        }

        if (!await examRepository.IsCandidateRegisteredAsync(
                sessionCode,
                candidateId,
                Context.ConnectionAborted))
        {
            throw new HubException("Session ou identifiant candidat invalide.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionCode));

        var status = registry.Join(sessionCode, candidateId, Context.ConnectionId);
        await examRepository.MarkCandidateConnectedAsync(sessionCode, candidateId, Context.ConnectionAborted);

        CerebroTelemetry.CandidatesJoined.Add(1);
        await activityStore.RecordAsync(
            sessionCode,
            candidateId,
            SessionActivityEventType.CandidateJoined,
            detail: null,
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
        => examRepository.GetSessionsAsync(Context.ConnectionAborted);

    // Même logique de provisioning que AdminCli.Provision (voir Admin/ExamProvisioner.cs) : le
    // dashboard accepte le même roster JSON que celui passé à `--input`, collé ou chargé depuis un
    // fichier côté navigateur, plutôt que d'exiger un accès CLI/SSH au serveur le jour de l'examen.
    [Authorize]
    public async Task<int> CreateSession(string sessionCode, string rosterJson)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Session.Create");
        activity?.SetTag(CerebroSessionCodeTag, sessionCode);

        int candidateCount;
        try
        {
            candidateCount = await ExamProvisioner.ProvisionAsync(
                examRepository,
                sessionCode,
                rosterJson,
                Context.ConnectionAborted
            );
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }

        CerebroTelemetry.SessionsCreated.Add(1);
        await activityStore.RecordAsync(
            sessionCode, candidateId: null, SessionActivityEventType.SessionCreated,
            detail: $"{{\"candidateCount\":{candidateCount}}}", Context.ConnectionAborted);

        return candidateCount;
    }

    [Authorize]
    public Task<IReadOnlyList<CandidateRosterEntryDto>> GetCandidateRoster(string sessionCode)
        => examRepository.GetCandidatesAsync(sessionCode, Context.ConnectionAborted);

    [Authorize]
    public Task<IReadOnlyList<SessionActivityEventDto>> GetSessionActivity(string sessionCode)
        => activityStore.GetActivityAsync(sessionCode, Context.ConnectionAborted);

    [Authorize]
    public async Task StartSession(string sessionCode)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Session.Start");
        activity?.SetTag(CerebroSessionCodeTag, sessionCode);

        if (!await examRepository.SessionExistsAsync(sessionCode, Context.ConnectionAborted))
        {
            throw new HubException("Session introuvable.");
        }

        await examRepository.MarkStartedAsync(sessionCode, Context.ConnectionAborted);
        var startedAt = await examRepository.GetStartedAtAsync(sessionCode, Context.ConnectionAborted);

        CerebroTelemetry.SessionsStarted.Add(1);
        await activityStore.RecordAsync(
            sessionCode, candidateId: null, SessionActivityEventType.SessionStarted, detail: null,
            Context.ConnectionAborted);

        await Clients.Group(DashboardGroup(sessionCode)).SessionStarted(sessionCode, startedAt!.Value);
    }

    [Authorize]
    public async Task StopSession(string sessionCode)
    {
        using var activity = CerebroTelemetry.ActivitySource.StartActivity("Session.Stop");
        activity?.SetTag(CerebroSessionCodeTag, sessionCode);

        if (!await examRepository.SessionExistsAsync(sessionCode, Context.ConnectionAborted))
        {
            throw new HubException("Session introuvable.");
        }

        await examRepository.MarkEndedAsync(sessionCode, Context.ConnectionAborted);

        CerebroTelemetry.SessionsEnded.Add(1);
        await activityStore.RecordAsync(
            sessionCode, candidateId: null, SessionActivityEventType.SessionEnded, detail: null,
            Context.ConnectionAborted);

        await Clients.Group(DashboardGroup(sessionCode)).SessionEnded(sessionCode, DateTimeOffset.UtcNow);
    }

    [Authorize]
    public Task<IReadOnlyList<CandidateStatusDto>> GetSnapshot(string sessionCode)
        => Task.FromResult(registry.GetSnapshot(sessionCode));

    public async Task Ping()
    {
        CerebroTelemetry.Pings.Add(1);

        await registry.Heartbeat(Context.ConnectionId)
            .Tap(async result =>
                {
                    var (sessionCode, candidate) = result;
                    await Clients.Group(DashboardGroup(sessionCode)).CandidateHeartbeat(candidate);
                }
            );
    }

    public async Task ReportReadiness(bool isReady, CaptureFailureReason? failureReason, string? failureDetail)
        => await registry.UpdateReadiness(Context.ConnectionId, isReady, failureReason, failureDetail)
            .Tap(async result =>
            {
                using var activity = CerebroTelemetry.ActivitySource.StartActivity("Candidate.ReportReadiness");

                var (sessionCode, candidate) = result;
                activity?.SetTag(CerebroSessionCodeTag, sessionCode);
                activity?.SetTag(CerebroCandidateIdTag, candidate.CandidateId);

                var detail = JsonSerializer.Serialize(new {isReady, failureReason, failureDetail});
                await activityStore.RecordAsync(
                    sessionCode, candidate.CandidateId, SessionActivityEventType.ReadinessReported, detail,
                    Context.ConnectionAborted);

                await Clients.Group(DashboardGroup(sessionCode)).CandidateReadinessUpdated(candidate);
            });

    public async Task UploadScreenshot(byte[] pngBytes)
    {
        var timestamp = DateTimeOffset.UtcNow;
        await registry.RecordScreenshot(Context.ConnectionId, timestamp)
            .Tap(async result =>
            {
                using var activity = CerebroTelemetry.ActivitySource.StartActivity("Candidate.UploadScreenshot");
                activity?.SetTag("cerebro.screenshot_bytes", pngBytes.Length);

                var (sessionCode, candidate) = result;

                activity?.SetTag(CerebroSessionCodeTag, sessionCode);
                activity?.SetTag(CerebroCandidateIdTag, candidate.CandidateId);

                await screenshotStore.SaveAsync(sessionCode, candidate.CandidateId, pngBytes, timestamp);

                CerebroTelemetry.ScreenshotsReceived.Add(1);
                var detail = JsonSerializer.Serialize(new {bytes = pngBytes.Length});
                await activityStore.RecordAsync(
                    sessionCode, candidate.CandidateId, SessionActivityEventType.ScreenshotReceived, detail,
                    Context.ConnectionAborted);

                await Clients.Group(DashboardGroup(sessionCode)).ScreenshotReceived(candidate.CandidateId, timestamp);
            });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await registry.Disconnect(Context.ConnectionId)
            .Tap(async result =>
            {
                using var activity = CerebroTelemetry.ActivitySource.StartActivity("Candidate.Disconnect");

                var (sessionCode, candidate) = result;
                activity?.SetTag(CerebroSessionCodeTag, sessionCode);
                activity?.SetTag(CerebroCandidateIdTag, candidate.CandidateId);

                CerebroTelemetry.CandidatesDisconnected.Add(1);

                // Context.ConnectionAborted est déjà déclenché ici (la connexion est en train de se fermer) :
                // utiliser CancellationToken.None pour ne pas annuler l'écriture du journal avant qu'elle démarre.
                await activityStore.RecordAsync(
                    sessionCode,
                    candidate.CandidateId,
                    SessionActivityEventType.CandidateDisconnected,
                    detail: null,
                    CancellationToken.None);

                await Clients.Group(DashboardGroup(sessionCode)).CandidateDisconnected(candidate);
            });

        await base.OnDisconnectedAsync(exception);
    }

    private static string SessionGroup(string sessionCode) => $"session:{sessionCode}";
    private static string DashboardGroup(string sessionCode) => $"dashboard:{sessionCode}";
}