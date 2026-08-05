namespace Cerebro.Server.Services;

public interface IScreenshotStore
{
    Task<string> SaveAsync(string sessionCode, string candidateId, byte[] pngBytes, DateTimeOffset timestamp);
    bool HasScreenshots(string sessionCode);
    Task WriteZipAsync(string sessionCode, Stream destination, CancellationToken cancellationToken);
}
