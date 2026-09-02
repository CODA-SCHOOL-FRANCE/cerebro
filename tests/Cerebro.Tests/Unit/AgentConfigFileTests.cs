using Cerebro.Agent.Configuration;
using NFluent;
using NFluent.ApiChecks;

namespace Cerebro.Tests.Unit;

[Trait("Category", "Unit")]
public class AgentConfigFileTests
{
    private readonly string _directory = CreateTempDirectory();

    [Fact]
    public void Load_ShouldReturnNull_WhenFileDoesNotExist()
        => Check.That(AgentConfigFile.Load(_directory)).IsNull();

    [Fact]
    public void Load_ShouldReadServerUrlAndCertThumbprint_WhenFileExists()
    {
        File.WriteAllText(
            Path.Combine(_directory, AgentConfigFile.FileName),
            """
            {
              "serverUrl": "https://192.168.1.10:8443",
              "certThumbprint": "A1B2C3D4E5F60718293A4B5C6D7E8F901234567"
            }
            """);

        var result = AgentConfigFile.Load(_directory);

        Check.That(result).IsNotNull();
        Check.That(result!.ServerUrl).IsEqualTo("https://192.168.1.10:8443");
        Check.That(result.CertThumbprint).IsEqualTo("A1B2C3D4E5F60718293A4B5C6D7E8F901234567");
    }

    [Fact]
    public void Load_ShouldReturnNullFields_WhenValuesAreJsonNull()
    {
        // C'est le contenu par défaut déployé par tous les canaux d'installation (docs/xavier.config.json) :
        // des champs à null, pas des placeholders factices - Program.cs retombe alors sur les prompts
        // interactifs exactement comme en l'absence de fichier (voir `serverUrl ??= configFile?.ServerUrl`).
        File.WriteAllText(
            Path.Combine(_directory, AgentConfigFile.FileName),
            """
            {
              "serverUrl": null,
              "certThumbprint": null
            }
            """);

        var result = AgentConfigFile.Load(_directory);

        Check.That(result).IsNotNull();
        Check.That(result!.ServerUrl).IsNull();
        Check.That(result.CertThumbprint).IsNull();
    }

    [Fact]
    public void Load_ShouldThrowWithClearMessage_WhenFileIsNotValidJson()
    {
        File.WriteAllText(Path.Combine(_directory, AgentConfigFile.FileName), "not json");

        Check.ThatCode(() => AgentConfigFile.Load(_directory))
            .Throws<InvalidOperationException>()
            .AndWhichMessage()
            .Contains(AgentConfigFile.FileName);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cerebro-agent-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        return directory;
    }
}