using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Cerebro.Server.Tls;

// Kestrel termine le TLS lui-même (plus de reverse proxy devant, voir Program.cs) : le certificat
// auto-signé qu'exigeait auparavant Caddy ("tls internal") est généré ici, une seule fois, puis
// persisté sur disque - dans le même volume nommé que la base SQLite (voir deploy/docker-compose.yml)
// pour survivre aux redéploiements sans faire changer l'empreinte SHA-256 déjà communiquée aux
// candidats.
public static class ServerCertificateProvisioner
{
    // 5 ans, comme le "lifetime 43800h" de l'ancien Caddyfile : le certificat est épinglé par
    // empreinte côté agents (pas par la chaîne de confiance du système), un renouvellement
    // automatique fréquent n'apporterait rien et casserait cet épinglage entre deux épreuves.
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(5 * 365);

    // Charge le certificat existant à ce chemin, ou en génère un nouveau et l'y écrit si absent.
    // Idempotent d'un redémarrage à l'autre : la génération n'a lieu qu'une fois par volume.
    public static X509Certificate2 EnsureCertificate(string pfxPath, string address)
    {
        if (File.Exists(pfxPath))
        {
            return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password: null);
        }

        return GenerateAndSave(pfxPath, address);
    }

    public static X509Certificate2 GenerateAndSave(string pfxPath, string address)
    {
        var certificate = Generate(address);

        var directory = Path.GetDirectoryName(pfxPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Réexporté puis rechargé depuis le fichier (plutôt que renvoyé tel quel) : la clé privée
        // d'un certificat fraîchement créé par CreateSelfSigned n'est pas garantie persistée par le
        // provider crypto sous-jacent - recharger depuis le .pfx écrit sur disque est la façon la
        // plus fiable d'obtenir une instance utilisable par Kestrel sur toutes les plateformes.
        File.WriteAllBytes(pfxPath, certificate.Export(X509ContentType.Pfx));
        return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password: null);
    }

    private static X509Certificate2 Generate(string address)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Cerebro Server",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false)); // Server Authentication

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        if (IPAddress.TryParse(address, out var ip))
        {
            sanBuilder.AddIpAddress(ip);
        }
        else if (!string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            sanBuilder.AddDnsName(address);
        }
        request.CertificateExtensions.Add(sanBuilder.Build());

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        return request.CreateSelfSigned(notBefore, notBefore + Lifetime);
    }

    public static string Sha256Thumbprint(X509Certificate2 certificate) =>
        Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256));
}
