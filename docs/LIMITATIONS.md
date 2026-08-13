# Limites connues / à faire avant un examen réel

Ce projet est fonctionnel mais **pas encore prêt pour un examen certificatif réel**. À traiter avant :

- ✅ ~~Pas de TLS/HTTPS configuré~~ — traité : voir
  [Sécurisation du transport](DEPLOYMENT.md#sécurisation-du-transport-tls) (reverse proxy Caddy + épinglage de
  certificat côté agent). Reste un point d'attention : le navigateur du surveillant affichera un
  avertissement "connexion non sécurisée" pour le certificat auto-signé (à accepter une fois,
  humainement, sur ce seul poste — ce n'est pas automatisable sans CA publique).
- ✅ ~~Aucune authentification sur le hub~~ — traité des deux côtés : côté candidat, l'agent doit
  fournir un identifiant candidat réellement enregistré en base pour cette session, via le roster
  officiel de l'épreuve (voir [Provisionner une épreuve](DEPLOYMENT.md#3-provisionner-une-épreuve)) ;
  côté dashboard, l'accès est protégé par identifiant/mot de passe (cookie de session, voir
  [Compte du dashboard](DEPLOYMENT.md#compte-du-dashboard-surveillant)) — toutes les méthodes du hub
  réservées au surveillant (`GetPlannedSessions`, `CreateSession`, `StartSession`, `StopSession`,
  `DeleteSession`...) exigent cette session, plus une redirection vers `/login.html` pour toute page
  non authentifiée. Limites résiduelles : un seul compte dashboard partagé (pas de comptes/rôles
  distincts par surveillant) ; pas de limitation de débit sur les tentatives de connexion candidat
  (un identifiant inconnu peut être retenté indéfiniment, sans blocage après N échecs) ; et
  l'identifiant candidat n'est pas un secret cryptographique généré pour l'occasion — sa robustesse
  dépend entièrement de la façon dont l'école génère et distribue ces id dans son propre outillage.
- ⚠️ **Pas de signature de code.** macOS bloquera l'agent via Gatekeeper (pas de compte Apple
  Developer) ; Windows affichera un avertissement SmartScreen. Voir les instructions étudiants
  plus bas.
- ⚠️ **Linux dépend d'outils externes non embarqués** (`grim`/`scrot`/`import`/`gnome-screenshot`) :
  à vérifier/installer sur les machines Linux avant l'examen.
- ⚠️ **Pas de politique de rétention/suppression automatique** des screenshots après correction
  (recommandé pour la conformité RGPD) — une suppression manuelle par session est possible depuis
  le dashboard (bouton "🗑 SUPPRIMER LA SESSION", voir [Fonctionnalités](FEATURES.md)), mais rien
  d'automatique/planifié n'existe encore.
- ✅ ~~Pas de "top départ"~~ — partiellement traité : le surveillant démarre/arrête désormais
  l'épreuve depuis le dashboard (`StartSession`/`StopSession`), ce qui bloque les nouvelles
  connexions candidat après l'arrêt. Limite résiduelle : arrêter la session ne déconnecte pas de
  force les candidats déjà connectés (ils peuvent continuer à envoyer des screenshots jusqu'à ce
  qu'ils ferment l'agent eux-mêmes) ; et Cerebro ne débloque aucun contenu d'épreuve externe (LMS,
  sujet PDF...) — hors de son périmètre, qui reste la seule surveillance par capture d'écran.
- ⚠️ **Capture testée uniquement sur macOS** (machine de développement). Les implémentations
  Windows et Linux compilent et passent les tests d'intégration *sur l'OS où ils tournent*, mais
  n'ont pas encore été validées sur du matériel réel.
