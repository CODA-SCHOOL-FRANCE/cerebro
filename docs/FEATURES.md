# Fonctionnalités supportées

- **Capture d'écran multi-OS** : Windows (GDI via P/Invoke), macOS (`screencapture` système),
  Linux (cascade `grim` → `scrot` → `import` → `gnome-screenshot`, premier outil disponible).
- **Auto-test de préparation** : à la connexion, l'agent capture un screenshot de test et remonte
  le résultat (prêt / échec + raison : permission refusée, outil manquant, etc.) — le surveillant
  voit un vrai statut de préparation, pas juste "connecté".
- **Dashboard temps réel** : liste des épreuves planifiées (provisionnées) à sélectionner — plus
  besoin de connaître/taper un code de session — puis, une fois une épreuve choisie, liste des
  candidats avec statut, horodatage du dernier screenshot reçu, mise à jour instantanée (SignalR),
  sans dépendance à un CDN externe (client JS vendoré localement — fonctionne sans accès internet
  le jour J).
- **Suivi de connexion en direct** : chaque agent envoie un battement (`Ping`) toutes les 60
  secondes par défaut, indépendamment des screenshots. Le dashboard affiche le statut de connexion
  et l'horodatage du dernier signal reçu par candidat ; si un agent se déconnecte (perte réseau,
  fermeture de l'application, machine éteinte...), sa ligne reste visible mais passe **en rouge**
  au lieu de disparaître — le surveillant voit immédiatement qui a décroché, plutôt que de perdre
  la trace du candidat.
- **Démarrage/arrêt de session depuis le dashboard** : le surveillant démarre l'épreuve
  (`StartSession`) une fois tous les candidats connectés et prêts, puis l'arrête en fin d'épreuve
  (`StopSession`) — après quoi le hub refuse toute nouvelle connexion d'agent pour cette session
  (`Cette session est terminée, les connexions ne sont plus acceptées.`). Les candidats déjà
  connectés au moment de l'arrêt ne sont pas déconnectés de force (voir limites connues).
- **Reconnexion automatique** : coupure réseau ponctuelle gérée par l'agent, qui rejoint
  automatiquement la session et renvoie son dernier statut connu.
- **Capture à intervalle aléatoire**, configurable (8–12 minutes par défaut, soit ~10 minutes en
  moyenne) — volontairement aléatoire pour ne pas être prévisible.
- **Stockage des screenshots côté serveur**, organisé par session/candidat, avec assainissement
  strict des identifiants pour empêcher toute traversée de répertoire.
- **Limite de message SignalR relevée à 50 Mo** pour supporter des screenshots plein écran /
  multi-moniteur / retina.
- **Chiffrement en transit par épinglage de certificat** : l'agent peut valider le certificat du
  serveur par empreinte SHA-256 plutôt que par la chaîne de confiance du système — utile derrière
  un reverse proxy TLS auto-signé sans avoir à installer une CA sur chaque machine étudiante (voir
  [Sécurisation du transport](DEPLOYMENT.md#sécurisation-du-transport-tls)).
- **Authentification par identifiant candidat enregistré** : le provisioning charge le roster
  officiel de l'épreuve (export existant de l'école) en base SQLite, et le hub vérifie que
  l'identifiant fourni par l'agent y est bien enregistré pour cette session avant d'accepter la
  connexion. Connaître seulement le code de session ne suffit plus à rejoindre une session ou à
  usurper un candidat (voir [Provisionner une épreuve](DEPLOYMENT.md#3-provisionner-une-épreuve)).
- **Dashboard protégé par identifiant/mot de passe** : accéder à `/index.html` (ou `/`) redirige
  vers un écran de connexion tant que la session n'est pas ouverte (cookie HttpOnly, 12h,
  `SameSite=Strict`). Les identifiants sont définis via `set-password` (mot de passe hashé en
  PBKDF2-SHA256, jamais stocké en clair) et n'affectent en rien les agents candidats, qui restent
  authentifiés uniquement par code de session + id candidat — seules les méthodes du hub réservées
  au dashboard (`GetPlannedSessions`, `StartSession`, etc.) exigent la session ouverte.
- **Télémétrie OpenTelemetry** : chaque connexion, déconnexion, screenshot reçu et changement de
  statut de préparation est tracé (`ActivitySource`) et compté (métriques), et persisté dans un
  journal d'activité par session en base SQLite — consultable directement depuis le dashboard
  (bouton "Journal d'activité"), sans outil externe.
