using System.IO.Compression;

namespace Cerebro.Server.Services;

public sealed class ScreenshotStore(IWebHostEnvironment environment) : IScreenshotStore
{
    private readonly string _rootPath = Path.Combine(environment.ContentRootPath, "screenshots");

    public async Task<string> SaveAsync(string sessionCode, string candidateId, byte[] imageBytes,
        DateTimeOffset timestamp)
    {
        var directory = Path.Combine(_rootPath, SanitizeSegment(sessionCode), SanitizeSegment(candidateId));
        Directory.CreateDirectory(directory);

        var fileName = $"{timestamp.UtcDateTime:yyyyMMdd_HHmmssfff}.webp";
        var fullPath = Path.Combine(directory, fileName);

        await File.WriteAllBytesAsync(fullPath, imageBytes);
        return fullPath;
    }

    public bool HasScreenshots(string sessionCode)
    {
        var directory = Path.Combine(_rootPath, SanitizeSegment(sessionCode));
        return Directory.Exists(directory) &&
               Directory.EnumerateFiles(directory, "*.webp", SearchOption.AllDirectories).Any();
    }

    // Un fichier par candidat/screenshot, réunis dans un seul zip organisé par candidat
    // (CAND0001/20260101_...webp) : le surveillant récupère tout d'un coup pour une session donnée,
    // sans avoir à fouiller le disque du serveur.
    //
    // `destination` est la réponse HTTP en streaming (voir Program.cs) : Kestrel y interdit les
    // I/O synchrones, or ZipArchive.Dispose() écrit la table centrale du zip via des appels Write
    // synchrones (pas d'équivalent async dans System.IO.Compression) — écrire directement dedans
    // lève "Synchronous operations are disallowed" et tronque le zip. On construit donc l'archive
    // dans un fichier temporaire (I/O synchrone sans restriction là), puis on ne copie que la
    // lecture finale de ce fichier vers `destination`, en async.
    public async Task WriteZipAsync(string sessionCode, Stream destination, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_rootPath, SanitizeSegment(sessionCode));
        var tempFilePath = Path.GetRandomFileName();

        try
        {
            if (Directory.Exists(directory))
            {
                await using var tempFileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
                await using var archive = new ZipArchive(tempFileStream, ZipArchiveMode.Create);

                foreach (var filePath in Directory.EnumerateFiles(directory, "*.webp", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entryName = Path.GetRelativePath(directory, filePath)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);

                    await using var entryStream = await entry.OpenAsync(cancellationToken);
                    await using var fileStream = File.OpenRead(filePath);
                    await fileStream.CopyToAsync(entryStream, cancellationToken);
                }
            }

            await using var finishedZip = File.OpenRead(tempFilePath);
            await finishedZip.CopyToAsync(destination, cancellationToken);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    // sessionCode/candidateId viennent des agents étudiants : on ne garde que [A-Za-z0-9-_]
    // pour empêcher toute traversée de répertoire via Path.Combine (ex: candidateId = "../../etc").
    private static string SanitizeSegment(string value)
    {
        var sanitized = new string(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}