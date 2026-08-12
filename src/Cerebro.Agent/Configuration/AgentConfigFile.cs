using System.Text.Json;
using static System.Text.Json.JsonSerializer;

namespace Cerebro.Agent.Configuration;

public sealed record AgentConfigFile(string? ServerUrl, string? CertThumbprint)
{
    public const string FileName = "cerebro-agent.config.json";

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