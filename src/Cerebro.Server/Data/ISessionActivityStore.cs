using Cerebro.Shared.Realtime;

namespace Cerebro.Server.Data;

public interface ISessionActivityStore
{
    Task RecordAsync(
        string sessionCode, string? candidateId, string eventType, string? detail, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionActivityEventDto>> GetActivityAsync(string sessionCode, CancellationToken cancellationToken);
}
