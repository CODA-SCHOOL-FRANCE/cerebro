# Déployer le serveur

- Image publiée sur GHCR, un seul conteneur (`cerebro-server`) lancé via `deploy/docker-compose.yml`
- Kestrel (le serveur web intégré à ASP.NET Core) termine le TLS lui-même sur `8443`, avec un
  certificat auto-signé généré automatiquement au tout premier démarrage — rien à installer ni à
  configurer en plus (voir [Sécurisation du transport](#sécurisation-du-transport-tls) ci-dessous)

Pour déployer l'agent candidat (Xavier), voir [Déployer l'agent](DEPLOYMENT-AGENT.md) — document séparé.

## Lancer le serveur

**1. Récupérer l'image**, publiée à chaque tag `vX.Y.Z` poussé sur un commit de `main` (`.github/workflows/server-release.yml`, qui fait tourner les tests avant de publier) :

```bash
docker pull ghcr.io/coda-school-france/cerebro-server:<version>
```

**2. Lancer la pile**, avec l'override `deploy/docker-compose.prod.yml` qui remplace le `build:` local du fichier de base par cette image (voir les commentaires en tête de ce fichier) :

```bash
CEREBRO_SERVER_VERSION=<version> \
  docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml up -d
```

- Les candidats et le surveillant se connectent alors sur `https://<server-ip>:8443` (`server-ip` =
  IP ou nom d'hôte réel du poste serveur sur le réseau d'épreuve).
- `CEREBRO_SERVER_ADDRESS=<server-ip>` est optionnelle (défaut `localhost`) : elle ne fait
  qu'inclure `<server-ip>` comme SAN (Subject Alternative Name) du certificat auto-signé. L'agent
  candidat n'en a pas besoin — il épingle l'empreinte SHA-256 du certificat, jamais son SAN (voir
  [Sécurisation du transport](#sécurisation-du-transport-tls)) — la définir évite juste un
  avertissement de nom de certificat invalide en plus dans le navigateur du surveillant.

Pré-pull l'image (étapes 1-2) la veille de l'épreuve : le réseau d'épreuve est volontairement isolé (pas d'accès internet le jour J), et `docker-compose.prod.yml` ne force jamais un re-pull au démarrage.

- `db/` et `screenshots/` sont persistés dans des volumes nommés (`cerebro-db`,
  `cerebro-screenshots`) : ils survivent à un redéploiement, tant qu'on ne fait pas
  `docker compose down -v`.
- Le certificat TLS auto-signé (`db/cerebro.pfx`) vit dans le même volume `cerebro-db` que la base
  SQLite — sans lui, un nouveau certificat serait généré à chaque recréation du conteneur, ce qui
  changerait l'empreinte SHA-256 à recommuniquer aux agents (à ne surtout pas perdre en cours
  d'épreuve, donc éviter `docker compose down -v` une fois une session commencée).

## Récupérer les screenshots depuis le conteneur

**Le plus simple : depuis le dashboard**, sur l'écran de détail d'une épreuve, bouton
"⬇ Télécharger Session (ZIP)" — télécharge un zip de la session complète (tous les screenshots,
organisés par candidat, plus le journal d'activité `activity.log`), généré à la volée par le
serveur. Ne nécessite aucun accès au disque du serveur. Ce qui suit (`docker cp`) n'est utile que
pour un accès direct au disque (script, sauvegarde de plusieurs sessions d'un coup, session dont la
base a été perdue mais dont les fichiers survivent encore).

Les screenshots vivent dans le volume nommé `cerebro-screenshots`, monté sur `/app/screenshots` dans le conteneur `cerebro-server`
- Les copier vers l'hôte avec `docker compose cp` (référence le service par son nom, pas besoin de connaître le nom réel du conteneur ni du volume — ni l'un ni l'autre ne sont fixes, ils dépendent du nom du projet compose) :

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml \
  cp cerebro-server:/app/screenshots ./screenshots-export
```

Organisés par session puis par candidat : `screenshots-export/{session}/{candidat}/*.webp`. 
Cette commande fonctionne conteneur démarré ou arrêté (tant qu'il n'a pas été supprimé) ; en cas de suppression du conteneur (`docker compose down` sans `-v`), le volume et son contenu survivent — seul `docker compose down -v` les détruit.

Si pas d'accès au `docker-compose.yml`, utiliser `docker cp` directement sur le conteneur :

```bash
docker ps --filter "ancestor=ghcr.io/coda-school-france/cerebro-server" --format "{{.Names}}"
docker cp <nom-du-conteneur>:/app/screenshots ./screenshots-export
```

## Mettre à jour le serveur (nouvelle image)

Le `db/` (SQLite) et les `screenshots/` vivent dans des volumes nommés, indépendants du conteneur :
recréer `cerebro-server` sur une nouvelle image ne perd donc ni les sessions provisionnées ni les screenshots déjà reçus.

**1. Pull la nouvelle version** (répéter le login GHCR si le token a expiré) :

```bash
docker pull ghcr.io/coda-school-france/cerebro-server:<nouvelle-version>
```

**2. Relancer la pile avec ce tag** — le certificat TLS (`db/cerebro.pfx`, volume `cerebro-db`) survit à la recréation du conteneur : même certificat, même empreinte, rien à recommuniquer aux agents :

```bash
CEREBRO_SERVER_VERSION=<nouvelle-version> \
  docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml up -d
```

**3. Vérifier la version effectivement lancée** :

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml images cerebro-server
```

**Rollback** : même procédure en repointant `CEREBRO_SERVER_VERSION` sur le tag précédent (déjà présent localement s'il a été pull une fois, pas besoin de réseau pour revenir en arrière).

> À faire la veille d'une épreuve, jamais le jour J (réseau isolé, voir plus haut) — et jamais pendant qu'une session est en cours (les candidats connectés perdraient leur connexion SignalR le temps que`cerebro-server` redémarre).

## Sécurisation du transport (TLS)

Sur un réseau d'épreuve isolé, il n'y a pas de CA publique disponible pour obtenir un certificat classique (type Let's Encrypt) :
- Kestrel (le serveur web intégré, pas de reverse proxy séparé) génère et sert automatiquement un
  certificat auto-signé au tout premier démarrage — rien à configurer manuellement, voir
  `Program.cs` et `Tls/ServerCertificateProvisioner.cs`
- le certificat est écrit dans `db/cerebro.pfx` (volume `cerebro-db`, voir plus haut) : il survit
  aux redémarrages et redéploiements, régénéré uniquement si ce fichier est absent
- le certificat serveur est valide **5 ans** : un renouvellement automatique fréquent n'apporterait
  rien ici (l'agent épingle l'empreinte, pas la chaîne de confiance, voir plus bas) et casserait
  silencieusement une empreinte déjà distribuée aux candidats entre deux sessions

**Récupérer l'empreinte SHA-256 du certificat**, à communiquer aux agents étudiants.
`cerebro-server` l'affiche en clair dans ses propres logs à chaque démarrage — pas besoin d'appeler `openssl` à la main :

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml logs cerebro-server | grep -A2 "Empreinte SHA-256"
```

Pour la retrouver après une purge des logs, ou en dehors de Docker, la récupérer directement sur le certificat :

```bash
openssl s_client -connect 192.168.1.10:8443 </dev/null 2>/dev/null \
  | openssl x509 -noout -fingerprint -sha256
```

**Changer d'adresse ou forcer un nouveau certificat** (ex. le poste serveur change d'IP entre deux
sessions) sans perdre la base SQLite (donc sans `docker compose down -v`) — commande admin
`generate-cert`, même usage que `set-password` (voir plus bas) :

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml \
  exec cerebro-server dotnet Cerebro.Server.dll generate-cert --address 192.168.1.20 --force
```

Redémarrer ensuite le conteneur pour que Kestrel charge le nouveau certificat, et recommuniquer la
nouvelle empreinte affichée aux candidats.

**Communiquer cette empreinte** aux candidats en même temps que l'URL du serveur et le code de session (annonce orale/écran en début de session, voir [Provisionner une épreuve](#provisionner-une-épreuve)). 
Elle se passe en 4ᵉ argument positionnel de l'agent (après l'identifiant candidat) ou via la variable d'environnement `CEREBRO_SERVER_CERT_THUMBPRINT` :

```bash
xavier https://192.168.1.10:8443 SESSION-2026-A FFFB5AB1 "19D497B5...3B5E"
```

L'agent valide alors le certificat du serveur par **épinglage d'empreinte** plutôt que par la chaîne de confiance du système : 
- un certificat différent (machine usurpée, MITM) est rejeté, sans qu'il soit nécessaire d'installer une CA sur chaque machine étudiante
- si l'empreinte n'est pas fournie, l'agent retombe sur la validation TLS standard (utile en HTTP simple, ou si le serveur possède un vrai certificat reconnu)

Le **navigateur du surveillant**, lui, affichera un avertissement pour ce certificat auto-signé : à accepter une fois manuellement sur ce seul poste (bouton "Continuer quand même" / "Avancé...").

## Compte du dashboard (surveillant)

Le dashboard n'a qu'un seul compte, protégé par cookie de session (`/login.html`, `/account/login`) — les identifiants sont définis via la commande admin `set-password`, jamais en clair dans un fichier de config.

Le conteneur `cerebro-server` tourne par défaut en mode serveur web (pas en mode admin) : la commande s'exécute donc dans le conteneur déjà démarré, avec `docker compose exec` :

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml \
  exec -it cerebro-server dotnet Cerebro.Server.dll set-password --username surveillant
```

- `-it` est indispensable : la saisie du mot de passe est masquée (aucun echo, ni terminal ni historique shell), ce qui a besoin d'un vrai terminal interactif.
- `surveillant` est un nom d'utilisateur libre (un seul compte supporté pour l'instant).
- Pas besoin de préciser `--db` : le chemin par défaut (`db/cerebro.db`, relatif au `WORKDIR /app` du conteneur) correspond déjà au volume nommé `cerebro-db` monté par `docker-compose.yml`.
- Le mot de passe est demandé deux fois (saisie + confirmation).

À faire une seule fois (la base SQLite étant dans un volume nommé, les identifiants survivent aux redéploiements — voir plus haut) ; à refaire uniquement après un `docker compose down -v` ou un changement de mot de passe voulu.

Le surveillant se connecte ensuite sur `https://<server-ip>:8443/login.html` avec ce couple identifiant/mot de passe.

## Provisionner une épreuve

**Depuis le dashboard, sans fichier JSON** : bouton **"+ NOUVELLE SESSION"**, onglet **"Saisie
manuelle"** — coller la liste des étudiants (un nom par ligne) et saisir le code de session. Le
serveur génère un identifiant de connexion unique et non devinable pour chaque étudiant
(`Admin/ExamProvisioner.ProvisionFromNamesAsync`, via SignalR `CerebroHub.CreateSessionFromNames`),
affiché **une seule fois** juste après la création — à noter ou copier (bouton "Copier la liste")
pour le communiquer aux candidats, il n'est pas ré-affiché ensuite ailleurs dans le dashboard.

**Avec un fichier JSON existant** (une liste `etudiants`, chacun avec `nom` et `id` — c'est tout ce
qu'`ExamProvisioner` utilise, voir `Admin/ExamRoster.cs`) :

```json
{
  "etudiants": [
    { "nom": "Jean Dupont", "id": "FFFB5AB1" },
    { "nom": "Marie Durand", "id": "0770F2DB" }
  ]
}
```

```bash
dotnet Cerebro.Server.dll provision --session SESSION-2026-A --input epreuve-e01.json --db ./cerebro.db
```

**Depuis le dashboard**, sans accès CLI/SSH au serveur : bouton **"+ NOUVELLE SESSION"**, onglet
**"Roster JSON"**, coller le même JSON (ou charger le fichier) et saisir le code de session —
utilise exactement la même logique de provisioning (`Admin/ExamProvisioner.ProvisionAsync`) via
SignalR (`CerebroHub.CreateSession`), donc les mêmes validations et messages d'erreur que la
commande CLI.

Dans les deux cas, l'**`id`** de chaque étudiant sert à la fois d'identifiant candidat et de secret
de connexion — pas de jeton généré séparément. Avec le JSON, c'est déjà un identifiant propre à
l'établissement (ex: `FFFB5AB1`), non devinable ; en saisie manuelle, c'est le serveur qui le
génère avec les mêmes propriétés.

Le même fichier `cerebro.db` doit être utilisé par le serveur au démarrage (variable`ConnectionStrings__CerebroDb`, ou `appsettings.json` → `ConnectionStrings:CerebroDb`) :

```bash
ConnectionStrings__CerebroDb="Data Source=./cerebro.db" dotnet Cerebro.Server.dll
```

Une fois l'épreuve prête à démarrer (tous les candidats connectés et prêts sur le dashboard) :

```bash
dotnet Cerebro.Server.dll start --session SESSION-2026-A --db ./cerebro.db
```

Pour l'instant, cette commande se contente d'horodater le démarrage en base (utile pour l'audit) — elle ne bloque pas encore les connexions tardives ni ne débloque automatiquement le sujet de l'épreuve.

## Utilisation le jour J

1. Annoncer une fois à toute la salle l'URL du serveur, le code de session et, si TLS est activé, l'empreinte du certificat (voir [Provisionner une épreuve](#provisionner-une-épreuve)).
2. Chaque candidat lance l'agent avec ces valeurs et son propre id (déjà connu de lui), par exemple `xavier https://192.168.1.10:8443 SESSION-2026-A FFFB5AB1 "19D497B5...3B5E"` — ou répond simplement aux invites interactives s'il lance l'agent sans argument (voir [Déployer l'agent](DEPLOYMENT-AGENT.md)).
3. Le surveillant ouvre le dashboard : il voit la liste des épreuves planifiées et **sélectionne** celle du jour, puis attend que tous les candidats apparaissent avec le statut **Prêt** (pas juste connectés — un statut **Échec** indique un problème de permission macOS ou d'outil manquant sous Linux, à résoudre avant de démarrer).
4. Le surveillant clique sur **Démarrer l'épreuve** dans le dashboard une fois tout le monde prêt (équivalent CLI : `dotnet Cerebro.Server.dll start --session SESSION-2026-A`).
5. En fin d'épreuve, il clique sur **Arrêter l'épreuve** : le hub refuse alors toute nouvelle connexion candidat pour cette session (les candidats déjà connectés ne sont pas coupés de force).
