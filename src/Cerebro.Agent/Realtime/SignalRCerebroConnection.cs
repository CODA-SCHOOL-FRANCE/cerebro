using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Cerebro.Shared.Capture;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cerebro.Agent.Realtime;

public sealed class SignalRCerebroConnection : ICerebroConnection
{
    private readonly HubConnection _connection;
    public event Func<Task>? Reconnected;

    // expectedServerCertificateThumbprint : à renseigner quand le serveur est derrière un reverse proxy
    // TLS avec certificat auto-signé (cas normal sur un réseau d'examen isolé, sans CA publique).
    // Laissé à null, la validation TLS standard du système s'applique (HTTP simple ou certificat reconnu).
    public SignalRCerebroConnection(string serverUrl, string? expectedServerCertificateThumbprint = null)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(serverUrl), "hubs/cerebro"), options =>
            {
                if (!string.IsNullOrWhiteSpace(expectedServerCertificateThumbprint))
                {
                    options.HttpMessageHandlerFactory = _ =>
                        CreatePinnedCertificateHandler(expectedServerCertificateThumbprint);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.Reconnected += _ => Reconnected is null ? Task.CompletedTask : Reconnected.Invoke();
    }

    private static HttpMessageHandler CreatePinnedCertificateHandler(string expectedThumbprint)
        => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                IsValidCertificate(expectedThumbprint, certificate)
        };

    private static bool IsValidCertificate(string expectedThumbprint, X509Certificate2? certificate)
        => certificate is not null &&
           CertificateThumbprintValidator
               .IsMatch(certificate.GetCertHashString(HashAlgorithmName.SHA256), expectedThumbprint);

    public Task ConnectAsync(CancellationToken cancellationToken)
        => _connection.StartAsync(cancellationToken);

    public Task JoinAsync(string sessionCode, string candidateId, CancellationToken cancellationToken)
        => _connection.InvokeAsync("JoinAsCandidate", sessionCode, candidateId, cancellationToken);

    public Task PingAsync(CancellationToken cancellationToken)
        => _connection.InvokeAsync("Ping", cancellationToken);

    public Task ReportReadinessAsync(
        bool isReady,
        CaptureFailureReason? failureReason,
        string? failureDetail,
        CancellationToken cancellationToken)
        => _connection.InvokeAsync("ReportReadiness", isReady, failureReason, failureDetail, cancellationToken);

    public Task UploadScreenshotAsync(byte[] pngBytes, CancellationToken cancellationToken) 
        => _connection.InvokeAsync("UploadScreenshot", pngBytes, cancellationToken);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}