using Cerebro.Agent;
using Cerebro.Agent.Configuration;
using Cerebro.Agent.Realtime;
using Cerebro.LoadSim;
using ConsoleAppFramework;
using Microsoft.AspNetCore.SignalR;

await ConsoleApp.RunAsync(args, RunSimulationAsync);

/// <summary>
/// Simule une session Cerebro avec plusieurs candidats synthétiques, tous connectés depuis cette
/// machine à un serveur cible (local ou distant, déjà déployé). Provisionne d'abord la session
/// (comme le bouton "+ NOUVELLE SESSION" du dashboard) puis fait tourner un agent par candidat.
/// </summary>
/// <param name="serverUrl">URL du serveur cible (ex: https://192.168.1.10:8443).</param>
/// <param name="candidateCount">Nombre de candidats simulés à connecter.</param>
/// <param name="dashboardUsername">Identifiant dashboard du serveur cible. Demandé interactivement si omis.</param>
/// <param name="dashboardPassword">Mot de passe dashboard. Demandé interactivement (masqué) si omis.</param>
/// <param name="sessionCode">Code de session à créer. Par défaut : généré (SIM-yyyyMMdd-HHmmss).</param>
/// <param name="candidateIdPrefix">Préfixe des identifiants candidat générés (SIM0001, SIM0002...).</param>
/// <param name="certThumbprint">Empreinte du certificat serveur (HTTPS auto-signé), voir CEREBRO_SERVER_CERT_THUMBPRINT.</param>
/// <param name="minIntervalSeconds">Intervalle minimum entre deux captures simulées, en secondes.</param>
/// <param name="maxIntervalSeconds">Intervalle maximum entre deux captures simulées, en secondes.</param>
/// <param name="pingIntervalSeconds">Intervalle entre deux battements de vie, en secondes.</param>
static async Task<int> RunSimulationAsync(
    [Argument] string serverUrl,
    [Argument] int candidateCount,
    string? dashboardUsername = null,
    string? dashboardPassword = null,
    string? sessionCode = null,
    string candidateIdPrefix = "SIM",
    string? certThumbprint = null,
    int minIntervalSeconds = 15,
    int maxIntervalSeconds = 30,
    int pingIntervalSeconds = 20,
    CancellationToken cancellationToken = default)
{
    if (candidateCount < 1)
    {
        LogColored(ConsoleColor.Red, "candidateCount doit être au moins 1.");
        return 1;
    }

    dashboardUsername ??= Prompt("Identifiant dashboard : ");
    dashboardPassword ??= PromptMasked("Mot de passe dashboard : ");
    sessionCode ??= $"SIM-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";

    var (rosterJson, candidateIds) = RosterBuilder.Build(candidateCount, candidateIdPrefix);

    Console.WriteLine($"Provisionnement de la session '{sessionCode}' avec {candidateCount} candidat(s) simulé(s)...");
    try
    {
        var provisioned = await DashboardSessionProvisioner.CreateSessionAsync(
            serverUrl, dashboardUsername, dashboardPassword, certThumbprint,
            sessionCode, rosterJson, cancellationToken);
        LogColored(ConsoleColor.Green, $"Session '{sessionCode}' créée avec {provisioned} candidat(s).");
    }
    catch (HubException ex)
    {
        LogColored(ConsoleColor.Red, $"Provisioning échoué : {DescribeHubError(ex)}");
        return 1;
    }
    catch (Exception ex)
    {
        LogColored(ConsoleColor.Red, $"Provisioning échoué : {ex.Message}");
        return 1;
    }

    Console.WriteLine(
        $"Connexion de {candidateCount} candidat(s) simulé(s) à {serverUrl} (Ctrl+C pour arrêter la simulation)...");

    var candidateRuns = candidateIds.Select(candidateId => RunCandidateAsync(
        serverUrl, sessionCode, candidateId, certThumbprint,
        minIntervalSeconds, maxIntervalSeconds, pingIntervalSeconds, cancellationToken));

    await Task.WhenAll(candidateRuns);

    Console.WriteLine("Simulation arrêtée.");
    return 0;
}

// Chaque candidat gère ses propres erreurs sans faire échouer Task.WhenAll : un candidat qui
// plante (session arrêtée entre-temps, etc.) ne doit pas couper la simulation des autres.
static async Task RunCandidateAsync(
    string serverUrl,
    string sessionCode,
    string candidateId,
    string? certThumbprint,
    int minIntervalSeconds,
    int maxIntervalSeconds,
    int pingIntervalSeconds,
    CancellationToken cancellationToken)
{
    var options = new AgentOptions(
        serverUrl, sessionCode, candidateId, minIntervalSeconds, maxIntervalSeconds, pingIntervalSeconds);

    try
    {
        await using ICerebroConnection connection = new SignalRCerebroConnection(serverUrl, certThumbprint);
        var runner = new AgentRunner(new FakeScreenCapturer(), connection, options);

        runner.Connected += () => Log($"[{candidateId}] connecté.");
        runner.Activity += message => Log($"[{candidateId}] {message}");

        await runner.RunAsync(cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        // Ctrl+C : arrêt normal de ce candidat, rien à signaler.
    }
    catch (HubException ex)
    {
        LogColored(ConsoleColor.Red, $"[{candidateId}] {DescribeHubError(ex)}");
    }
    catch (Exception ex)
    {
        LogColored(ConsoleColor.Red, $"[{candidateId}] Erreur : {ex.Message}");
    }
}

static string DescribeHubError(HubException ex)
{
    const string marker = "HubException: ";
    var index = ex.Message.IndexOf(marker, StringComparison.Ordinal);
    return index >= 0 ? ex.Message[(index + marker.Length)..] : ex.Message;
}

static string Prompt(string label)
{
    Console.Write(label);
    return Console.ReadLine() ?? string.Empty;
}

static string PromptMasked(string label)
{
    Console.Write(label);
    var password = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write('*');
        }
    }

    Console.WriteLine();
    return password.ToString();
}

static void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

static void LogColored(ConsoleColor color, string message)
{
    var previousColor = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Log(message);
    Console.ForegroundColor = previousColor;
}
