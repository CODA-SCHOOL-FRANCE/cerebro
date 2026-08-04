# Architecture

### Conteneurs (C4 — niveau 2)

```mermaid
C4Container
    title Cerebro — Diagramme de conteneurs

    Person(candidat, "Candidat", "Étudiant passant l'épreuve sur sa propre machine (Windows/macOS/Linux)")
    Person(surveillant, "Surveillant", "Supervise l'examen et valide le démarrage")

    System_Boundary(cerebro, "Cerebro") {
        Container(agent, "Cerebro.Agent", ".NET, exécutable self-contained", "Capture l'écran à intervalles aléatoires, s'auto-teste à la connexion")
        Container(server, "Cerebro.Server", "ASP.NET Core + SignalR Hub", "Maintient l'état des sessions et diffuse les mises à jour en temps réel")
        Container(dashboard, "Dashboard", "HTML/TypeScript + client SignalR", "Interface temps réel consultée par le surveillant")
        ContainerDb(storage, "Stockage screenshots", "Système de fichiers", "Screenshots persistés par session/candidat")
    }

    Rel(candidat, agent, "Lance et exécute")
    Rel(agent, server, "JoinAsCandidate, Ping, ReportReadiness, UploadScreenshot", "SignalR / WebSocket")
    Rel(server, storage, "Écrit chaque screenshot reçu")
    Rel(server, dashboard, "CandidateJoined, CandidateHeartbeat, CandidateReadinessUpdated, ScreenshotReceived, CandidateDisconnected", "SignalR / WebSocket")
    Rel(surveillant, dashboard, "Sélectionne une épreuve planifiée, démarre/arrête la session", "Navigateur")
```

### Composants de `Cerebro.Server` (C4 — niveau 3)

```mermaid
C4Component
    title Cerebro.Server — Diagramme de composants

    Container(agent, "Cerebro.Agent", ".NET", "Agent candidat")
    Container(dashboard, "Dashboard", "HTML/TypeScript", "Interface surveillant")

    Container_Boundary(server, "Cerebro.Server") {
        Component(adminCli, "AdminCli", "Mode CLI (ConsoleAppFramework)", "Parse le roster JSON de l'école, enregistre chaque candidat en base")
        Component(hub, "CerebroHub", "SignalR Hub", "JoinAsCandidate, JoinAsDashboard, GetPlannedSessions, StartSession, StopSession, Ping, ReportReadiness, UploadScreenshot, GetSnapshot")
        Component(registry, "SessionRegistry", "Service singleton (ConcurrentDictionary)", "État en mémoire des candidats par session : statut, dernière capture, connexion")
        Component(screenshotStore, "ScreenshotStore", "Service singleton", "Persiste les screenshots, assainit les identifiants contre le path traversal")
        Component(examRepository, "SqliteExamRepository", "Service singleton (Dapper)", "Sessions/candidats enregistrés persistés")
        Component(activityStore, "SqliteSessionActivityStore", "Service singleton (Dapper)", "Journal d'activité par session (OpenTelemetry)")
        Component(credentialsStore, "SqliteDashboardCredentialsStore", "Service singleton (Dapper)", "Identifiants du dashboard (cookie auth), alimentés par 'set-password'")
    }

    ContainerDb(disk, "Disque", "Système de fichiers", "screenshots/{session}/{candidat}/*.png")
    ContainerDb(sqlite, "cerebro.db", "SQLite", "ExamSessions, Candidates (id du roster), SessionActivityEvents, DashboardCredentials")

    Rel(agent, hub, "Invoque les méthodes du hub (sans authentification, validé par code de session + id candidat)", "SignalR")
    Rel(dashboard, hub, "Invoque / reçoit les évènements (méthodes réservées au dashboard protégées par [Authorize])", "SignalR")
    Rel(hub, registry, "Lit/écrit l'état des candidats")
    Rel(hub, screenshotStore, "Sauvegarde chaque screenshot reçu")
    Rel(hub, examRepository, "Vérifie que le candidat est enregistré")
    Rel(hub, activityStore, "Journalise chaque évènement métier")
    Rel(dashboard, credentialsStore, "/account/login, /account/logout (cookie de session)", "HTTP")
    Rel(adminCli, examRepository, "Crée la session, enregistre chaque candidat du roster")
    Rel(adminCli, credentialsStore, "set-password : définit le mot de passe du dashboard")
    Rel(screenshotStore, disk, "Écrit les fichiers PNG")
    Rel(examRepository, sqlite, "Lit/écrit")
    Rel(activityStore, sqlite, "Lit/écrit")
    Rel(credentialsStore, sqlite, "Lit/écrit")
```

- **`Cerebro.Agent`** — console app .NET, un binaire par OS. Capture l'écran, s'auto-teste à la
  connexion, puis boucle à intervalle aléatoire.
- **`Cerebro.Server`** — ASP.NET Core + SignalR Hub. Reçoit les screenshots, maintient l'état des
  candidats par session, sert le dashboard temps réel. Inclut aussi un mode admin en CLI
  (`provision`/`start`, via [ConsoleAppFramework](https://github.com/Cysharp/ConsoleAppFramework))
  et la persistance des sessions/candidats en SQLite. Le dashboard (`wwwroot/ts/*.ts`) est écrit en
  TypeScript et compilé en JavaScript brut (`wwwroot/js/`, non committé, servi tel quel sans
  bundler). La compilation est automatique : `dotnet build`/`dotnet run` recompile les `.ts`
  modifiés avant chaque build (cible MSBuild `BuildDashboardTypeScript`). **Prérequis unique** :
  lancer `npm install` une fois depuis `src/Cerebro.Server/` (nécessite Node.js) — sans ça,
  `wwwroot/js/` n'existe pas et le dashboard ne se charge pas dans le navigateur.
- **`Cerebro.Shared`** — contrats communs (DTOs, résultats de capture) partagés entre agent et
  serveur.
- **`Cerebro.Tests`** — xUnit + NFluent, tests Unit (logique pure) et Integration (capture d'écran
  réelle, écriture disque réelle, aller-retour SignalR réel via `WebApplicationFactory`).
