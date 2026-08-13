using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Cerebro.Server.Telemetry;

public static class CerebroTelemetry
{
    public const string SourceName = "Cerebro.Server";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> CandidatesJoined =
        Meter.CreateCounter<long>("cerebro.candidates.joined", description: "Nombre de connexions candidat.");

    public static readonly Counter<long> CandidatesDisconnected =
        Meter.CreateCounter<long>("cerebro.candidates.disconnected", description: "Nombre de déconnexions candidat.");

    public static readonly Counter<long> Pings =
        Meter.CreateCounter<long>("cerebro.pings.received", description: "Battements de vie reçus des agents.");

    public static readonly Counter<long> ScreenshotsReceived =
        Meter.CreateCounter<long>("cerebro.screenshots.received", description: "Screenshots reçus.");

    public static readonly Counter<long> SessionsCreated =
        Meter.CreateCounter<long>("cerebro.sessions.created", description: "Sessions provisionnées (CLI ou dashboard).");

    public static readonly Counter<long> SessionsStarted =
        Meter.CreateCounter<long>("cerebro.sessions.started", description: "Sessions démarrées depuis le dashboard.");

    public static readonly Counter<long> SessionsEnded =
        Meter.CreateCounter<long>("cerebro.sessions.ended", description: "Sessions arrêtées depuis le dashboard.");

    public static readonly Counter<long> SessionsDeleted =
        Meter.CreateCounter<long>("cerebro.sessions.deleted", description: "Sessions supprimées depuis le dashboard.");
}
