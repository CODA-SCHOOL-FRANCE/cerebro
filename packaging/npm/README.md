# xavier-agent

[![npm version](https://img.shields.io/npm/v/xavier-agent?color=cb3837&logo=npm&logoColor=white)](https://www.npmjs.com/package/xavier-agent)
[![npm downloads](https://img.shields.io/npm/dm/xavier-agent)](https://www.npmjs.com/package/xavier-agent)
[![Cerebro](https://img.shields.io/badge/cerebro-d%C3%A9p%C3%B4t%20principal-blueviolet)](https://github.com/CODA-SCHOOL-FRANCE/cerebro)

## À quoi sert ce package ?

[Cerebro](https://github.com/CODA-SCHOOL-FRANCE/cerebro) est un outil anti-fraude pour la
surveillance d'épreuves à distance (BYOD, multi-OS) : un serveur centralise les sessions et
affiche un dashboard temps réel au surveillant, pendant que chaque candidat lance sur sa propre
machine un agent léger — **Xavier** — qui capture des screenshots à intervalles aléatoires et les
transmet en direct au serveur.

`xavier-agent` est le moyen d'installer cet agent via npm : c'est un **wrapper**, pas l'agent
lui-même. Il ne contient aucun code de Cerebro — à l'installation, son script `postinstall`
télécharge le binaire natif correspondant à votre plateforme depuis les
[Releases de cerebro](https://github.com/CODA-SCHOOL-FRANCE/cerebro/releases), puis l'expose comme
commande `xavier`. C'est l'un des cinq canaux d'installation de l'agent (avec Homebrew, Scoop, un
script d'installation, et le téléchargement manuel de l'archive) — voir la
[documentation de déploiement](https://github.com/CODA-SCHOOL-FRANCE/cerebro/blob/main/docs/DEPLOYMENT.md#2-agent-xavier-distribution-multi-canal)
du dépôt principal pour le détail des autres canaux et de l'architecture globale.

Un candidat n'a normalement pas besoin d'installer ce package lui-même : c'est fait pour lui, en
suivant les instructions transmises par son surveillant le jour de l'épreuve.

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
distribution.
