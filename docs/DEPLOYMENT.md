# Déploiement

## 1. Serveur (`Docker` + `Caddy`)

- Image publiée sur GHCR
  - lancée via `deploy/docker-compose.yml` avec [Caddy](https://caddyserver.com/) devant un reverse proxy TLS
- `cerebro-server` et Caddy tournent dans deux conteneurs séparés sur un réseau Docker interne : 
  - `cerebro-server` ne publie aucun port (joignable uniquement par Caddy, via le nom de service `cerebro-server:8080` résolu par le DNS interne de Docker), seul Caddy expose `8443` vers l'extérieur

**1. Se logger sur GHCR**, requis tant que le dépôt reste privé (le package en hérite la visibilité — `docker pull` échoue sinon avec `unauthorized`), avec un[Personal Access Token](https://github.com/settings/tokens) portant le scope `read:packages` :

```bash
echo "$GHCR_PAT" | docker login ghcr.io -u <user> --password-stdin
```

**2. Récupérer l'image**, publiée à chaque tag `vX.Y.Z` poussé sur un commit de `main` (`.github/workflows/server-release.yml`, qui fait tourner les tests avant de publier) :

```bash
docker pull ghcr.io/coda-school-france/cerebro-server:<version>
```

**3. Lancer la pile**, avec l'override `deploy/docker-compose.prod.yml` qui remplace le `build:` local du fichier de base par cette image (voir les commentaires en tête de ce fichier) :

```bash
CEREBRO_SERVER_ADDRESS=<server-ip> CEREBRO_SERVER_VERSION=<version> \
  docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml up -d
```

- remplacer `server-ip` par l'IP ou le nom d'hôte réel du poste serveur sur le réseau d'examen
  - utilisée uniquement par Caddy pour générer son certificat auto-signé, voir `deploy/Caddyfile`
- Les candidats et le surveillant se connectent alors sur `https://<server-ip>:8443`.

Pré-pull l'image (étapes 1-2) la veille de l'examen : le réseau d'examen est volontairement isolé (pas d'accès internet le jour J), et `docker-compose.prod.yml` ne force jamais un re-pull au démarrage.

- `db/` et `screenshots/` sont persistés dans des volumes nommés (`cerebro-db`,
  `cerebro-screenshots`) : ils survivent à un redéploiement, tant qu'on ne fait pas
  `docker compose down -v`.
- Le volume `caddy_data` contient la CA locale et le certificat générés par `tls internal` — sans
  lui, ils sont régénérés à chaque recréation du conteneur Caddy, ce qui change l'empreinte SHA-256
  à recommuniquer aux agents (à ne surtout pas perdre en cours d'examen, donc éviter
  `docker compose down -v` une fois une session commencée).

### Récupérer les screenshots depuis le conteneur

Les screenshots vivent dans le volume nommé `cerebro-screenshots`, monté sur `/app/screenshots` dans le conteneur `cerebro-server`
- Les copier vers l'hôte avec `docker compose cp` (référence le service par son nom, pas besoin de connaître le nom réel du conteneur ni du volume — ni l'un ni l'autre ne sont fixes, ils dépendent du nom du projet compose) :

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml \
  cp cerebro-server:/app/screenshots ./screenshots-export
```

Organisés par session puis par candidat : `screenshots-export/{session}/{candidat}/*.png`. 
Cette commande fonctionne conteneur démarré ou arrêté (tant qu'il n'a pas été supprimé) ; en cas de suppression du conteneur (`docker compose down` sans `-v`), le volume et son contenu survivent — seul `docker compose down -v` les détruit.

Si pas d'accès au `docker-compose.yml`, utiliser `docker cp` directement sur le conteneur :

```bash
docker ps --filter "ancestor=ghcr.io/coda-school-france/cerebro-server" --format "{{.Names}}"
docker cp <nom-du-conteneur>:/app/screenshots ./screenshots-export
```

### Mettre à jour le serveur (nouvelle image)

Le `db/` (SQLite) et les `screenshots/` vivent dans des volumes nommés, indépendants du conteneur :
recréer `cerebro-server` sur une nouvelle image ne perd donc ni les sessions provisionnées ni les screenshots déjà reçus.

**1. Pull la nouvelle version** (répéter le login GHCR si le token a expiré) :

```bash
docker pull ghcr.io/coda-school-france/cerebro-server:<nouvelle-version>
```

**2. Relancer la pile avec ce tag** — seul `cerebro-server` est recréé, Caddy continue de tourner sans interruption (même certificat, même empreinte, rien à recommuniquer aux agents) :

```bash
CEREBRO_SERVER_ADDRESS=192.168.1.10 CEREBRO_SERVER_VERSION=<nouvelle-version> \
  docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml up -d
```

**3. Vérifier la version effectivement lancée** :

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml images cerebro-server
```

**Rollback** : même procédure en repointant `CEREBRO_SERVER_VERSION` sur le tag précédent (déjà présent localement s'il a été pull une fois, pas besoin de réseau pour revenir en arrière).

> À faire la veille d'un examen, jamais le jour J (réseau isolé, voir plus haut) — et jamais pendant qu'une session est en cours (les candidats connectés perdraient leur connexion SignalR le temps que`cerebro-server` redémarre).

### Sécurisation du transport (TLS)

Sur un réseau d'examen isolé, il n'y a pas de CA publique disponible pour obtenir un certificat classique (type Let's Encrypt) : 
- Caddy génère et sert automatiquement un certificat auto-signé via sa CA interne
- rien à configurer manuellement, c'est déjà réglé par `deploy/Caddyfile`

**Récupérer l'empreinte SHA-256 du certificat**, à communiquer aux agents étudiants. 
Caddy l'affiche en clair dans ses propres logs au démarrage (voir `deploy/caddy-entrypoint.sh`) — pas besoin d'appeler `openssl` à la main :

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml logs caddy | grep -A2 "Empreinte SHA-256"
```

En dehors de ce mode de déploiement (Caddy natif sur l'hôte, ou pour la retrouver après une purge des logs), la récupérer directement sur le certificat :

```bash
openssl s_client -connect 192.168.1.10:8443 </dev/null 2>/dev/null \
  | openssl x509 -noout -fingerprint -sha256
```

**Communiquer cette empreinte** aux candidats en même temps que l'URL du serveur et le code de session (annonce orale/écran en début de session, voir[Provisionner une épreuve](#3-provisionner-une-épreuve)). 
Elle se passe en 4ᵉ argument positionnel de l'agent (après l'identifiant candidat) ou via la variable d'environnement `CEREBRO_SERVER_CERT_THUMBPRINT` :

```bash
Cerebro.Agent https://192.168.1.10:8443 F2I-20260801-A FFFB5AB1 "19D497B5...3B5E"
```

L'agent valide alors le certificat du serveur par **épinglage d'empreinte** plutôt que par la chaîne de confiance du système : 
- un certificat différent (machine usurpée, MITM) est rejeté, sans qu'il soit nécessaire d'installer une CA sur chaque machine étudiante
- si l'empreinte n'est pas fournie, l'agent retombe sur la validation TLS standard (utile en HTTP simple, ou si le serveur possède un vrai certificat reconnu)

Le **navigateur du surveillant**, lui, affichera un avertissement pour ce certificat auto-signé : à accepter une fois manuellement sur ce seul poste (bouton "Continuer quand même" / "Avancé...").

## 2. Agent, un exécutable autonome par OS

`dotnet publish` en mode self-contained + fichier unique : l'étudiant n'a pas besoin du runtime .NET installé.

```bash
dotnet publish src/Cerebro.Agent -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -o ./publish/agent-win-x64
dotnet publish src/Cerebro.Agent -c Release -r osx-x64   --self-contained -p:PublishSingleFile=true -o ./publish/agent-osx-x64
dotnet publish src/Cerebro.Agent -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o ./publish/agent-osx-arm64
dotnet publish src/Cerebro.Agent -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish/agent-linux-x64
```

**Via GitHub Actions** : un tag `agent-vX.Y.Z` poussé sur un commit `main` déclenche `.github/workflows/agent-release.yml`
- build obfusqué pour les 4 OS ci-dessus, empaqueté avec les instructions d'installation (`docs/USER-DOC.txt`, contournement SmartScreen/Gatekeeper inclus)
- création d'une **Release GitHub en brouillon** (à relire et publier manuellement, ce sont des binaires exécutés directement par les candidats)

## 3. Provisionner une épreuve

L'admin fournit le code de session et le fichier JSON de l'épreuve (export existant de l'école, format `ec`/`date`/`rattrapage`/`etudiants`/`correcteurs`/`diplome`) :

```json
{
  "ec": "E01",
  "date": "2026-10-09",
  "rattrapage": false,
  "etudiants": {
    "jean.luc@coda.school": { "nom": "Jean Luc", "id": "FFFB5AB1", "promo": "B1", "drive_folder_id": "..." },
    "herr.cul@coda.school": { "nom": "Herr Cul", "id": "0770F2DB", "promo": "B1", "drive_folder_id": "..." }
  },
  "correcteurs": [{ "nom": "Yoan Thirion", "email": "yoan@coda.school" }],
  "diplome": "RNCP39608-CDWFS"
}
```

```bash
dotnet Cerebro.Server.dll provision --session F2I-20260801-A --input epreuve-e01.json --db ./cerebro.db
```

Le champ **`id`** de chaque étudiant (ex: `FFFB5AB1`) sert à la fois d'identifiant candidat et de secret de connexion — pas de jeton généré séparément : c'est déjà un identifiant propre à l'école, non devinable.

Le même fichier `cerebro.db` doit être utilisé par le serveur au démarrage (variable`ConnectionStrings__CerebroDb`, ou `appsettings.json` → `ConnectionStrings:CerebroDb`) :

```bash
ConnectionStrings__CerebroDb="Data Source=./cerebro.db" dotnet Cerebro.Server.dll
```

Une fois l'épreuve prête à démarrer (tous les candidats connectés et prêts sur le dashboard) :

```bash
dotnet Cerebro.Server.dll start --session F2I-20260801-A --db ./cerebro.db
```

Pour l'instant, cette commande se contente d'horodater le démarrage en base (utile pour l'audit) — elle ne bloque pas encore les connexions tardives ni ne débloque automatiquement le sujet d'examen(voir [limites connues](LIMITATIONS.md), "pas de top départ").

## 4. Instructions à donner aux étudiants (à faire la veille, pas le jour J)

- **Windows** : au premier lancement, SmartScreen affichera "Windows a protégé votre PC" → cliquer sur *Informations complémentaires* puis *Exécuter quand même*.
- **macOS** : Gatekeeper bloquera l'app (pas de compte Apple Developer) → **clic droit sur l'exécutable → Ouvrir** (une seule fois). Accorder ensuite la permission **Enregistrement de l'écran** dans *Réglages Système → Confidentialité et sécurité* quand macOS la demande.
- **Linux** : vérifier qu'un outil de capture est installé (`grim` sous Wayland, ou `scrot` / ImageMagick `import` / `gnome-screenshot` sous X11) — sinon `sudo apt install scrot` (ou équivalent selon la distribution).

## Utilisation le jour J

1. Annoncer une fois à toute la salle l'URL du serveur, le code de session et, si TLS est activé, l'empreinte du certificat (voir [provisioning](#3-provisionner-une-épreuve)).
2. Chaque candidat lance l'agent avec ces valeurs et son propre id (déjà connu de lui), par exemple `Cerebro.Agent https://192.168.1.10:8443 F2I-20260801-A FFFB5AB1 "19D497B5...3B5E"` — ou répond simplement aux invites interactives s'il lance l'agent sans argument.
3. Le surveillant ouvre le dashboard : il voit la liste des épreuves planifiées et **sélectionne** celle du jour, puis attend que tous les candidats apparaissent avec le statut **Prêt** (pas juste connectés — un statut **Échec** indique un problème de permission macOS ou d'outil manquant sous Linux, à résoudre avant de démarrer).
4. Le surveillant clique sur **Démarrer l'épreuve** dans le dashboard une fois tout le monde prêt (équivalent CLI : `dotnet Cerebro.Server.dll start --session F2I-20260801-A`).
5. En fin d'épreuve, il clique sur **Arrêter l'épreuve** : le hub refuse alors toute nouvelle connexion candidat pour cette session (les candidats déjà connectés ne sont pas coupés de force — voir [limites connues](LIMITATIONS.md)).
