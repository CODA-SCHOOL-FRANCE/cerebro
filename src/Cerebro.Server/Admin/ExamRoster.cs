using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cerebro.Server.Admin;

// Format minimal du roster fourni pour provisionner une épreuve : uniquement ce qu'utilise
// ExamProvisioner (voir ExamProvisioner.cs). L'id de chaque étudiant sert à la fois d'identifiant
// candidat et de secret de connexion : pas besoin de générer un jeton séparé. Les champs en plus
// que les exports d'école ajoutent parfois (ec/évaluation, date, rattrapage, correcteurs...) sont
// silencieusement ignorés par la désérialisation (comportement par défaut de System.Text.Json face
// à des propriétés JSON inconnues) - inutile de les déclarer ici.
public sealed record ExamRosterFile(
    [property: JsonConverter(typeof(RosterStudentsConverter))] List<ExamRosterStudent> Etudiants);

public sealed record ExamRosterStudent(string Nom, string Id);

// Accepte "etudiants" sous deux formes vues dans des exports d'école réels : un tableau
// (`[{ "nom": ..., "id": ... }, ...]`, format documenté) ou un objet indexé par une clé libre non
// exploitée (typiquement l'email : `{ "email@ecole.fr": { "nom": ..., "id": ... }, ... }`) - seules
// les valeurs comptent, la clé de chaque entrée est ignorée.
internal sealed class RosterStudentsConverter : JsonConverter<List<ExamRosterStudent>>
{
    public override List<ExamRosterStudent> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.Array => root.Deserialize<List<ExamRosterStudent>>(options) ?? [],
            JsonValueKind.Object => root.Deserialize<Dictionary<string, ExamRosterStudent>>(options)?
                .Values.ToList() ?? [],
            _ => throw new JsonException("Le champ 'etudiants' doit être un tableau ou un objet d'étudiants."),
        };
    }

    public override void Write(Utf8JsonWriter writer, List<ExamRosterStudent> value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}
