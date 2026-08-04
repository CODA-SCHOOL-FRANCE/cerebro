# Cerebro

Outil anti-fraude pour la surveillance d'épreuves certifiantes à distance (BYOD, multi-OS).
Chaque candidat lance un agent léger sur sa propre machine ; l'agent capture des screenshots à
intervalles aléatoires et les transmet en temps réel à un serveur central, consulté par le
surveillant via un dashboard web.

![Cerebro by Charles Thirion](img/cerebro.webp)

## Sommaire

- [Architecture](docs/ARCHITECTURE.md)
- [Fonctionnalités supportées](docs/FEATURES.md)
- [Limites connues / à faire avant un examen réel](docs/LIMITATIONS.md)
- [Développement local](docs/DEVELOPMENT.md) — prérequis, structure du dépôt, prise en main, tests
- [Déploiement pour un examen](docs/DEPLOYMENT.md) — serveur, Docker, TLS, provisioning,
  utilisation le jour J
- [Documentation candidat](docs/USER-DOC.txt) — instructions d'installation et d'usage de l'agent,
  remises aux étudiants (incluse dans chaque archive de release)
- [TESTING.md](TESTING.md) — protocole de test manuel de bout en bout
