using System.IO.Compression;
using Cerebro.Server.Services;
using NFluent;

namespace Cerebro.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ScreenshotStoreIntegrationTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly ScreenshotStore _store;

    public ScreenshotStoreIntegrationTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"cerebro-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _store = new ScreenshotStore(new FakeWebHostEnvironment { ContentRootPath = _contentRoot });
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ShouldWriteFileUnderSessionAndCandidateFolders()
    {
        byte[] pngBytes = [1, 2, 3, 4];

        var path = await _store.SaveAsync("SESSION-A", "alice", pngBytes, DateTimeOffset.UtcNow);

        Check.That(File.Exists(path)).IsTrue();
        Check.That(path).Contains(Path.Combine("screenshots", "SESSION-A", "alice"));
        Check.That(await File.ReadAllBytesAsync(path)).ContainsExactly(pngBytes);
    }

    [Fact]
    public async Task SaveAsync_ShouldSanitizePathTraversalAttemptsInSessionCode()
    {
        byte[] pngBytes = [1, 2, 3];

        var path = await _store.SaveAsync("../../etc", "alice", pngBytes, DateTimeOffset.UtcNow);

        var fullContentRoot = Path.GetFullPath(_contentRoot);
        var fullSavedPath = Path.GetFullPath(path);

        Check.That(fullSavedPath).StartsWith(fullContentRoot);
        Check.That(path).Not.Contains("..");
    }

    [Fact]
    public async Task SaveAsync_ShouldSanitizePathTraversalAttemptsInCandidateId()
    {
        byte[] pngBytes = [1, 2, 3];

        var path = await _store.SaveAsync("SESSION-A", "../../../etc/passwd", pngBytes, DateTimeOffset.UtcNow);

        var fullContentRoot = Path.GetFullPath(_contentRoot);
        var fullSavedPath = Path.GetFullPath(path);

        Check.That(fullSavedPath).StartsWith(fullContentRoot);
        Check.That(path).Not.Contains("..");
    }

    [Fact]
    public async Task WriteZipAsync_ShouldIncludeActivityLog_AlongsideCandidateScreenshots()
    {
        await _store.SaveAsync("SESSION-A", "alice", [1, 2, 3], DateTimeOffset.UtcNow);

        // Écrit là où FileSessionActivityStore place le journal : à la racine du dossier de
        // session, en sibling des sous-dossiers candidats.
        var sessionDirectory = Path.Combine(_contentRoot, "screenshots", "SESSION-A");
        await File.WriteAllTextAsync(Path.Combine(sessionDirectory, "activity.log"), "2026-01-01T00:00:00Z | CandidateJoined | alice | ");

        using var destination = new MemoryStream();
        await _store.WriteZipAsync("SESSION-A", destination, CancellationToken.None);

        destination.Position = 0;
        using var archive = new ZipArchive(destination, ZipArchiveMode.Read);
        var entryNames = archive.Entries.Select(e => e.FullName).ToList();

        Check.That(entryNames).Contains("activity.log");
        Check.That(entryNames.Any(name => name.StartsWith("alice/") && name.EndsWith(".webp"))).IsTrue();
    }

    [Fact]
    public void HasExportableContent_ShouldReturnTrue_WhenOnlyActivityLogExists_NoScreenshots()
    {
        var sessionDirectory = Path.Combine(_contentRoot, "screenshots", "SESSION-A");
        Directory.CreateDirectory(sessionDirectory);
        File.WriteAllText(Path.Combine(sessionDirectory, "activity.log"), "2026-01-01T00:00:00Z | SessionCreated |  | 1 candidat(s)");

        Check.That(_store.HasExportableContent("SESSION-A")).IsTrue();
    }

    [Fact]
    public void HasExportableContent_ShouldReturnFalse_WhenSessionDirectoryDoesNotExist()
        => Check.That(_store.HasExportableContent("UNKNOWN-SESSION")).IsFalse();
}
