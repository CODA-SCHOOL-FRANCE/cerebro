# xavier-agent

[![npm version](https://img.shields.io/npm/v/xavier-agent?color=cb3837&logo=npm&logoColor=white)](https://www.npmjs.com/package/xavier-agent)
[![npm downloads](https://img.shields.io/npm/dm/xavier-agent)](https://www.npmjs.com/package/xavier-agent)

Wrapper npm pour l'agent candidat Cerebro (Xavier). Ne contient aucun code de Cerebro : à
l'installation, `postinstall` télécharge le binaire natif correspondant à votre plateforme
depuis les [Releases publiques de xavier-releases](https://github.com/CODA-SCHOOL-FRANCE/xavier-releases/releases)
(le dépôt `cerebro` lui-même est privé).

## Utilisation

```bash
npx xavier-agent <serverUrl> <sessionCode> <candidateId> [certThumbprint]
```

ou, installé globalement :

```bash
npm install -g xavier-agent
xavier <serverUrl> <sessionCode> <candidateId> [certThumbprint]
```

Sans argument, l'agent demande les informations interactivement.

## Version épinglée

Par défaut, `npm install` télécharge la dernière version publiée de Xavier. Pour épingler une
version précise (utile pour reproduire un déploiement) :

```bash
XAVIER_VERSION=0.1.0 npm install xavier-agent
```

## Plateformes supportées

Windows x64, macOS Intel, macOS Apple Silicon, Linux x64 — mêmes builds que les autres canaux de
distribution (script d'installation, Homebrew, Scoop). Voir
[docs/DEPLOYMENT.md](https://github.com/CODA-SCHOOL-FRANCE/cerebro/blob/main/docs/DEPLOYMENT.md#2-agent-xavier-distribution-multi-canal)
du dépôt principal pour le tableau complet des canaux.
