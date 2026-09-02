using System.Text;
using Cerebro.Server.Data;
using Cerebro.Server.Tls;
using ConsoleAppFramework;
using static System.Environment;

namespace Cerebro.Server.Admin;

public static class AdminCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        ExitCode = 0;

        var app = ConsoleApp.Create();
        app.UseFilter<QuietErrorFilter>();
        app.Add<AdminCommands>();
        await app.RunAsync(args);

        return ExitCode;
    }
}

internal sealed class QuietErrorFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        try
        {
            await Next.InvokeAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            ExitCode = 1;
            ConsoleApp.LogError(ex.Message);
        }
    }
}

internal sealed class AdminCommands
{
    /// <summary>Provisionne une session d'épreuve à partir d'un roster JSON.</summary>
    /// <param name="session">Code de la session à créer.</param>
    /// <param name="input">Chemin vers le fichier roster JSON.</param>
    /// <param name="db">Chemin vers le fichier de base SQLite.</param>
    public async Task Provision(string session, string input, string db = "db/cerebro.db")
    {
        if (!File.Exists(input))
        {
            throw new InvalidOperationException($"Fichier d'entrée introuvable : '{input}'.");
        }

        IExamRepository repository = new SqliteExamRepository($"Data Source={db}");
        var rosterJson = await File.ReadAllTextAsync(input);

        var count = await ExamProvisioner.ProvisionAsync(
            repository, session, rosterJson, CancellationToken.None,
            onCandidateAdded: student => Console.WriteLine($"  {student.Nom} ({student.Id})"));

        Console.WriteLine($"Session '{session}' provisionnée avec {count} candidat(s).");
    }

    /// <summary>Démarre une session d'épreuve déjà provisionnée.</summary>
    /// <param name="session">Code de la session à démarrer.</param>
    /// <param name="db">Chemin vers le fichier de base SQLite.</param>
    public async Task Start(string session, string db = "db/cerebro.db")
    {
        IExamRepository repository = new SqliteExamRepository($"Data Source={db}");

        if (!await repository.SessionExistsAsync(session, CancellationToken.None))
        {
            throw new InvalidOperationException($"Session '{session}' introuvable dans la base.");
        }

        await repository.MarkStartedAsync(session, CancellationToken.None);
        Console.WriteLine($"Session '{session}' démarrée.");
    }

    /// <summary>Définit (ou change) le mot de passe d'accès au dashboard.</summary>
    /// <param name="username">Nom d'utilisateur du surveillant.</param>
    /// <param name="db">Chemin vers le fichier de base SQLite.</param>
    public async Task SetPassword(string username, string db = "db/cerebro.db")
    {
        var password = ReadPasswordMasked("Mot de passe : ");
        var confirmation = ReadPasswordMasked("Confirmer le mot de passe : ");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Le mot de passe ne peut pas être vide.");
        }

        if (password != confirmation)
        {
            throw new InvalidOperationException("Les mots de passe ne correspondent pas.");
        }

        IDashboardCredentialsStore credentials = new SqliteDashboardCredentialsStore($"Data Source={db}");
        await credentials.SetCredentialsAsync(username, password, CancellationToken.None);

        Console.WriteLine($"Mot de passe défini pour '{username}'.");
    }

    /// <summary>Régénère le certificat TLS auto-signé du serveur (généré automatiquement au premier démarrage sinon).</summary>
    /// <param name="address">IP ou nom d'hôte du poste serveur sur le réseau d'épreuve (inclus comme SAN du certificat).</param>
    /// <param name="output">Chemin du fichier .pfx à écrire.</param>
    /// <param name="force">Écraser un certificat existant (change l'empreinte SHA-256 à recommuniquer aux agents).</param>
    public void GenerateCert(string address, string output = "db/cerebro.pfx", bool force = false)
    {
        if (File.Exists(output) && !force)
        {
            throw new InvalidOperationException(
                $"Un certificat existe déjà ('{output}'). Utiliser --force pour le régénérer " +
                "(l'empreinte à recommuniquer aux agents candidats changera alors).");
        }

        var certificate = ServerCertificateProvisioner.GenerateAndSave(output, address);

        Console.WriteLine($"Certificat généré : {output}");
        Console.WriteLine($"Empreinte SHA-256 (CEREBRO_SERVER_CERT_THUMBPRINT) : {ServerCertificateProvisioner.Sha256Thumbprint(certificate)}");
    }

    // Saisie masquée (aucun echo, ni dans le terminal ni dans l'historique shell) : contrairement
    // à Console.ReadLine, cette commande est destinée à être lancée à la main juste avant l'épreuve.
    private static string ReadPasswordMasked(string label)
    {
        Console.Write(label);
        var password = new StringBuilder();

        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                    Console.Write("\b \b");
                }
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Write('*');
            }
        }

        Console.WriteLine();
        return password.ToString();
    }
}