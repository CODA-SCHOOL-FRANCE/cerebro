# Cerebro

[![Release Xavier](https://img.shields.io/github/v/release/CODA-SCHOOL-FRANCE/xavier-releases?label=xavier&color=blue)](https://github.com/CODA-SCHOOL-FRANCE/xavier-releases/releases)
[![npm](https://img.shields.io/npm/v/xavier-agent?label=npm&logo=npm&logoColor=white)](https://www.npmjs.com/package/xavier-agent)
[![Homebrew](https://img.shields.io/badge/homebrew-brew%20install-fbb040?logo=homebrew&logoColor=white)](https://github.com/CODA-SCHOOL-FRANCE/homebrew-cerebro)
[![Scoop](https://img.shields.io/badge/scoop-scoop%20install-blue?logo=powershell&logoColor=white)](https://github.com/CODA-SCHOOL-FRANCE/xavier-releases/tree/main/bucket)

Outil anti-fraude pour la surveillance d'épreuves à distance (BYOD, multi-OS).
Chaque candidat lance un agent léger (nommé **Xavier** une fois publié) sur sa propre machine ; l'agent capture des screenshots à intervalles aléatoires et les transmet en temps réel à un serveur central, consulté par le surveillant via un dashboard web.

![Cerebro by Charles Thirion](img/cerebro.webp)

## Prérequis

- [.NET SDK 10.0+](https://dotnet.microsoft.com/download)
- `Node.js` / `npm`
- Pour le déploiement multi-OS de l'agent : aucune dépendance supplémentaire (publication self-contained, voir [Déploiement pour une épreuve](docs/DEPLOYMENT.md))
- Pour l'installation de l'agent côté étudiant (npm/Homebrew/Scoop/script) : voir [Déploiement §2](docs/DEPLOYMENT.md#2-agent-xavier-distribution-multi-canal)

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
tools/
  Cerebro.LoadSim/     # simule une session avec N candidats depuis une seule machine (voir son README)
packaging/
  npm/                 # wrapper npm "xavier-agent" (télécharge le binaire natif au postinstall, voir son README)
```

## Développement local

```bash
# Compiler toute la solution
dotnet build

# Créer un roster de test minimal
cat > roster-test.json << 'EOF'
{
  "etudiants": [
    { "nom": "Test Candidat", "id": "CAND0001" }
  ]
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

Pour un test manuel de bout en bout (serveur + plusieurs agents, dashboard, scénarios d'erreur), voir [TESTING.md](TESTING.md).

## Autres documentations
- [Architecture](docs/ARCHITECTURE.md)
- [Fonctionnalités supportées](docs/FEATURES.md)
- [Déploiement](docs/DEPLOYMENT.md)
- [Documentation candidat](docs/USER-DOC.txt)
- [Protocole de test manuel de bout en bout](TESTING.md) 