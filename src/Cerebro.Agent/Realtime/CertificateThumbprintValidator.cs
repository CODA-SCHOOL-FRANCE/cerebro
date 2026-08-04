namespace Cerebro.Agent.Realtime;

// Le certificat du serveur est auto-signé (réseau d'examen isolé, pas de CA publique) : plutôt que
// de désactiver toute validation TLS, on épingle le certificat attendu par son empreinte SHA-256.
// Un certificat différent (ex: MITM) est rejeté même s'il est par ailleurs valide.
public static class CertificateThumbprintValidator
{
    public static bool IsMatch(string actualThumbprint, string expectedThumbprint)
        => Normalize(actualThumbprint) == Normalize(expectedThumbprint);

    private static string Normalize(string thumbprint)
        => thumbprint.Replace(":", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Trim()
            .ToUpperInvariant();
}