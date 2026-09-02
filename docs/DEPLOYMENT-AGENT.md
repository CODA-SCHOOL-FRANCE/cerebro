# Déployer l'agent (Xavier)

`dotnet publish` en mode self-contained + fichier unique : l'étudiant n'a pas besoin du runtime .NET installé.
Le projet source reste `src/Cerebro.Agent`, mais l'exécutable publié (`AssemblyName` dans le `.csproj`) s'appelle `xavier` (`xavier.exe` sous Windows).

Pour déployer le serveur, voir [Déployer le serveur](DEPLOYMENT-SERVER.md) — document séparé.

## Publier l'agent

```bash
dotnet publish src/Cerebro.Agent -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -o ./publish/agent-win-x64
dotnet publish src/Cerebro.Agent -c Release -r osx-x64   --self-contained -p:PublishSingleFile=true -o ./publish/agent-osx-x64
dotnet publish src/Cerebro.Agent -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o ./publish/agent-osx-arm64
dotnet publish src/Cerebro.Agent -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish/agent-linux-x64
```

**Via GitHub Actions** : un tag `agent-vX.Y.Z` poussé sur un commit `main` déclenche `.github/workflows/agent-release.yml`
- build pour les 4 OS ci-dessus, empaqueté avec les instructions d'installation (`docs/USER-DOC.txt`, contournement SmartScreen/Gatekeeper inclus)
- création directe d'une **Release GitHub publiée sur ce dépôt**, nommée `Xavier agent-vX.Y.Z` (le job de tests en amont sert de garde-fou : la release n'est créée que s'il passe)

Deux jobs se déclenchent ensuite en parallèle (même workflow, `needs: release`) :
- `publish-npm` republie le wrapper npm (`packaging/npm/`) avec le même numéro de version, via
  `npm-publish.yml` appelé en workflow réutilisable (`workflow_dispatch` y reste disponible pour
  republier le wrapper seul, sans nouvelle release agent).
- `update-distribution-channels` régénère le bucket Scoop (`bucket/xavier.json`, dans ce dépôt,
  commit direct sur `main`) et la formule
  [`homebrew-cerebro`](https://github.com/CODA-SCHOOL-FRANCE/homebrew-cerebro) avec les nouveaux
  hachages — Homebrew impose qu'un tap vive dans un dépôt séparé nommé `homebrew-<nom>`,
  indépendamment de la visibilité du dépôt principal, donc ce dépôt-là reste distinct.

## Canaux d'installation pour les étudiants

Le script d'installation et le postinstall npm résolvent la dernière release agent directement sur
ce dépôt (voir `packaging/install.sh`, `install.ps1`, `packaging/npm/scripts/postinstall.js`) :
rien à mirorer côté binaire. Canaux disponibles pour les étudiants (détail dans
`docs/USER-DOC.txt`) :

| Canal | Commande |
|---|---|
| Script (macOS/Linux) | `curl -fsSL https://raw.githubusercontent.com/CODA-SCHOOL-FRANCE/cerebro/main/packaging/install.sh \| sh` |
| Script (Windows) | `irm https://raw.githubusercontent.com/CODA-SCHOOL-FRANCE/cerebro/main/packaging/install.ps1 \| iex` |
| Homebrew (macOS/Linux) | `brew install coda-school-france/cerebro/xavier` |
| Scoop (Windows) | `scoop bucket add xavier https://github.com/CODA-SCHOOL-FRANCE/cerebro && scoop install xavier/xavier` |
| npm (tous OS) | `npx xavier-agent <serverUrl> <sessionCode> <candidateId>` |
| Manuel | archive `Xavier-<version>-<rid>.zip` sur la Release GitHub |

Ces canaux n'embarquent volontairement pas de `xavier.config.json` préempli (contrairement à
l'archive manuelle) : ce fichier est à éditer par le surveillant avant une distribution
individuelle (voir [Instructions à donner aux étudiants](#instructions-à-donner-aux-étudiants-à-faire-la-veille-pas-le-jour-j)
et `docs/USER-DOC.txt`) ; sans lui, l'agent retombe simplement sur les prompts interactifs.

## Lancer l'agent

```bash
xavier <serverUrl> <sessionCode> <candidateId> [certThumbprint]
```

Sans argument, l'agent demande ces informations interactivement. `serverUrl`, `sessionCode` et
`certThumbprint` sont les mêmes pour tous les candidats d'une session (voir [Provisionner une épreuve](DEPLOYMENT-SERVER.md#provisionner-une-épreuve) et [Sécurisation du transport](DEPLOYMENT-SERVER.md#sécurisation-du-transport-tls) côté serveur);
`candidateId` est propre à chaque étudiant. Détail complet des invites, du fichier de configuration `xavier.config.json` et des messages d'erreur : `docs/USER-DOC.txt`.

## Instructions à donner aux étudiants (à faire la veille, pas le jour J)

- **Windows** : au premier lancement, SmartScreen affichera "Windows a protégé votre PC" → cliquer sur *Informations complémentaires* puis *Exécuter quand même*.
- **macOS** : Gatekeeper bloquera l'app (pas de compte Apple Developer) → **clic droit sur l'exécutable → Ouvrir** (une seule fois). Accorder ensuite la permission **Enregistrement de l'écran** dans *Réglages Système → Confidentialité et sécurité* quand macOS la demande.
- **Linux** : vérifier qu'un outil de capture est installé (`grim` sous Wayland, ou `scrot` / ImageMagick `import` / `gnome-screenshot` sous X11) — sinon `sudo apt install scrot` (ou équivalent selon la distribution).
