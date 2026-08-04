using Cerebro.Shared.Capture;
using Cerebro.Shared.Realtime;

namespace Cerebro.Server.Services;

public interface ISessionRegistry
{
    CandidateStatusDto Join(string sessionCode, string candidateId, string connectionId);

    (string SessionCode, CandidateStatusDto Candidate)? UpdateReadiness(
        string connectionId, bool isReady, CaptureFailureReason? failureReason, string? failureDetail);

    (string SessionCode, CandidateStatusDto Candidate)? RecordScreenshot(string connectionId, DateTimeOffset timestamp);

    (string SessionCode, CandidateStatusDto Candidate)? Heartbeat(string connectionId);

    (string SessionCode, CandidateStatusDto Candidate)? Disconnect(string connectionId);

    IReadOnlyList<CandidateStatusDto> GetSnapshot(string sessionCode);
}
