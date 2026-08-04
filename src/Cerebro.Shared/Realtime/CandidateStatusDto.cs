using Cerebro.Shared.Capture;

namespace Cerebro.Shared.Realtime;

public sealed record CandidateStatusDto(
    string CandidateId,
    bool IsConnected,
    bool? IsReady,
    CaptureFailureReason? FailureReason,
    string? FailureDetail,
    DateTimeOffset ConnectedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? LastScreenshotAt);
