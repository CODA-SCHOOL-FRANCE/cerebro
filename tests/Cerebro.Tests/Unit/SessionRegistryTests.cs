using Cerebro.Server.Services;
using Cerebro.Shared.Capture;
using NFluent;

namespace Cerebro.Tests.Unit;

[Trait("Category", "Unit")]
public class SessionRegistryTests
{
    [Fact]
    public void Join_ShouldReturnPendingCandidateStatus()
    {
        var registry = new SessionRegistry();

        var status = registry.Join("SESSION-A", "alice", "conn-1");

        Check.That(status.CandidateId).IsEqualTo("alice");
        Check.That(status.IsReady).IsNull();
        Check.That(status.LastScreenshotAt).IsNull();
    }

    [Fact]
    public void UpdateReadiness_ShouldUpdateStatusAndReturnSessionCode()
    {
        var registry = new SessionRegistry();
        registry.Join("SESSION-A", "alice", "conn-1");

        var result = registry.UpdateReadiness("conn-1", isReady: false, CaptureFailureReason.PermissionDenied, "détail");

        Check.That(result).IsNotNull();
        Check.That(result!.Value.SessionCode).IsEqualTo("SESSION-A");
        Check.That(result.Value.Candidate.IsReady).IsEqualTo(false);
        Check.That(result.Value.Candidate.FailureReason).IsEqualTo(CaptureFailureReason.PermissionDenied);
        Check.That(result.Value.Candidate.FailureDetail).IsEqualTo("détail");
    }

    [Fact]
    public void UpdateReadiness_ForUnknownConnection_ShouldReturnNull()
    {
        var registry = new SessionRegistry();

        var result = registry.UpdateReadiness("unknown-conn", isReady: true, null, null);

        Check.That(result).IsNull();
    }

    [Fact]
    public void RecordScreenshot_ShouldUpdateLastScreenshotTimestamp()
    {
        var registry = new SessionRegistry();
        registry.Join("SESSION-A", "alice", "conn-1");
        var timestamp = DateTimeOffset.UtcNow;

        var result = registry.RecordScreenshot("conn-1", timestamp);

        Check.That(result).IsNotNull();
        Check.That(result!.Value.Candidate.LastScreenshotAt).IsEqualTo(timestamp);
    }

    [Fact]
    public void Disconnect_ShouldMarkCandidateAsDisconnectedButKeepItInSnapshot()
    {
        var registry = new SessionRegistry();
        registry.Join("SESSION-A", "alice", "conn-1");

        var result = registry.Disconnect("conn-1");

        Check.That(result).IsNotNull();
        Check.That(result!.Value.Candidate.CandidateId).IsEqualTo("alice");
        Check.That(result.Value.Candidate.IsConnected).IsFalse();

        var snapshot = registry.GetSnapshot("SESSION-A");
        Check.That(snapshot.Select(c => c.CandidateId)).ContainsExactly("alice");
        Check.That(snapshot.Single().IsConnected).IsFalse();
    }

    [Fact]
    public void Disconnect_ForConnectionAlreadyReplacedByAReconnect_ShouldBeIgnored()
    {
        var registry = new SessionRegistry();
        registry.Join("SESSION-A", "alice", "conn-1");
        registry.Join("SESSION-A", "alice", "conn-2");

        var result = registry.Disconnect("conn-1");

        Check.That(result).IsNull();
        Check.That(registry.GetSnapshot("SESSION-A").Single().IsConnected).IsTrue();
    }

    [Fact]
    public void Heartbeat_ShouldUpdateLastSeenAt()
    {
        var registry = new SessionRegistry();
        registry.Join("SESSION-A", "alice", "conn-1");
        var before = DateTimeOffset.UtcNow;

        var result = registry.Heartbeat("conn-1");

        Check.That(result).IsNotNull();
        Check.That(result!.Value.Candidate.LastSeenAt >= before).IsTrue();
    }

    [Fact]
    public void Heartbeat_ForUnknownConnection_ShouldReturnNull()
    {
        var registry = new SessionRegistry();

        var result = registry.Heartbeat("unknown-conn");

        Check.That(result).IsNull();
    }

    [Fact]
    public void Disconnect_ForUnknownConnection_ShouldReturnNull()
    {
        var registry = new SessionRegistry();

        var result = registry.Disconnect("unknown-conn");

        Check.That(result).IsNull();
    }

    [Fact]
    public void GetSnapshot_ShouldOnlyReturnCandidatesFromRequestedSession()
    {
        var registry = new SessionRegistry();
        registry.Join("SESSION-A", "alice", "conn-1");
        registry.Join("SESSION-B", "bob", "conn-2");

        var snapshotA = registry.GetSnapshot("SESSION-A");
        var snapshotB = registry.GetSnapshot("SESSION-B");

        Check.That(snapshotA.Select(c => c.CandidateId)).ContainsExactly("alice");
        Check.That(snapshotB.Select(c => c.CandidateId)).ContainsExactly("bob");
    }

    [Fact]
    public void GetSnapshot_ForUnknownSession_ShouldReturnEmptyList()
    {
        var registry = new SessionRegistry();

        var snapshot = registry.GetSnapshot("UNKNOWN-SESSION");

        Check.That(snapshot).IsEmpty();
    }
}
