using System.Text.Json;

namespace Cerebro.LoadSim;

// Construit un roster minimal, au format attendu par ExamProvisioner côté serveur (voir
// Admin/ExamRoster.cs) : mêmes clés que l'export officiel de l'école, mais avec des candidats
// synthétiques aux identifiants prévisibles (ex: SIM0001, SIM0002...).
internal static class RosterBuilder
{
    public static (string RosterJson, IReadOnlyList<string> CandidateIds) Build(
        int candidateCount, string candidateIdPrefix)
    {
        var candidateIds = Enumerable.Range(1, candidateCount)
            .Select(i => $"{candidateIdPrefix}{i:0000}")
            .ToList();

        var etudiants = candidateIds.ToDictionary(
            id => $"{id.ToLowerInvariant()}@simulation.local",
            id => new { nom = $"Candidat simulé {id}", id, promo = (string?)null });

        var roster = new
        {
            ec = "SIMULATION",
            date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
            rattrapage = false,
            etudiants,
            diplome = (string?)null
        };

        return (JsonSerializer.Serialize(roster), candidateIds);
    }
}
