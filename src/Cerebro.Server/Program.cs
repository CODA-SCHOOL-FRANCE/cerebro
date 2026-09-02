using System.Security.Claims;
using Cerebro.Server.Admin;
using Cerebro.Server.Auth;
using Cerebro.Server.Data;
using Cerebro.Server.Hubs;
using Cerebro.Server.Services;
using Cerebro.Server.Telemetry;
using Cerebro.Server.Tls;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Mode admin (provisioning hors ligne, n'a pas besoin d'héberger le serveur web) :
// `dotnet Cerebro.Server.dll provision --session ... --candidates ...`, `start --session ...`,
// `set-password --username ...` ou `generate-cert --address ...`.
if (args.Length > 0 && args[0] is "provision" or "start" or "set-password" or "generate-cert")
{
    return await AdminCli.RunAsync(args);
}

var builder = WebApplication.CreateBuilder(args);

// Kestrel termine le TLS lui-même, mais seulement si CEREBRO_SERVER_TLS_PORT est défini (voir
// src/Cerebro.Server/Dockerfile - c'est le seul endroit qui la positionne, à 8443). Sans elle -
// notamment un simple `dotnet run` en développement local, sans conteneur - Kestrel garde son
// comportement par défaut (port de dev habituel, HTTP). Certificat auto-signé généré une seule
// fois puis persisté dans le même volume nommé que la base SQLite (db/cerebro.pfx par défaut),
// pour ne pas faire changer l'empreinte communiquée aux candidats à chaque redéploiement.
var tlsPortValue = builder.Configuration["CEREBRO_SERVER_TLS_PORT"];
if (!string.IsNullOrEmpty(tlsPortValue))
{
    var tlsPort = int.Parse(tlsPortValue);
    var certPath = builder.Configuration["Tls:CertificatePath"] ?? "db/cerebro.pfx";
    var certAddress = builder.Configuration["CEREBRO_SERVER_ADDRESS"] ?? "localhost";
    var certificate = ServerCertificateProvisioner.EnsureCertificate(certPath, certAddress);

    Console.WriteLine("==================================================================");
    Console.WriteLine(" Empreinte SHA-256 du certificat serveur (CEREBRO_SERVER_CERT_THUMBPRINT) :");
    Console.WriteLine($" {ServerCertificateProvisioner.Sha256Thumbprint(certificate)}");
    Console.WriteLine(" À communiquer aux agents candidats en même temps que l'URL et le code de session.");
    Console.WriteLine("==================================================================");

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(tlsPort, listenOptions => listenOptions.UseHttps(certificate));
    });
}

builder.Services.AddSignalR(options =>
{
    // Un screenshot plein écran (multi-moniteur, retina) peut dépasser la limite par défaut
    // de SignalR (32 Ko) largement conçue pour de petits messages JSON.
    options.MaximumReceiveMessageSize = 50 * 1024 * 1024;
});

builder.Services.AddSingleton<ISessionRegistry, SessionRegistry>();
builder.Services.AddSingleton<IScreenshotStore, ScreenshotStore>();

// Résolution paresseuse (via IServiceProvider, pas builder.Configuration lu ici) : WebApplicationFactory
// (tests d'intégration) injecte sa propre chaîne de connexion après ce point du top-level Program.cs -
// la lire trop tôt fait retomber les tests sur la config par défaut au lieu de leur base temporaire isolée.
builder.Services.AddSingleton<IExamRepository>(sp =>
    new SqliteExamRepository(GetConnectionString(sp.GetRequiredService<IConfiguration>())));
builder.Services.AddSingleton<ISessionActivityStore, FileSessionActivityStore>();
builder.Services.AddSingleton<IDashboardCredentialsStore>(sp =>
    new SqliteDashboardCredentialsStore(GetConnectionString(sp.GetRequiredService<IConfiguration>())));

static string GetConnectionString(IConfiguration configuration) =>
    configuration.GetConnectionString("CerebroDb") ?? "Data Source=db/cerebro.db";

