using Cerebro.Agent;
using Cerebro.Agent.Capture;
using Cerebro.Agent.Configuration;
using Cerebro.Agent.Realtime;
using ConsoleAppFramework;
using Microsoft.AspNetCore.SignalR;

PrintBanner();

// Pas de gestion spéciale pour args.Length == 0 : RunAgentAsync a déjà tous ses paramètres
// optionnels et gère elle-même le repli (fichier de config, variable d'env, prompt) — un bloc
// séparé ici forcerait un prompt pour serverUrl/sessionCode/candidateId avant même que
// RunAgentAsync ne s'exécute, court-circuitant xavier.config.json.
await ConsoleApp.RunAsync(args, RunAgentAsync);

/// <summary>Lance l'agent Cerebro (Xavier) : capture d'écran et signal de présence envoyés au serveur pour la durée de l'épreuve.</summary>
/// <param name="serverUrl">URL du serveur (ex: https://192.168.1.10:8443). Par défaut : fichier xavier.config.json à côté de l'exécutable, sinon demandée interactivement.</param>
/// <param name="sessionCode">Code de session.</param>
/// <param name="candidateId">Identifiant candidat (votre id).</param>
/// <param name="certThumbprint">Empreinte du certificat serveur (HTTPS auto-signé). Par défaut : fichier xavier.config.json, sinon variable d'environnement CEREBRO_SERVER_CERT_THUMBPRINT, sinon demandée interactivement si l'URL est en HTTPS.</param>
static async Task<int> RunAgentAsync(
    [Argument] string? serverUrl = null,
    [Argument] string? sessionCode = null,
    [Argument] string? candidateId = null,
    [Argument] string? certThumbprint = null,
    CancellationToken cancellationToken = default)
{
    var configFile = AgentConfigFile.Load(AppContext.BaseDirectory);

    serverUrl ??= configFile?.ServerUrl;
    serverUrl ??= Prompt("URL du serveur (ex: https://192.168.1.10:8443) : ");
    sessionCode ??= Prompt("Code de session : ");
    candidateId ??= Prompt("Identifiant candidat (votre id) : ");
    certThumbprint ??= configFile?.CertThumbprint;
    certThumbprint ??= Environment.GetEnvironmentVariable("CEREBRO_SERVER_CERT_THUMBPRINT");

    // Uniquement pertinent en HTTPS (certificat auto-signé épinglé par empreinte, voir
    // CertificateThumbprintValidator) — ne pas demander pour une connexion HTTP simple (réseau de
    // test local, pas de TLS). Laisser vide = validation TLS standard du système (cas d'un vrai
    // certificat reconnu, hors réseau d'épreuve isolé).
    if (string.IsNullOrWhiteSpace(certThumbprint) &&
        serverUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        var input = Prompt("Empreinte du certificat serveur (laisser vide si non applicable) : ");
        certThumbprint = string.IsNullOrWhiteSpace(input) ? null : input;
    }

    var options = new AgentOptions(serverUrl, sessionCode, candidateId);

    Console.WriteLine(
        $"Connexion à {options.ServerUrl} — session {options.SessionCode} — candidat {options.CandidateId}...");

    try
    {
        var capturer = ScreenCapturerFactory.Create();
        await using ICerebroConnection connection = new SignalRCerebroConnection(options.ServerUrl, certThumbprint);
        var runner = new AgentRunner(capturer, connection, options);

        runner.Connected += () =>
            LogColored(ConsoleColor.Green, "Vous êtes bien connecté à Cerebro.");
        runner.Activity += message => Log(message);

        await runner.RunAsync(cancellationToken);

        Console.WriteLine("Agent arrêté.");
        return 0;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        Console.WriteLine("Agent arrêté.");
        return 0;
    }
    catch (HubException ex)
    {
        // Le serveur a rejeté la demande (session ou identifiant candidat invalide, session terminée...) :
        // c'est une erreur de saisie côté candidat, pas un bug — pas de stack trace, un message clair suffit.
        LogColored(ConsoleColor.Red, DescribeHubError(ex));
        return 1;
    }
    catch (Exception ex)
    {
        // Toute autre erreur (URL malformée, serveur injoignable, certificat refusé...) : même logique,
        // l'agent ne doit jamais planter avec une stack trace face au candidat.
        LogColored(ConsoleColor.Red, $"Impossible de se connecter au serveur : {ex.Message}");
        return 1;
    }
}

static string DescribeHubError(HubException ex)
{
    // Le message brut ressemble à "An unexpected error occurred invoking 'X' on the server.
    // HubException: <message métier>" — on n'en garde que la partie utile au candidat.
    const string marker = "HubException: ";
    var index = ex.Message.IndexOf(marker, StringComparison.Ordinal);
    return index >= 0 ? ex.Message[(index + marker.Length)..] : ex.Message;
}

static string Prompt(string label)
{
    Console.Write(label);
    return Console.ReadLine() ?? string.Empty;
}

static void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

static void LogColored(ConsoleColor color, string message)
{
    var previousColor = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Log(message);
    Console.ForegroundColor = previousColor;
}

// Premier repère visuel pour le candidat au lancement (terminal souvent peu familier pour un
// public non technique) : confirme d'un coup d'œil qu'il a bien lancé Xavier, avant même les
// invites interactives ou les logs de connexion.
static void PrintBanner()
{
    const string art = """
        __   __     __      _______ ______ _____
        \ \ / /    /\ \    / /_   _|  ____|  __ \
         \ V /    /  \ \  / /  | | | |__  | |__) |
          > <    / /\ \ \/ /   | | |  __| |  _  /
         / . \  / ____ \  /   _| |_| |____| | \ \
        /_/ \_\/_/    \_\/   |_____|______|_|  \_\
        """;

    var previousColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(art);
    Console.ForegroundColor = previousColor;
    Console.WriteLine("Agent candidat Cerebro");
    Console.WriteLine();
}