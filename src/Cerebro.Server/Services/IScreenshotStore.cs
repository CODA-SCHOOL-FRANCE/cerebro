namespace Cerebro.Server.Services;

public interface IScreenshotStore
{
    Task<string> SaveAsync(string sessionCode, string candidateId, byte[] imageBytes, DateTimeOffset timestamp);
    bool HasExportableContent(string sessionCode);
    Task WriteZipAsync(string sessionCode, Stream destination, CancellationToken cancellationToken);
    void DeleteSessionData(string sessionCode);
}