// Accès au dashboard protégé par identifiant/mot de passe (cookie de session) : voir
// Admin/AdminCommands.SetPassword pour définir les identifiants. Les agents candidats ne
// s'authentifient jamais ici - ils restent validés par code de session + id candidat (voir
// CerebroHub.JoinAsCandidate), seules les méthodes du hub réservées au dashboard portent [Authorize].
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login.html";
        options.Cookie.Name = "CerebroDashboardAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            // La négociation SignalR, l'API de login et les téléchargements (zip de screenshots)
            // sont des appels "API" : une redirection HTML n'a pas de sens pour eux (on ne veut
            // surtout pas qu'un clic non authentifié télécharge une page de connexion nommée
            // "*.zip"), un 401 est ce qu'attend le client.
            if (context.Request.Path.StartsWithSegments("/hubs") ||
                context.Request.Path.StartsWithSegments("/account") ||
                context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// Traces/métriques Cerebro exportées en console par défaut (visibles sur le terminal du serveur) ;
// le journal persisté en fichier texte (ISessionActivityStore, screenshots/{session}/activity.log,
// exposé au dashboard via GetSessionActivity) reste la source principale pour consulter "qui a fait
// quoi" - voir Telemetry/CerebroTelemetry.cs.
// Pour brancher un vrai backend (Seq, Grafana/Tempo...), remplacer AddConsoleExporter() par
// AddOtlpExporter() sans toucher au code d'instrumentation (Hub, spans, compteurs).
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "Cerebro.Server"))
    .WithTracing(tracing => tracing
        .AddSource(CerebroTelemetry.SourceName)
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(CerebroTelemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/account/login",
    async (LoginRequest request, IDashboardCredentialsStore credentials, HttpContext http, CancellationToken ct) =>
    {
        if (!await credentials.ValidateAsync(request.Username, request.Password, ct))
        {
            return Results.Unauthorized();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, request.Username)], CookieAuthenticationDefaults.AuthenticationScheme);

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Results.Ok();
    });

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
});

// Navigation directe du navigateur (<a href>, pas un appel SignalR) : le zip est streamé
// directement dans la réponse HTTP, sans être bufferisé en mémoire côté serveur (voir
// ScreenshotStore.WriteZipAsync). Contient les screenshots ET le journal d'activité de la session
// (screenshots/{session}/activity.log, voir FileSessionActivityStore) - export complet en un clic.
// RequireAuthorization() suffit à protéger l'accès ; voir OnRedirectToLogin plus haut pour le 401
// (au lieu d'une redirection HTML) si la session cookie a expiré entre-temps.
app.MapGet("/api/sessions/{sessionCode}/export.zip",
    async (string sessionCode, IExamRepository examRepository, IScreenshotStore screenshotStore,
        CancellationToken ct) =>
    {
        if (!await examRepository.SessionExistsAsync(sessionCode, ct))
        {
            return Results.NotFound();
        }

        if (!screenshotStore.HasExportableContent(sessionCode))
        {
            return Results.NotFound();
        }

        return Results.Stream(
            stream => screenshotStore.WriteZipAsync(sessionCode, stream, ct),
            contentType: "application/zip",
            fileDownloadName: $"{sessionCode}.zip");
    }).RequireAuthorization();

// index.html vit hors de wwwroot (Dashboard/) précisément pour ne jamais être servi par
// UseStaticFiles sans passer par cette route protégée.
app.MapGet("/", ServeDashboardAsync).RequireAuthorization();
app.MapGet("/index.html", ServeDashboardAsync).RequireAuthorization();

app.MapHub<CerebroHub>("/hubs/cerebro");

using (var scope = app.Services.CreateScope())
{
    var credentials = scope.ServiceProvider.GetRequiredService<IDashboardCredentialsStore>();
    if (!await credentials.HasCredentialsAsync(CancellationToken.None))
    {
        app.Logger.LogWarning(
            "Aucun mot de passe défini pour le dashboard. Exécutez 'set-password --username <nom>' avant l'épreuve.");
    }
}

await app.RunAsync();
return 0;

static IResult ServeDashboardAsync(HttpContext http, IWebHostEnvironment env)
{
    // Sans ça, le navigateur peut resservir cette page protégée depuis son cache après une
    // déconnexion (logout) sans jamais retourner au serveur revérifier le cookie de session.
    http.Response.Headers.CacheControl = "no-store";
    return Results.File(Path.Combine(env.ContentRootPath, "Dashboard", "index.html"), "text/html; charset=utf-8");
}

// Rend le point d'entrée accessible à WebApplicationFactory<Program> pour les tests d'intégration.
public partial class Program;