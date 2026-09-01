# Cerebro.LoadSim

Simule une session Cerebro avec plusieurs candidats, tous connectés depuis cette machine à un
serveur cible (local ou distant, déjà déployé) — utile pour tester la charge, le dashboard avec
plusieurs candidats affichés, ou juste vérifier qu'un déploiement encaisse correctement N
connexions simultanées, sans avoir besoin de N machines réelles.

Provisionne d'abord la session (comme le bouton "+ NOUVELLE SESSION" du dashboard, via
`CerebroHub.CreateSession`) avec des candidats synthétiques (`SIM0001`, `SIM0002`...), puis fait
tourner un `AgentRunner` réel par candidat — capture, compression WebP, ping, tout le pipeline de
l'agent sauf la capture d'écran elle-même (remplacée par une image générée, voir
`FakeScreenCapturer.cs`).

## Utilisation

```bash
dotnet run --project tools/Cerebro.LoadSim -- <server-url> <candidate-count> [options]
```

Exemple, 20 candidats contre un serveur local en HTTP :

```bash
dotnet run --project tools/Cerebro.LoadSim -- http://localhost:5289 20 \
  --dashboard-username surveillant
```

(mot de passe demandé de façon masquée si `--dashboard-password` est omis)

Contre un serveur en HTTPS avec certificat auto-signé (réseau d'épreuve isolé) :

```bash
dotnet run --project tools/Cerebro.LoadSim -- https://192.168.1.10:8443 20 \
  --dashboard-username surveillant --cert-thumbprint "19D497B5...3B5E"
```

`Ctrl+C` arrête proprement tous les candidats simulés. Voir `--help` pour la liste complète des
options (code de session, préfixe des identifiants, intervalles de capture/ping...).

## Notes

- Chaque candidat simulé envoie de vraies images (compressées en WebP comme l'agent réel) : à
  quantité de candidats élevée, la simulation génère une vraie charge réseau/disque représentative.
- Les intervalles par défaut (`--min-interval-seconds 15 --max-interval-seconds 30`, très en dessous
  des 8-12 minutes réelles de l'agent) sont pensés pour observer rapidement le comportement du
  dashboard — à ajuster si l'objectif est plutôt de reproduire fidèlement le trafic d'une épreuve réelle.
- Un candidat qui échoue (ex: session arrêtée en cours de simulation) affiche son erreur en rouge
  mais n'interrompt pas les autres.
