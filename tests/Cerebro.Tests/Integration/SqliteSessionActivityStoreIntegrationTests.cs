using Cerebro.Server.Data;
using NFluent;

namespace Cerebro.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SqliteSessionActivityStoreIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteSessionActivityStore _store;

    public SqliteSessionActivityStoreIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cerebro-activity-tests-{Guid.NewGuid():N}.db");
        _store = new SqliteSessionActivityStore($"Data Source={_dbPath}");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task RecordAsync_ThenGetActivity_ShouldReturnEventInOrder()
    {
        await _store.RecordAsync("SESSION-A", "FFFB5AB1", "CandidateJoined", null, CancellationToken.None);
        await _store.RecordAsync("SESSION-A", "FFFB5AB1", "ScreenshotReceived", """{"bytes":1234}""", CancellationToken.None);
        await _store.RecordAsync("SESSION-A", null, "SessionStarted", null, CancellationToken.None);

        var events = await _store.GetActivityAsync("SESSION-A", CancellationToken.None);

        Check.That(events.Select(e => e.EventType)).ContainsExactly("CandidateJoined", "ScreenshotReceived", "SessionStarted");
        Check.That(events[1].CandidateId).IsEqualTo("FFFB5AB1");
        Check.That(events[1].Detail).IsEqualTo("""{"bytes":1234}""");
        Check.That(events[2].CandidateId).IsNull();
    }

    [Fact]
    public async Task GetActivity_ShouldOnlyReturnEventsFromRequestedSession()
    {
        await _store.RecordAsync("SESSION-A", "FFFB5AB1", "CandidateJoined", null, CancellationToken.None);
        await _store.RecordAsync("SESSION-B", "0770F2DB", "CandidateJoined", null, CancellationToken.None);

        var events = await _store.GetActivityAsync("SESSION-A", CancellationToken.None);

        Check.That(events.Select(e => e.CandidateId)).ContainsExactly("FFFB5AB1");
    }

    [Fact]
    public async Task GetActivity_ForUnknownSession_ShouldReturnEmptyList()
    {
        var events = await _store.GetActivityAsync("UNKNOWN-SESSION", CancellationToken.None);

        Check.That(events).IsEmpty();
    }
}
