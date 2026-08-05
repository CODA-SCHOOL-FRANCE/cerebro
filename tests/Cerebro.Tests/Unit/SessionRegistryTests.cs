using Cerebro.Server.Services;
using Cerebro.Shared.Capture;
using NFluent;

namespace Cerebro.Tests.Unit;

[Trait("Category", "Unit")]
public class SessionRegistryTests
{
    private readonly SessionRegistry _registry = new();

    [Fact]
    public void Join_ShouldReturnPendingCandidateStatus()
    {
        var status = _registry.Join("SESSION-A", "alice", "conn-1");

        Check.That(status.CandidateId).IsEqualTo("alice");
        Check.That(status.IsReady).IsNull();
        Check.That(status.LastScreenshotAt).IsNull();
    }

    [Fact]
    public void UpdateReadiness_ShouldUpdateStatusAndReturnSessionCode()
    {
        _registry.Join("SESSION-A", "alice", "conn-1");

        var result = _registry.UpdateReadiness("conn-1", isReady: false, CaptureFailureReason.PermissionDenied, "détail");

        Check.That(result).IsNotNull();
        Check.That(result!.Value.SessionCode).IsEqualTo("SESSION-A");
        Check.That(result.Value.Candidate.IsReady).IsEqualTo(false);
        Check.That(result.Value.Candidate.FailureReason).IsEqualTo(CaptureFailureReason.PermissionDenied);
        Check.That(result.Value.Candidate.FailureDetail).IsEqualTo("détail");
    }

    [Fact]
    public void UpdateReadiness_ForUnknownConnection_ShouldReturnNull()
    {
        var result = _registry.UpdateReadiness("unknown-conn", isReady: true, null, null);

        Check.That(result).IsNull();
    }

    [Fact]
    public void RecordScreenshot_ShouldUpdateLastScreenshotTimestamp()
    {
        _registry.Join("SESSION-A", "alice", "conn-1");
        var timestamp = DateTimeOffset.UtcNow;

        var result = _registry.RecordScreenshot("conn-1", timestamp);

        Check.That(result).IsNotNull();
        Check.That(result!.Value.Candidate.LastScreenshotAt).IsEqualTo(timestamp);
    }

    [Fact]
    public void Disconnect_ShouldMarkCandidateAsDisconnectedButKeepItInSnapshot()
    {
        _registry.Join("SESSION-A", "alice", "conn-1");

        var result = _registry.Disconnect("conn-1");

        Check.That(result).IsNotNull();
        Check.That(result!.Value.Candidate.CandidateId).IsEqualTo("alice");
        Check.That(result.Value.Candidate.IsConnected).IsFalse();

        var snapshot = _registry.GetSnapshot("SESSION-A");
        Check.That(snapshot.Select(c => c.CandidateId)).ContainsExactly("alice");
        Check.That(snapshot.Single().IsConnected).IsFalse();
    }

    [Fact]
    public void Disconnect_ForConnectionAlreadyReplacedByAReconnect_ShouldBeIgnored()
    {
        _registry.Join("SESSION-A", "alice", "conn-1");
        _registry.Join("SESSION-A", "alice", "conn-2");

        var result = _registry.Disconnect("conn-1");

        Check.That(result).IsNull();
        Check.That(_registry.GetSnapshot("SESSION-A").Single().IsConnected).IsTrue();
    }

    [Fact]
    public void Heartbeat_ShouldUpdateLastSeenAt()
    {
        _registry.Join("SESSION-A", "alice", "conn-1");
        var before = DateTimeOffset.UtcNow;

        var result = _registry.Heartbeat("conn-1");

        Check.That(result).IsNotNull();
        Check.That(result!.Value.Candidate.LastSeenAt >= before).IsTrue();
    }

    [Fact]
    public void Heartbeat_ForUnknownConnection_ShouldReturnNull()
    {
        var result = _registry.Heartbeat("unknown-conn");

        Check.That(result).IsNull();
    }

    [Fact]
    public void Disconnect_ForUnknownConnection_ShouldReturnNull()
    {
        var result = _registry.Disconnect("unknown-conn");

        Check.That(result).IsNull();
    }

    [Fact]
    public void GetSnapshot_ShouldOnlyReturnCandidatesFromRequestedSession()
    {
        _registry.Join("SESSION-A", "alice", "conn-1");
        _registry.Join("SESSION-B", "bob", "conn-2");

        var snapshotA = _registry.GetSnapshot("SESSION-A");
        var snapshotB = _registry.GetSnapshot("SESSION-B");

        Check.That(snapshotA.Select(c => c.CandidateId)).ContainsExactly("alice");
        Check.That(snapshotB.Select(c => c.CandidateId)).ContainsExactly("bob");
    }

    [Fact]
    public void GetSnapshot_ForUnknownSession_ShouldReturnEmptyList()
    {
        var snapshot = _registry.GetSnapshot("UNKNOWN-SESSION");

        Check.That(snapshot).IsEmpty();
    }
}
