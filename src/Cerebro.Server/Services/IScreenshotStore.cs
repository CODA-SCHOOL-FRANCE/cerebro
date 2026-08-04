namespace Cerebro.Server.Services;

public interface IScreenshotStore
{
    Task<string> SaveAsync(string sessionCode, string candidateId, byte[] pngBytes, DateTimeOffset timestamp);
}
