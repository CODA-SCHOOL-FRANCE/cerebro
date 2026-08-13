# Cerebro

Outil anti-fraude pour la surveillance d'épreuves certifiantes à distance (BYOD, multi-OS).
Chaque candidat lance un agent léger (nommé **Xavier** une fois publié) sur sa propre machine ; l'agent capture des screenshots à intervalles aléatoires et les transmet en temps réel à un serveur central, consulté par le surveillant via un dashboard web.

![Cerebro by Charles Thirion](img/cerebro.webp)

## Prérequis

- [.NET SDK 10.0+](https://dotnet.microsoft.com/download)
- `Node.js` / `npm`
- Pour le déploiement multi-OS de l'agent : aucune dépendance supplémentaire (publication self-contained, voir [Déploiement pour un examen](DEPLOYMENT.md))

## Structure du dépôt

```
Cerebro.sln
src/
  Cerebro.Agent/       # agent candidat (console app), publié sous le nom "xavier"
    Capture/           # IScreenCapturer + implémentations Windows/macOS/Linux
    Realtime/          # client SignalR (ICerebroConnection)
    AgentRunner.cs     # boucle métier (self-test, intervalle aléatoire, reporting)
  Cerebro.Server/      # serveur (ASP.NET Core + SignalR)
    Hubs/CerebroHub.cs
    Services/          # SessionRegistry (état en mémoire), ScreenshotStore (disque)
    Data/              # IExamRepository/SqliteExamRepository (Dapper) : sessions + candidats enregistrés
                       # ISessionActivityStore/FileSessionActivityStore : journal d'activité (texte, screenshots/{session}/activity.log)
    Admin/             # AdminCli (`provision`/`start`, ConsoleAppFramework) + ExamRoster (format du roster de l'école)
    Telemetry/         # CerebroTelemetry (ActivitySource + Meter OpenTelemetry), SessionActivityEventType
    wwwroot/           # dashboard (index.html + app.js + client SignalR vendoré)
  Cerebro.Shared/      # contrats communs (DTOs, Result<byte[], CaptureError> pour la capture) partagés agent/serveur
tests/
  Cerebro.Tests/
    Unit/              # logique pure, sans dépendance OS
    Integration/        # capture réelle, disque réel, SignalR réel de bout en bout
```

## Développement local

```bash
# Compiler toute la solution
dotnet build

# Créer un roster de test minimal (même format que l'export officiel de l'école)
cat > roster-test.json << 'EOF'
{
  "ec": "TEST",
  "date": "2026-01-01",
  "rattrapage": false,
  "etudiants": {
    "test@example.com": { "nom": "Test Candidat", "id": "CAND0001", "promo": "B1" }
  },
  "diplome": "TEST"
}
EOF

# Provisionner une session à partir de ce fichier (enregistre chaque candidat du roster en base)
dotnet run --project src/Cerebro.Server -- provision --session SESSION-TEST --input roster-test.json

# Lancer le serveur (dashboard sur l'URL affichée, ex: http://localhost:5204)
dotnet run --project src/Cerebro.Server

# Dans un autre terminal, lancer l'agent (identifiant candidat = CAND0001, le sien, déjà connu de lui)
dotnet run --project src/Cerebro.Agent -- http://localhost:5204 SESSION-TEST CAND0001
```

Ouvrir le dashboard dans un navigateur, entrer le code de session (`SESSION-TEST` dans l'exemple),
cliquer sur "Rejoindre la session".

## Tests

```bash
dotnet test                              # tout
dotnet test --filter "Category=Unit"        # logique pure, rapide
dotnet test --filter "Category=Integration"  # capture réelle, disque réel, SignalR réel
```

Pour un test manuel de bout en bout (serveur + plusieurs agents, dashboard, scénarios d'erreur), voir [TESTING.md](../TESTING.md).

## Autres documentations
- [Architecture](docs/ARCHITECTURE.md)
- [Fonctionnalités supportées](docs/FEATURES.md)
- [Limites connues](docs/LIMITATIONS.md)
- [Déploiement](docs/DEPLOYMENT.md)
- [Documentation candidat](docs/USER-DOC.txt)
- [Protocole de test manuel de bout en bout](TESTING.md) 