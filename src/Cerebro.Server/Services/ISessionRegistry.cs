using Cerebro.Shared.Capture;
using Cerebro.Shared.Realtime;
using CSharpFunctionalExtensions;

namespace Cerebro.Server.Services;

public interface ISessionRegistry
{
    CandidateStatusDto Join(string sessionCode, string candidateId, string connectionId);

    Maybe<CandidateInSessionDto> UpdateReadiness(string connectionId, bool isReady, CaptureFailureReason? failureReason,
        string? failureDetail);

    Maybe<CandidateInSessionDto> RecordScreenshot(string connectionId, DateTimeOffset timestamp);

    Maybe<CandidateInSessionDto> Heartbeat(string connectionId);

    Maybe<CandidateInSessionDto> Disconnect(string connectionId);

    IReadOnlyList<CandidateStatusDto> GetSnapshot(string sessionCode);
}