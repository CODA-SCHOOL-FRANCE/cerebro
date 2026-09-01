using System.Security.Cryptography;
using System.Text.Json;
using Cerebro.Server.Data;
using Cerebro.Shared.Realtime;

namespace Cerebro.Server.Admin;

// Logique de provisioning partagée entre AdminCli (roster lu depuis un fichier local) et
// CerebroHub.CreateSession (roster collé/chargé depuis le dashboard) : une seule implémentation
// pour créer une session à partir d'un roster JSON et y enregistrer chaque candidat, afin que les
// deux points d'entrée valident et échouent exactement de la même façon.
public static class ExamProvisioner
{
    private static readonly JsonSerializerOptions RosterJsonOptions = new() {PropertyNameCaseInsensitive = true};

    // Longueur alignée sur les ids déjà vus dans les exports d'école (ex: "FFFB5AB1") : assez
    // court pour être recopié/annoncé à l'oral, assez long pour ne pas être devinable.
    private const int GeneratedCandidateIdLength = 8;

    public static async Task<int> ProvisionAsync(
        IExamRepository repository,
        string sessionCode,
        string rosterJson,
        CancellationToken cancellationToken,
        Action<string, ExamRosterStudent>? onCandidateAdded = null)
    {
        if (await repository.SessionExistsAsync(sessionCode, cancellationToken))
        {
            throw new InvalidOperationException(
                $"La session '{sessionCode}' existe déjà dans la base. Choisissez un autre code.");
        }

        ExamRosterFile? roster;
        try
        {
            roster = JsonSerializer.Deserialize<ExamRosterFile>(rosterJson, RosterJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Roster JSON invalide : {ex.Message}");
        }

        if (roster is null || roster.Etudiants.Count == 0)
        {
            throw new InvalidOperationException("Le roster ne contient aucun étudiant exploitable.");
        }

        var sessionId = await repository.CreateSessionAsync(sessionCode, cancellationToken);

        foreach (var (email, student) in roster.Etudiants)
        {
            await repository.AddCandidateAsync(sessionId, student.Id, student.Nom, cancellationToken);
            onCandidateAdded?.Invoke(email, student);
        }

        return roster.Etudiants.Count;
    }

    // Variante "saisie manuelle" (pas de roster JSON fourni par l'école) : le surveillant ne donne
    // que des noms, le serveur génère un identifiant candidat aléatoire pour chacun (voir
    // ExamRosterFile pour le rappel : cet id sert à la fois d'identifiant candidat et de secret de
    // connexion). Les ids générés sont retournés pour que l'appelant (CerebroHub.CreateSessionFromNames)
    // puisse les afficher au surveillant - c'est la seule fois où ils sont communiqués.
    public static async Task<IReadOnlyList<CandidateRosterEntryDto>> ProvisionFromNamesAsync(
        IExamRepository repository,
        string sessionCode,
        IReadOnlyList<string> studentNames,
        CancellationToken cancellationToken)
    {
        if (await repository.SessionExistsAsync(sessionCode, cancellationToken))
        {
            throw new InvalidOperationException(
                $"La session '{sessionCode}' existe déjà dans la base. Choisissez un autre code.");
        }

        var names = studentNames
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .ToList();

        if (names.Count == 0)
        {
            throw new InvalidOperationException("Aucun étudiant renseigné.");
        }

        var sessionId = await repository.CreateSessionAsync(sessionCode, cancellationToken);

        var usedIds = new HashSet<string>();
        var result = new List<CandidateRosterEntryDto>(names.Count);
        foreach (var name in names)
        {
            string candidateId;
            do
            {
                candidateId = RandomNumberGenerator.GetHexString(GeneratedCandidateIdLength, lowercase: false);
            } while (!usedIds.Add(candidateId));

            await repository.AddCandidateAsync(sessionId, candidateId, name, cancellationToken);
            result.Add(new CandidateRosterEntryDto {CandidateId = candidateId, Name = name, HasConnectedOnce = false});
        }

        return result;
    }
}
