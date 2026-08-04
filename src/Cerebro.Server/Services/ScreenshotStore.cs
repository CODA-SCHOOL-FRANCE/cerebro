namespace Cerebro.Server.Services;

public sealed class ScreenshotStore : IScreenshotStore
{
    private readonly string _rootPath;

    public ScreenshotStore(IWebHostEnvironment environment)
        => _rootPath = Path.Combine(environment.ContentRootPath, "screenshots");

    public async Task<string> SaveAsync(string sessionCode, string candidateId, byte[] pngBytes,
        DateTimeOffset timestamp)
    {
        var directory = Path.Combine(_rootPath, SanitizeSegment(sessionCode), SanitizeSegment(candidateId));
        Directory.CreateDirectory(directory);

        var fileName = $"{timestamp.UtcDateTime:yyyyMMdd_HHmmssfff}.png";
        var fullPath = Path.Combine(directory, fileName);

        await File.WriteAllBytesAsync(fullPath, pngBytes);
        return fullPath;
    }

    // sessionCode/candidateId viennent des agents étudiants : on ne garde que [A-Za-z0-9-_]
    // pour empêcher toute traversée de répertoire via Path.Combine (ex: candidateId = "../../etc").
    private static string SanitizeSegment(string value)
    {
        var sanitized = new string(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}