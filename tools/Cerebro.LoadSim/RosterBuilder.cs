using System.Text.Json;

namespace Cerebro.LoadSim;

// Construit un roster minimal, au format attendu par ExamProvisioner côté serveur (voir
// Admin/ExamRoster.cs), avec des candidats synthétiques aux identifiants prévisibles
// (ex: SIM0001, SIM0002...).
internal static class RosterBuilder
{
    public static (string RosterJson, IReadOnlyList<string> CandidateIds) Build(
        int candidateCount, string candidateIdPrefix)
    {
        var candidateIds = Enumerable.Range(1, candidateCount)
            .Select(i => $"{candidateIdPrefix}{i:0000}")
            .ToList();

        var etudiants = candidateIds.Select(id => new {nom = $"Candidat simulé {id}", id});

        var roster = new {etudiants};

        return (JsonSerializer.Serialize(roster), candidateIds);
    }
}
