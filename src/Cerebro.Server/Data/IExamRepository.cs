using Cerebro.Shared.Realtime;

namespace Cerebro.Server.Data;

public interface IExamRepository
{
    Task<bool> SessionExistsAsync(string sessionCode, CancellationToken cancellationToken);

    Task<long> CreateSessionAsync(string sessionCode, CancellationToken cancellationToken);

    Task AddCandidateAsync(long sessionId, string candidateId, string name, CancellationToken cancellationToken);

    Task MarkCandidateConnectedAsync(string sessionCode, string candidateId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CandidateRosterEntryDto>> GetCandidatesAsync(
        string sessionCode, CancellationToken cancellationToken);

    Task MarkStartedAsync(string sessionCode, CancellationToken cancellationToken);

    Task MarkEndedAsync(string sessionCode, CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetStartedAtAsync(string sessionCode, CancellationToken cancellationToken);

    Task<bool> IsSessionEndedAsync(string sessionCode, CancellationToken cancellationToken);

    Task<bool> IsCandidateRegisteredAsync(string sessionCode, string candidateId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExamSessionSummaryDto>> GetSessionsAsync(CancellationToken cancellationToken);

    Task DeleteSessionAsync(string sessionCode, CancellationToken cancellationToken);
}
