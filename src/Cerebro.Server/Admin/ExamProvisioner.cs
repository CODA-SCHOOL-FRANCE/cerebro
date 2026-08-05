using System.Text.Json;
using Cerebro.Server.Data;

namespace Cerebro.Server.Admin;

// Logique de provisioning partagée entre AdminCli (roster lu depuis un fichier local) et
// CerebroHub.CreateSession (roster collé/chargé depuis le dashboard) : une seule implémentation
// pour créer une session à partir d'un roster JSON et y enregistrer chaque candidat, afin que les
// deux points d'entrée valident et échouent exactement de la même façon.
public static class ExamProvisioner
{
    private static readonly JsonSerializerOptions RosterJsonOptions = new() {PropertyNameCaseInsensitive = true};

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
}
