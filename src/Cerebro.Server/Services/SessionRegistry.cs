using System.Collections.Concurrent;
using Cerebro.Shared.Capture;
using Cerebro.Shared.Realtime;
using CSharpFunctionalExtensions;
using static CSharpFunctionalExtensions.Maybe;

namespace Cerebro.Server.Services;

public record CandidateInSessionDto(string SessionCode, CandidateStatusDto Candidate);

public sealed class SessionRegistry : ISessionRegistry
{
    private sealed record ConnectionKey(string SessionCode, string CandidateId);

    private sealed class CandidateState
    {
        public required string CandidateId { get; init; }
        public required string ConnectionId { get; set; }
        public required DateTimeOffset ConnectedAt { get; init; }
        public bool IsConnected { get; set; } = true;
        public DateTimeOffset LastSeenAt { get; set; }
        public bool? IsReady { get; set; }
        public CaptureFailureReason? FailureReason { get; set; }
        public string? FailureDetail { get; set; }
        public DateTimeOffset? LastScreenshotAt { get; set; }

        public CandidateStatusDto ToDto() => new(
            CandidateId,
            IsConnected,
            IsReady,
            FailureReason,
            FailureDetail,
            ConnectedAt,
            LastSeenAt,
            LastScreenshotAt);
    }

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CandidateState>> _sessions = new();
    private readonly ConcurrentDictionary<string, ConnectionKey> _connections = new();

    public CandidateStatusDto Join(string sessionCode, string candidateId, string connectionId)
    {
        var candidates = _sessions.GetOrAdd(sessionCode, _ => new ConcurrentDictionary<string, CandidateState>());

        var now = DateTimeOffset.UtcNow;
        var state = new CandidateState
        {
            CandidateId = candidateId,
            ConnectionId = connectionId,
            ConnectedAt = now,
            LastSeenAt = now
        };

        candidates[candidateId] = state;
        _connections[connectionId] = new ConnectionKey(sessionCode, candidateId);

        return state.ToDto();
    }

    public Maybe<CandidateInSessionDto> UpdateReadiness(
        string connectionId,
        bool isReady,
        CaptureFailureReason? failureReason,
        string? failureDetail)
        => FindState(connectionId)
            .Map(found =>
            {
                var (sessionCode, state) = found;
                state.IsReady = isReady;
                state.FailureReason = failureReason;
                state.FailureDetail = failureDetail;
                state.LastSeenAt = DateTimeOffset.UtcNow;

                return new CandidateInSessionDto(sessionCode, state.ToDto());
            });

    public Maybe<CandidateInSessionDto> RecordScreenshot(string connectionId, DateTimeOffset timestamp)
        => FindState(connectionId)
            .Map(found =>
            {
                var (sessionCode, state) = found;
                state.LastScreenshotAt = timestamp;
                state.LastSeenAt = timestamp;

                return new CandidateInSessionDto(sessionCode, state.ToDto());
            });

    public Maybe<CandidateInSessionDto> Heartbeat(string connectionId)
        => FindState(connectionId)
            .Map(found =>
            {
                var (sessionCode, state) = found;
                state.LastSeenAt = DateTimeOffset.UtcNow;

                return new CandidateInSessionDto(sessionCode, state.ToDto());
            });

    public Maybe<CandidateInSessionDto> Disconnect(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var key) ||
            !_sessions.TryGetValue(key.SessionCode, out var candidates) ||
            !candidates.TryGetValue(key.CandidateId, out var state))
        {
            return None;
        }

        // Une reconnexion a déjà remplacé cette connexion par une nouvelle avant que l'évènement de
        // déconnexion de l'ancienne ne soit traité : ne pas marquer le candidat comme déconnecté à tort.
        if (state.ConnectionId != connectionId)
        {
            return None;
        }

        state.IsConnected = false;

        return new CandidateInSessionDto(key.SessionCode, state.ToDto());
    }

    public IReadOnlyList<CandidateStatusDto> GetSnapshot(string sessionCode)
        => _sessions.TryGetValue(sessionCode, out var candidates)
            ? candidates.Values.Select(c => c.ToDto()).ToList()
            : [];

    private Maybe<(string SessionCode, CandidateState State)> FindState(string connectionId)
    {
        if (!_connections.TryGetValue(connectionId, out var key))
        {
            return None;
        }

        if (_sessions.TryGetValue(key.SessionCode, out var candidates) &&
            candidates.TryGetValue(key.CandidateId, out var state))
        {
            return (key.SessionCode, state);
        }

        return None;
    }
}