using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Text.Json.JsonSerializer;

namespace Cerebro.Agent.Configuration;

public sealed record AgentConfigFile(
    [property: JsonPropertyName("serverUrl")] string? ServerUrl,
    [property: JsonPropertyName("certThumbprint")] string? CertThumbprint)
{
    public const string FileName = "cerebro-agent.config.json";

    // Noms de propriétés explicites ci-dessus : le build Release obfusque aussi l'API publique
    // (voir obfuscar.xml, KeepPublicApi=false) et renomme ServerUrl/CertThumbprint. Sans
    // [JsonPropertyName], System.Text.Json (réflexif) ne retrouve alors plus la correspondance
    // avec les clés JSON du fichier écrit par le surveillant, et laisse tout à null en silence.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AgentConfigFile? Load(string directory)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return Deserialize<AgentConfigFile>(
                File.ReadAllText(path),
                JsonOptions
            );
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Le fichier {FileName} est invalide ({ex.Message}). Vérifiez sa syntaxe JSON.", ex);
        }
    }
}