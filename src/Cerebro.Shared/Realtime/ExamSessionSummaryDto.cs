namespace Cerebro.Shared.Realtime;

public sealed record ExamSessionSummaryDto
{
    public required string SessionCode { get; init; }
    public required int CandidateCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
}
