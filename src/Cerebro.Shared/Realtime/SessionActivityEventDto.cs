namespace Cerebro.Shared.Realtime;

public sealed record SessionActivityEventDto
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string EventType { get; init; }
    public string? CandidateId { get; init; }
    public string? Detail { get; init; }
}
