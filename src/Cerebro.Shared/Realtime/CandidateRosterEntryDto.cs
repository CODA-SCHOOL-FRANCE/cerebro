namespace Cerebro.Shared.Realtime;

public sealed record CandidateRosterEntryDto
{
    public required string CandidateId { get; init; }
    public required string Name { get; init; }
    public required bool HasConnectedOnce { get; init; }
}
