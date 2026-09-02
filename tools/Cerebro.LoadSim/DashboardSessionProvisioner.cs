using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cerebro.Agent.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cerebro.LoadSim;

// Réplique côté client ce que fait le bouton "+ NOUVELLE SESSION" du dashboard : login par cookie
// puis appel du hub CerebroHub.CreateSession. Le simulateur n'a donc besoin que des identifiants
// dashboard de l'instance ciblée, pas d'un accès direct à sa base (utile pour simuler contre un
// serveur distant déjà déployé, pas seulement en local).
internal static class DashboardSessionProvisioner
{
    public static async Task<int> CreateSessionAsync(
        string serverUrl,
        string username,
        string password,
        string? certThumbprint,
        string sessionCode,
        string rosterJson,
        CancellationToken cancellationToken)
    {
        using var loginHandler = CreateHandler(certThumbprint);
        using var httpClient = new HttpClient(loginHandler) { BaseAddress = new Uri(serverUrl) };

        var loginBody = JsonSerializer.Serialize(new { Username = username, Password = password });
        using var loginResponse = await httpClient.PostAsync(
            "/account/login",
            new StringContent(loginBody, Encoding.UTF8, "application/json"),
            cancellationToken);

        if (!loginResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Login dashboard échoué ({(int)loginResponse.StatusCode}) — identifiants incorrects ?");
        }

        var cookies = new CookieContainer();
        if (loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (var header in setCookieHeaders)
            {
                cookies.SetCookies(new Uri(serverUrl), header);
            }
        }

        await using var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(serverUrl), "hubs/cerebro"), options =>
            {
                // options.Cookies seul ne suffit pas ici (vérifié empiriquement contre un vrai
                // déploiement HTTPS : le cookie n'était jamais transmis, CreateSession
                // échouait avec "user is unauthorized") - on le rattache donc à la main sur chaque
                // requête via CookieForwardingHandler. Transport forcé en long polling : cette
                // connexion ne sert qu'à un seul appel ponctuel (CreateSession), la latence
                // supplémentaire du long polling n'a aucun impact ici.
                options.Cookies = cookies;
                options.HttpMessageHandlerFactory = _ =>
                    new CookieForwardingHandler(CreateHandler(certThumbprint), cookies);
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await hubConnection.StartAsync(cancellationToken);
        try
        {
            return await hubConnection.InvokeAsync<int>(
                "CreateSession", sessionCode, rosterJson, cancellationToken);
        }
        finally
        {
            await hubConnection.StopAsync(CancellationToken.None);
        }
    }

    // Même logique d'épinglage que SignalRCerebroConnection (voir CertificateThumbprintValidator) :
    // certificat auto-signé accepté seulement s'il correspond à l'empreinte attendue, sinon
    // validation TLS standard du système.
    private static HttpClientHandler CreateHandler(string? certThumbprint)
    {
        if (string.IsNullOrWhiteSpace(certThumbprint))
        {
            return new HttpClientHandler();
        }

        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null &&
                CertificateThumbprintValidator.IsMatch(
                    certificate.GetCertHashString(HashAlgorithmName.SHA256), certThumbprint)
        };
    }

    private sealed class CookieForwardingHandler(HttpMessageHandler inner, CookieContainer cookies)
        : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cookieHeader = cookies.GetCookieHeader(request.RequestUri!);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
