# Scénario de test — Cerebro

Ce document décrit un scénario de test manuel de bout en bout pour valider une session d'examen
complète sur une seule machine (un serveur + deux agents simulant des candidats), ainsi que les
principaux scénarios d'erreur attendus. Il complète les tests automatisés (`dotnet test`, voir
[Tests](docs/DEVELOPMENT.md#tests)) : ceux-ci valident chaque composant isolément, ce document valide
le parcours complet tel que le vivrait un surveillant le jour J.

## Prérequis

- [.NET SDK 10.0+](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (pour compiler le dashboard TypeScript) — lancer une fois
  `npm install` depuis `src/Cerebro.Server/` ; `dotnet build` recompile ensuite automatiquement
  le dashboard à chaque changement (voir [README](README.md))
- Le dépôt compile : `dotnet build` sans erreur
- Un terminal libre par processus à lancer (serveur, agent candidat 1, agent candidat 2)

## Scénario nominal

### 1. Build

```bash
cd /chemin/vers/le/dépôt
dotnet build
```

**Attendu** : `Build succeeded`, 0 erreur.

### 2. Créer un roster de test

```bash
mkdir -p /tmp/cerebro-test
cat > /tmp/cerebro-test/roster.json << 'EOF'
{
  "ec": "E01",
  "date": "2026-08-03",
  "rattrapage": false,
  "etudiants": {
    "etudiant1@ecole.fr": { "nom": "Test Un", "id": "AAAA1111", "promo": "B1" },
    "etudiant2@ecole.fr": { "nom": "Test Deux", "id": "BBBB2222", "promo": "B1" }
  },
  "correcteurs": [{ "nom": "Surveillant Test", "email": "surveillant@ecole.fr" }],
  "diplome": "TEST"
}
EOF
```

### 3. Provisionner la session

```bash
dotnet run --project src/Cerebro.Server -- provision \
  --session TEST-2026 \
  --input /tmp/cerebro-test/roster.json
```

Sans `--db`, la commande utilise le défaut `db/cerebro.db`, résolu relativement au dossier du
projet serveur (`dotnet run --project` s'exécute avec ce dossier comme répertoire courant) — le
fichier atterrit donc dans `src/Cerebro.Server/db/cerebro.db`, à côté de `wwwroot`, et est créé
automatiquement s'il n'existe pas encore.

**Attendu** :
- Chaque candidat listé sur sa propre ligne (`Test Un <etudiant1@ecole.fr> (AAAA1111)`, idem pour Test Deux)
- `Session 'TEST-2026' provisionnée avec 2 candidat(s).`
- Code de sortie `0`
- Aucun fichier généré à côté du roster (celui-ci n'est utilisé que pour peupler `db/cerebro.db`)

### 4. Définir le mot de passe du dashboard

```bash
dotnet run --project src/Cerebro.Server -- set-password --username surveillant
```

Saisie interactive et masquée (aucun echo, ni dans le terminal ni dans l'historique shell) :
tape le même mot de passe deux fois quand demandé.

**Attendu** : `Mot de passe défini pour 'surveillant'.`, code de sortie `0`.

### 5. Démarrer le serveur

Dans un premier terminal :

```bash
dotnet run --project src/Cerebro.Server --urls http://127.0.0.1:5289
```

Sans configuration explicite, le serveur pointe vers ce même `db/cerebro.db` par défaut — pas
besoin de variable d'environnement pour ce scénario.

**Attendu** : `Now listening on: http://127.0.0.1:5289`, aucune exception au démarrage.

### 6. Se connecter et sélectionner l'épreuve

Dans un navigateur : `http://127.0.0.1:5289/index.html`.

**Attendu** :
- Redirection automatique vers `/login.html` (pas encore de session ouverte)
- Se connecter avec `surveillant` / le mot de passe défini à l'étape 4 → retour sur le dashboard
- La section "Épreuves planifiées" liste `TEST-2026` avec 2 candidats et le statut "Planifiée (non démarrée)"
- Cliquer sur **Sélectionner** bascule vers la vue de suivi de la session (liste des candidats vide pour l'instant, boutons Démarrer/Arrêter/Journal d'activité visibles)
- Le bouton **⏻ DÉCONNEXION** (en haut à droite) ramène à l'écran de connexion et bloque à nouveau l'accès à `/index.html` tant qu'on ne s'est pas reconnecté

### 6. Lancer les deux agents candidats

Dans un deuxième terminal :

```bash
dotnet run --project src/Cerebro.Agent -- http://127.0.0.1:5289 TEST-2026 AAAA1111
```

Dans un troisième terminal :

```bash
dotnet run --project src/Cerebro.Agent -- http://127.0.0.1:5289 TEST-2026 BBBB2222
```

**Attendu** (dans le dashboard, sans rien recharger) :
- Les deux candidats apparaissent avec la colonne **Connexion = Connecté**
- Après quelques secondes, la colonne **Statut** passe à **Prêt** (capture de test réussie) — un statut
  **Échec** avec une raison est possible si aucun outil de capture n'est disponible sur la machine de test
- La colonne **Dernier vu** se met à jour automatiquement (le ping des agents est envoyé toutes les 60s)

### 7. Démarrer l'épreuve

Dans le dashboard, cliquer sur **Démarrer l'épreuve**.

**Attendu** :
- Le statut de session affiché passe à `Démarrée à HH:MM:SS`
- Le bouton **Démarrer l'épreuve** devient grisé, **Arrêter l'épreuve** devient cliquable
- En revenant à la liste des épreuves puis en resélectionnant `TEST-2026`, le statut "Démarrée" est conservé (persisté en base)

### 8. Vérifier la réception d'un screenshot

Par défaut l'intervalle de capture est aléatoire entre 8 et 12 minutes — trop long pour un test interactif.
Pour accélérer ce test ponctuellement, éditer temporairement `src/Cerebro.Agent/Program.cs` :

```csharp
var options = new AgentOptions(serverUrl, sessionCode, candidateId, MinIntervalSeconds: 5, MaxIntervalSeconds: 10);
```

puis relancer les deux agents. **Ne pas committer ce changement.**

**Attendu** : la colonne **Dernier screenshot** de chaque candidat se met à jour avec l'heure de réception,
dans les 5 à 10 secondes suivant le lancement.

### 9. Vérifier l'affichage d'une déconnexion

Couper l'agent du candidat `AAAA1111` (`Ctrl+C` dans son terminal).

**Attendu** :
- La ligne du candidat **reste affichée** (elle ne disparaît pas)
- Elle passe **en rouge**, colonne Connexion = **Déconnecté**
- Le candidat `BBBB2222` reste normal (vert/jaune selon son statut), non affecté

### 10. Consulter le journal d'activité

Cliquer sur **Journal d'activité**.

**Attendu** : une table chronologique apparaît avec au minimum, dans l'ordre :
`Épreuve démarrée` → `Connexion` (× 2) → `Statut de préparation` (× 2) → `Screenshot reçu` (× 2, avec la
taille en octets dans la colonne Détail) → `Déconnexion` (pour `AAAA1111`).

### 11. Arrêter l'épreuve

Cliquer sur **Arrêter l'épreuve**.

**Attendu** :
- Le statut passe à `Terminée à HH:MM:SS`
- Le journal d'activité affiche un nouvel évènement `Épreuve arrêtée` après actualisation
- Le bouton **Arrêter l'épreuve** devient grisé

### 12. Vérifier le blocage des connexions après arrêt

Dans un terminal :

```bash
dotnet run --project src/Cerebro.Agent -- http://127.0.0.1:5289 TEST-2026 BBBB2222
```

**Attendu** : l'agent échoue à se connecter avec un message contenant
`Cette session est terminée, les connexions ne sont plus acceptées.`

### 13. Vérifier la trace OpenTelemetry côté serveur

Dans le terminal du serveur (celui de l'étape 5), rechercher les blocs `Activity.DisplayName`.

**Attendu** : au moins un span par évènement métier (`Candidate.Join`, `Candidate.ReportReadiness`,
`Candidate.UploadScreenshot`, `Candidate.Disconnect`, `Session.Start`, `Session.Stop`), chacun avec les tags
`cerebro.session_code` et, sauf pour les évènements de session, `cerebro.candidate_id`.

### 14. Nettoyage

Arrêter tous les processus (`Ctrl+C` dans chaque terminal), puis :

```bash
rm -rf /tmp/cerebro-test src/Cerebro.Server/db src/Cerebro.Server/screenshots
```

(`db/` et `screenshots/` sont déjà exclus du dépôt par `.gitignore` — ce nettoyage évite juste de
laisser traîner les données de ce test sur le disque.)

## Scénarios d'erreur à valider

| Scénario | Commande | Résultat attendu |
|---|---|---|
| Identifiant candidat inconnu | `dotnet run --project src/Cerebro.Agent -- http://127.0.0.1:5289 TEST-2026 ID-INCONNU` | Rejet avec `Session ou identifiant candidat invalide.` |
| Code de session inconnu | `dotnet run --project src/Cerebro.Agent -- http://127.0.0.1:5289 SESSION-INCONNUE AAAA1111` | Rejet avec `Session ou identifiant candidat invalide.` |
| Provisioning d'une session déjà existante | Relancer la commande `provision` de l'étape 3 à l'identique | Échec, code de sortie `1`, message `La session 'TEST-2026' existe déjà dans la base.` |
| Provisioning avec un fichier roster manquant | `provision --session X --input /chemin/inexistant.json` | Échec, code de sortie `1`, message `Fichier d'entrée introuvable`. |
| Création de session (dashboard) sur un code déjà existant | Bouton "+ NOUVELLE SESSION", coller le roster de l'étape 2, code de session `TEST-2026` | Erreur affichée dans le formulaire : `La session 'TEST-2026' existe déjà dans la base. Choisissez un autre code.` (même message que la CLI, voir `Admin/ExamProvisioner.cs`) |
| Création de session (dashboard) avec un JSON invalide | Coller un texte qui n'est pas du JSON valide | Erreur affichée dans le formulaire : `Roster JSON invalide : ...` |
| Création de session (dashboard) avec un roster sans étudiant | Coller `{"ec":"X","date":"2026-01-01","rattrapage":false,"etudiants":{}}` | Erreur affichée dans le formulaire : `Le roster ne contient aucun étudiant exploitable.` |
| `start` (CLI) sur une session inconnue | `dotnet run --project src/Cerebro.Server -- start --session INCONNUE` | Échec, code de sortie `1`, message `Session 'INCONNUE' introuvable dans la base.` |
| `StartSession`/`StopSession` (dashboard) sur une session inconnue | Appel hub direct avec un code de session inexistant | Rejet avec `HubException: Session introuvable.` (pas de commande CLI `stop` équivalente — l'arrêt se fait uniquement depuis le dashboard) |
| Suppression (dashboard) d'une session en cours | Bouton "🗑 SUPPRIMER LA SESSION" sur une épreuve démarrée, non arrêtée | Rejet avec `HubException: Impossible de supprimer une épreuve en cours. Arrêtez-la d'abord.`, session et fichiers toujours présents |
| Suppression (dashboard) d'une session arrêtée | Bouton "🗑 SUPPRIMER LA SESSION" après confirmation, sur une épreuve terminée (ou jamais démarrée) | Retour à la liste des épreuves ; la session disparaît de "Épreuves planifiées" ; `screenshots/{session}/` supprimé du disque (candidats + `activity.log`) |
| Mauvais mot de passe sur `/login.html` | Se connecter avec un mauvais mot de passe | Message `Identifiant ou mot de passe incorrect.`, pas de redirection |
| Accès direct à `/index.html` sans session ouverte | Ouvrir `http://127.0.0.1:5289/index.html` dans une fenêtre de navigation privée | Redirection automatique vers `/login.html` |

## Ce que ce scénario ne couvre pas

- Capture d'écran réelle sur Windows et Linux (à rejouer sur du matériel réel — voir
  [Limites connues](docs/LIMITATIONS.md))
- Le TLS/reverse proxy Caddy (voir [Sécurisation du transport](docs/DEPLOYMENT.md#sécurisation-du-transport-tls)
  pour un scénario dédié avec certificat auto-signé et épinglage d'empreinte)
- La montée en charge avec un grand nombre de candidats simultanés
