namespace Cerebro.Shared.Realtime;

public interface ICerebroDashboardClient
{
    Task CandidateJoined(CandidateStatusDto status);
    Task CandidateReadinessUpdated(CandidateStatusDto status);
    Task CandidateHeartbeat(CandidateStatusDto status);
    Task ScreenshotReceived(string candidateId, DateTimeOffset timestamp);
    Task CandidateDisconnected(CandidateStatusDto status);
    Task SessionStarted(string sessionCode, DateTimeOffset startedAt);
    Task SessionEnded(string sessionCode, DateTimeOffset endedAt);
}
