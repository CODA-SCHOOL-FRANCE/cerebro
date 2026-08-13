using Cerebro.Server.Data;
using NFluent;

namespace Cerebro.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class FileSessionActivityStoreIntegrationTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly FileSessionActivityStore _store;

    public FileSessionActivityStoreIntegrationTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"cerebro-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _store = new FileSessionActivityStore(new FakeWebHostEnvironment { ContentRootPath = _contentRoot });
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RecordAsync_ThenGetActivity_ShouldReturnEventInOrder()
    {
        await _store.RecordAsync("SESSION-A", "FFFB5AB1", "CandidateJoined", null, CancellationToken.None);
        await _store.RecordAsync("SESSION-A", "FFFB5AB1", "ScreenshotReceived", "1 234 octets", CancellationToken.None);
        await _store.RecordAsync("SESSION-A", null, "SessionStarted", null, CancellationToken.None);

        var events = await _store.GetActivityAsync("SESSION-A", CancellationToken.None);

        Check.That(events.Select(e => e.EventType)).ContainsExactly("CandidateJoined", "ScreenshotReceived", "SessionStarted");
        Check.That(events[1].CandidateId).IsEqualTo("FFFB5AB1");
        Check.That(events[1].Detail).IsEqualTo("1 234 octets");
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

    [Fact]
    public async Task RecordAsync_ShouldWriteHumanReadableTextFile_NotJson()
    {
        await _store.RecordAsync("SESSION-A", "FFFB5AB1", "ScreenshotReceived", "173 404 octets", CancellationToken.None);

        var filePath = Path.Combine(_contentRoot, "screenshots", "SESSION-A", "activity.log");
        Check.That(File.Exists(filePath)).IsTrue();

        var content = await File.ReadAllTextAsync(filePath);
        Check.That(content).Not.Contains("{");
        Check.That(content).Contains("ScreenshotReceived");
        Check.That(content).Contains("173 404 octets");
    }

    [Fact]
    public async Task RecordAsync_ShouldWriteFileAtSessionRoot_SiblingToCandidateScreenshotFolders()
    {
        await _store.RecordAsync("SESSION-A", "FFFB5AB1", "CandidateJoined", null, CancellationToken.None);

        var filePath = Path.Combine(_contentRoot, "screenshots", "SESSION-A", "activity.log");
        Check.That(File.Exists(filePath)).IsTrue();
    }

    [Fact]
    public async Task RecordAsync_ShouldSanitizePathTraversalAttemptsInSessionCode()
    {
        await _store.RecordAsync("../../etc", null, "CandidateJoined", null, CancellationToken.None);

        var events = await _store.GetActivityAsync("../../etc", CancellationToken.None);
        Check.That(events).HasSize(1);

        var expectedPath = Path.GetFullPath(Path.Combine(_contentRoot, "screenshots", "etc"));
        Check.That(Directory.Exists(expectedPath)).IsTrue();
    }
}
