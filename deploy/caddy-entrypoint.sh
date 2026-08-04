#!/bin/sh
# Démarre Caddy normalement, puis lit son propre certificat (auto-signé, "tls internal") une fois
# prêt pour en afficher l'empreinte SHA-256 en clair dans les logs du conteneur — plutôt que
# d'obliger le surveillant à lancer la commande openssl à la main à chaque démarrage. Écrit aussi
# l'empreinte dans un fichier partagé (volume cert-info) pour que cerebro-server puisse l'afficher
# sur la page de connexion (voir Program.cs, /account/server-thumbprint).
set -e

CADDY_ADDRESS="${CEREBRO_SERVER_ADDRESS:-localhost}"

caddy run --config /etc/caddy/Caddyfile --adapter caddyfile &
CADDY_PID=$!
trap 'kill -TERM "$CADDY_PID" 2>/dev/null' TERM INT

echo "En attente du certificat TLS de Caddy..."
THUMBPRINT=""
for _ in $(seq 1 30); do
    THUMBPRINT=$(openssl s_client -connect "localhost:8443" -servername "$CADDY_ADDRESS" </dev/null 2>/dev/null \
        | openssl x509 -noout -fingerprint -sha256 2>/dev/null | sed 's/^sha256 Fingerprint=//')
    [ -n "$THUMBPRINT" ] && break
    sleep 1
done

if [ -n "$THUMBPRINT" ]; then
    echo "=================================================================="
    echo " Empreinte SHA-256 du certificat serveur (CEREBRO_SERVER_CERT_THUMBPRINT) :"
    echo " $THUMBPRINT"
    echo " À communiquer aux agents candidats en même temps que l'URL et le code de session."
    echo "=================================================================="

    if [ -n "$THUMBPRINT_OUTPUT_PATH" ]; then
        mkdir -p "$(dirname "$THUMBPRINT_OUTPUT_PATH")"
        echo "$THUMBPRINT" > "$THUMBPRINT_OUTPUT_PATH"
    fi
else
    echo "Avertissement : impossible de récupérer l'empreinte du certificat après 30s." >&2
fi

wait "$CADDY_PID"
