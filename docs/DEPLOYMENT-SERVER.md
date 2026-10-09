# Déployer le serveur

- Image publiée sur GHCR, un seul conteneur (`cerebro`) lancé avec un simple `docker run` (ou
  `docker compose up -d` avec le `docker-compose.yml` à la racine du dépôt)
- Kestrel (le serveur web intégré à ASP.NET Core) termine le TLS lui-même sur `8443`, avec un
  certificat auto-signé généré automatiquement au tout premier démarrage — rien à installer ni à
  configurer en plus (voir [Sécurisation du transport](#sécurisation-du-transport-tls) ci-dessous)

Pour déployer l'agent candidat (Xavier), voir [Déployer l'agent](DEPLOYMENT-AGENT.md) — document séparé.

## Lancer le serveur

L'image est publiée à chaque tag `vX.Y.Z` poussé sur un commit de `main`
(`.github/workflows/release.yml`, qui fait tourner les tests avant de publier — **même tag, même
pipeline que l'agent** : un seul `vX.Y.Z` publie à la fois cette image et les archives Xavier, voir
[Déployer l'agent](DEPLOYMENT-AGENT.md)). `<version>` = tag Git **sans le préfixe `v`** (ex. `0.2.3`).

```bash
docker run -d --name cerebro --restart unless-stopped \
  -p 8443:8443 \
  -v cerebro-db:/app/db \
  -v cerebro-screenshots:/app/screenshots \
  ghcr.io/coda-school-france/cerebro-server:<version>
```

Ou, depuis la racine du dépôt, avec le `docker-compose.yml` fourni (exactement équivalent : même
conteneur `cerebro`, mêmes volumes, donc toutes les commandes ci-dessous restent valables) :

```bash
docker compose pull   # récupère la dernière release (tag latest)
docker compose up -d
```

- Sans rien préciser, c'est l'image `latest` (dernière release) qui est lancée. `docker compose up`
  ne la retélécharge jamais si elle est déjà présente : d'où le `docker compose pull` avant, à faire
  la veille de l'épreuve (réseau isolé le jour J).
- Pour figer une version précise (ou revenir en arrière) : `CEREBRO_SERVER_VERSION=<version>
  docker compose up -d` (`<version>` sans le préfixe `v`, ex. `0.2.3`).

- Les candidats et le surveillant se connectent alors sur `https://<server-ip>:8443` (`server-ip` =
  IP ou nom d'hôte réel du poste serveur sur le réseau d'épreuve).
- Option `-e CEREBRO_SERVER_ADDRESS=<server-ip>` (défaut `localhost`), inutile en pratique : elle
  ne fait qu'inclure `<server-ip>` comme SAN (Subject Alternative Name) du certificat auto-signé,
  lu uniquement à sa génération (premier démarrage). L'agent candidat épingle l'empreinte SHA-256,
  jamais le SAN (voir [Sécurisation du transport](#sécurisation-du-transport-tls)), et le
  navigateur du surveillant affiche de toute façon un avertissement pour un certificat auto-signé.
- Les deux `-v` sont **indispensables** : sans eux, Docker crée des volumes anonymes, perdus (base,
  screenshots ET certificat) dès que le conteneur est recréé, par exemple lors d'une mise à jour.

Lancer cette commande (ou au moins `docker pull` de l'image) la veille de l'épreuve : le réseau
d'épreuve est volontairement isolé (pas d'accès internet le jour J). Une fois l'image présente
localement, `docker run` ne la re-télécharge jamais.

- `db/` et `screenshots/` sont persistés dans les volumes nommés `cerebro-db` et
  `cerebro-screenshots` : ils survivent à la suppression et à la recréation du conteneur, tant
  qu'on ne les supprime pas explicitement (`docker volume rm`).
- Le certificat TLS auto-signé (`db/cerebro.pfx`) vit dans le même volume `cerebro-db` que la base
  SQLite — sans lui, un nouveau certificat serait généré à chaque recréation du conteneur, ce qui
  changerait l'empreinte SHA-256 à recommuniquer aux agents (à ne surtout pas perdre en cours
  d'épreuve, donc ne jamais supprimer ces volumes une fois une session commencée).

> **Migration depuis l'ancien déploiement `docker compose`** : les données existantes vivent dans
> les volumes `deploy_cerebro-db` et `deploy_cerebro-screenshots` (préfixés par le nom du projet
> compose). Pour les conserver (base, compte surveillant, certificat donc même empreinte), supprimer
> l'ancien conteneur (`docker rm -f deploy-cerebro-server-1` — supprime le conteneur, pas les
> volumes) puis lancer la commande ci-dessus en remplaçant `cerebro-db`/`cerebro-screenshots` par
> ces deux noms.

## Récupérer les screenshots depuis le conteneur

**Le plus simple : depuis le dashboard**, sur l'écran de détail d'une épreuve, bouton
"⬇ Télécharger Session (ZIP)" — télécharge un zip de la session complète (tous les screenshots,
organisés par candidat, plus le journal d'activité `activity.log`), généré à la volée par le
serveur. Ne nécessite aucun accès au disque du serveur. Ce qui suit (`docker cp`) n'est utile que
pour un accès direct au disque (script, sauvegarde de plusieurs sessions d'un coup, session dont la
base a été perdue mais dont les fichiers survivent encore).

Les screenshots vivent dans le volume nommé `cerebro-screenshots`, monté sur `/app/screenshots` dans le conteneur `cerebro`. Les copier vers l'hôte avec `docker cp` :

```bash
docker cp cerebro:/app/screenshots ./screenshots-export
```

Organisés par session puis par candidat : `screenshots-export/{session}/{candidat}/*.webp`. 
Cette commande fonctionne conteneur démarré ou arrêté (tant qu'il n'a pas été supprimé) ; en cas de suppression du conteneur (`docker rm`), le volume et son contenu survivent — seul `docker volume rm cerebro-screenshots` les détruit.

## Mettre à jour le serveur (nouvelle image)

Le `db/` (SQLite) et les `screenshots/` vivent dans des volumes nommés, indépendants du conteneur :
recréer le conteneur sur une nouvelle image ne perd donc ni les sessions provisionnées ni les screenshots déjà reçus.

**1. Pull la nouvelle version** (`<nouvelle-version>` sans le préfixe `v`) :

```bash
docker pull ghcr.io/coda-school-france/cerebro-server:<nouvelle-version>
```

**2. Remplacer le conteneur** — le certificat TLS (`db/cerebro.pfx`, volume `cerebro-db`) survit à la recréation : même certificat, même empreinte, rien à recommuniquer aux agents :

```bash
docker rm -f cerebro
docker run -d --name cerebro --restart unless-stopped \
  -p 8443:8443 \
  -v cerebro-db:/app/db \
  -v cerebro-screenshots:/app/screenshots \
  ghcr.io/coda-school-france/cerebro-server:<nouvelle-version>
```

Avec le `docker-compose.yml` : `docker compose pull && docker compose up -d` (dernière release),
ou `CEREBRO_SERVER_VERSION=<nouvelle-version> docker compose up -d` pour une version précise —
recrée le conteneur sur la nouvelle image, volumes conservés.

**3. Vérifier la version effectivement lancée** :

```bash
docker inspect cerebro --format '{{.Config.Image}}'
```

**Rollback** : même procédure avec le tag précédent (déjà présent localement s'il a été pull une fois, pas besoin de réseau pour revenir en arrière).

> À faire la veille d'une épreuve, jamais le jour J (réseau isolé, voir plus haut) — et jamais pendant qu'une session est en cours (les candidats connectés perdraient leur connexion SignalR le temps que le conteneur `cerebro` redémarre).

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
Le serveur l'affiche en clair dans ses propres logs à chaque démarrage — pas besoin d'appeler `openssl` à la main :

```bash
docker logs cerebro 2>&1 | grep -A2 "Empreinte SHA-256"
```

Pour la retrouver après une purge des logs, ou en dehors de Docker, la récupérer directement sur le certificat :

```bash
openssl s_client -connect 192.168.1.10:8443 </dev/null 2>/dev/null \
  | openssl x509 -noout -fingerprint -sha256
```

**Changer d'adresse ou forcer un nouveau certificat** (ex. le poste serveur change d'IP entre deux
sessions) sans perdre la base SQLite (donc sans supprimer le volume `cerebro-db`) — commande admin
`generate-cert`, même usage que `set-password` (voir plus bas) :

```bash
docker exec cerebro dotnet Cerebro.Server.dll generate-cert --address 192.168.1.20 --force
```

Redémarrer ensuite le conteneur (`docker restart cerebro`) pour que Kestrel charge le nouveau certificat, et recommuniquer la
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

Le conteneur `cerebro` tourne par défaut en mode serveur web (pas en mode admin) : la commande s'exécute donc dans le conteneur déjà démarré, avec `docker exec` :

```bash
docker exec -it cerebro dotnet Cerebro.Server.dll set-password --username surveillant
```

- `-it` est indispensable : la saisie du mot de passe est masquée (aucun echo, ni terminal ni historique shell), ce qui a besoin d'un vrai terminal interactif.
- `surveillant` est un nom d'utilisateur libre (un seul compte supporté pour l'instant).
- Pas besoin de préciser `--db` : le chemin par défaut (`db/cerebro.db`, relatif au `WORKDIR /app` du conteneur) correspond déjà au volume nommé `cerebro-db` monté par `docker run`.
- Le mot de passe est demandé deux fois (saisie + confirmation).

À faire une seule fois (la base SQLite étant dans un volume nommé, les identifiants survivent aux redéploiements — voir plus haut) ; à refaire uniquement après suppression du volume `cerebro-db` ou un changement de mot de passe voulu.

Le surveillant se connecte ensuite sur `https://<server-ip>:8443/login.html` avec ce couple identifiant/mot de passe.

## Provisionner une épreuve

**Depuis le dashboard, sans fichier JSON** : bouton **"+ NOUVELLE SESSION"**, onglet **"Saisie
manuelle"** — coller la liste des étudiants (un nom par ligne) et saisir le code de session. Le
serveur génère un identifiant de connexion unique et non devinable pour chaque étudiant
(`Admin/ExamProvisioner.ProvisionFromNamesAsync`, via SignalR `CerebroHub.CreateSessionFromNames`),
affiché **une seule fois** juste après la création — à noter ou copier (bouton "Copier la liste")
pour le communiquer aux candidats, il n'est pas ré-affiché ensuite ailleurs dans le dashboard.

**Avec un fichier JSON existant** (un champ `etudiants`, chaque étudiant avec `nom` et `id` — c'est
tout ce qu'`ExamProvisioner` utilise, voir `Admin/ExamRoster.cs`) :

```json
{
  "etudiants": [
    { "nom": "Jean Dupont", "id": "FFFB5AB1" },
    { "nom": "Marie Durand", "id": "0770F2DB" }
  ]
}
```

`etudiants` accepte aussi un objet indexé par une clé libre (l'export d'un outil tiers utilise
souvent l'email comme clé) — la clé de chaque entrée est ignorée, seuls `nom`/`id` comptent :

```json
{
  "etudiants": {
    "jean.dupont@ecole.fr": { "nom": "Jean Dupont", "id": "FFFB5AB1" },
    "marie.durand@ecole.fr": { "nom": "Marie Durand", "id": "0770F2DB" }
  }
}
```

Tout champ en plus au niveau racine (nom de l'épreuve, date, rattrapage, correcteurs...) est
silencieusement ignoré — seul `etudiants` est lu.

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
