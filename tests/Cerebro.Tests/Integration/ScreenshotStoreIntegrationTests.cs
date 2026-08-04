using Cerebro.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using NFluent;

namespace Cerebro.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ScreenshotStoreIntegrationTests : IDisposable
{
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Cerebro.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

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
}
