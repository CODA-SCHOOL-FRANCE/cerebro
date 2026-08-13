using System.Globalization;
using System.Text;
using Cerebro.Shared.Realtime;

namespace Cerebro.Server.Data;

// Journal d'activité stocké en texte brut, un fichier par session, à la racine de son dossier de
// screenshots (screenshots/{session}/activity.log) plutôt qu'en base : un surveillant peut l'ouvrir
// et le relire directement (aucun outil SQLite nécessaire), et il voyage avec le reste des preuves
// de la session si screenshots/{session} est archivé/exporté. Voir Telemetry/CerebroTelemetry.cs
// pour les traces/métriques OpenTelemetry émises en parallèle (console par défaut).
public sealed class FileSessionActivityStore : ISessionActivityStore
{
    private const string FileName = "activity.log";
    private const string FieldSeparator = " | ";

    private readonly string _rootPath;

    // Une seule écriture/lecture à la fois, tous fichiers confondus : le volume d'évènements est
    // faible (quelques-uns par candidat et par minute), pas besoin d'un verrou par session — évite
    // les écritures concurrentes entrelacées/tronquées sur un même fichier.
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileSessionActivityStore(IWebHostEnvironment environment)
        => _rootPath = Path.Combine(environment.ContentRootPath, "screenshots");

    public async Task RecordAsync(
        string sessionCode, string? candidateId, string eventType, string? detail, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_rootPath, SanitizeSegment(sessionCode));

        var line = string.Join(FieldSeparator,
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            SanitizeField(eventType),
            SanitizeField(candidateId ?? ""),
            SanitizeField(detail ?? ""));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(directory);
            await File.AppendAllTextAsync(
                Path.Combine(directory, FileName), line + Environment.NewLine, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<SessionActivityEventDto>> GetActivityAsync(
        string sessionCode, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_rootPath, SanitizeSegment(sessionCode), FileName);
        if (!File.Exists(filePath))
        {
            return [];
        }

        await _lock.WaitAsync(cancellationToken);
        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        return lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseLine)
            .Reverse()
            .ToList();
    }

    private static SessionActivityEventDto ParseLine(string line)
    {
        var parts = line.Split(FieldSeparator, 4);
        return new SessionActivityEventDto
        {
            Timestamp = DateTimeOffset.Parse(parts[0], CultureInfo.InvariantCulture),
            EventType = parts[1],
            CandidateId = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null,
            Detail = parts.Length > 3 && parts[3].Length > 0 ? parts[3] : null
        };
    }

    // sessionCode vient du surveillant (dashboard/CLI) : on ne garde que [A-Za-z0-9-_] pour éviter
    // toute traversée de répertoire via Path.Combine (même défense que ScreenshotStore).
    private static string SanitizeSegment(string value)
    {
        var sanitized = new string(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    // Une ligne = un évènement : neutraliser les retours à la ligne dans les champs libres
    // (candidateId, detail) pour ne jamais casser ce format un-évènement-par-ligne.
    private static string SanitizeField(string value) => value.Replace("\r", " ").Replace("\n", " ");
}