using System.Collections.Concurrent;
using Cerebro.Shared.Capture;
using Cerebro.Shared.Realtime;

namespace Cerebro.Server.Services;

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

    public (string SessionCode, CandidateStatusDto Candidate)? UpdateReadiness(
        string connectionId, bool isReady, CaptureFailureReason? failureReason, string? failureDetail)
    {
        var found = FindState(connectionId);
        if (found is null)
        {
            return null;
        }

        var (sessionCode, state) = found.Value;
        state.IsReady = isReady;
        state.FailureReason = failureReason;
        state.FailureDetail = failureDetail;
        state.LastSeenAt = DateTimeOffset.UtcNow;

        return (sessionCode, state.ToDto());
    }

    public (string SessionCode, CandidateStatusDto Candidate)? RecordScreenshot(string connectionId, DateTimeOffset timestamp)
    {
        var found = FindState(connectionId);
        if (found is null)
        {
            return null;
        }

        var (sessionCode, state) = found.Value;
        state.LastScreenshotAt = timestamp;
        state.LastSeenAt = timestamp;

        return (sessionCode, state.ToDto());
    }

    public (string SessionCode, CandidateStatusDto Candidate)? Heartbeat(string connectionId)
    {
        var found = FindState(connectionId);
        if (found is null)
        {
            return null;
        }

        var (sessionCode, state) = found.Value;
        state.LastSeenAt = DateTimeOffset.UtcNow;

        return (sessionCode, state.ToDto());
    }

    public (string SessionCode, CandidateStatusDto Candidate)? Disconnect(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var key))
        {
            return null;
        }

        if (!_sessions.TryGetValue(key.SessionCode, out var candidates) ||
            !candidates.TryGetValue(key.CandidateId, out var state))
        {
            return null;
        }

        // Une reconnexion a déjà remplacé cette connexion par une nouvelle avant que l'évènement de
        // déconnexion de l'ancienne ne soit traité : ne pas marquer le candidat comme déconnecté à tort.
        if (state.ConnectionId != connectionId)
        {
            return null;
        }

        state.IsConnected = false;

        return (key.SessionCode, state.ToDto());
    }

    public IReadOnlyList<CandidateStatusDto> GetSnapshot(string sessionCode) 
        => _sessions.TryGetValue(sessionCode, out var candidates) 
            ? candidates.Values.Select(c => c.ToDto()).ToList() 
            : [];

    private (string SessionCode, CandidateState State)? FindState(string connectionId)
    {
        if (!_connections.TryGetValue(connectionId, out var key))
        {
            return null;
        }

        if (_sessions.TryGetValue(key.SessionCode, out var candidates) &&
            candidates.TryGetValue(key.CandidateId, out var state))
        {
            return (key.SessionCode, state);
        }

        return null;
    }
}
