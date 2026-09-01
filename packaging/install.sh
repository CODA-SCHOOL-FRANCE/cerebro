#!/usr/bin/env sh
# Installe le binaire natif de Xavier (agent candidat Cerebro) pour macOS/Linux.
#
#   curl -fsSL https://raw.githubusercontent.com/CODA-SCHOOL-FRANCE/cerebro/main/packaging/install.sh | sh
#
# Variables d'environnement optionnelles :
#   XAVIER_VERSION     version précise à installer (ex: 0.1.0) ; par défaut, la dernière release.
#   XAVIER_INSTALL_DIR dossier d'installation ; par défaut $HOME/.xavier/bin.
set -eu

REPO="CODA-SCHOOL-FRANCE/cerebro"
INSTALL_DIR="${XAVIER_INSTALL_DIR:-$HOME/.xavier/bin}"

fail() {
  echo "Erreur : $1" >&2
  echo "Installation manuelle : téléchargez l'archive correspondante sur" >&2
  echo "https://github.com/${REPO}/releases" >&2
  exit 1
}

os="$(uname -s)"
arch="$(uname -m)"

case "${os}" in
  Darwin)
    case "${arch}" in
      arm64) rid="osx-arm64" ;;
      x86_64) rid="osx-x64" ;;
      *) fail "architecture macOS non supportée (${arch})" ;;
    esac
    ;;
  Linux)
    case "${arch}" in
      x86_64) rid="linux-x64" ;;
      *) fail "architecture Linux non supportée (${arch}) — seul linux-x64 est publié" ;;
    esac
    ;;
  *)
    fail "système non supporté (${os}) — utilisez install.ps1 sous Windows"
    ;;
esac

command -v curl >/dev/null 2>&1 || fail "curl est requis"

if [ -n "${XAVIER_VERSION:-}" ]; then
  release_url="https://api.github.com/repos/${REPO}/releases/tags/agent-v${XAVIER_VERSION}"
  release_json="$(curl -fsSL "${release_url}")" || fail "impossible de récupérer la release (${release_url})"
else
  # cerebro publie aussi des releases serveur (tags vX.Y.Z, sans binaire agent - le serveur se
  # distribue via une image Docker) entrelacées avec celles de l'agent (tags agent-vX.Y.Z) :
  # /releases/latest pourrait renvoyer l'une d'elles. Seules les releases agent ont des assets, donc
  # chercher directement la première correspondance dans la liste (triée du plus récent au plus
  # ancien par l'API) suffit à isoler la bonne release, en un seul appel.
  releases_url="https://api.github.com/repos/${REPO}/releases?per_page=100"
  release_json="$(curl -fsSL "${releases_url}")" || fail "impossible de récupérer les releases (${releases_url})"
fi

asset_url="$(printf '%s' "${release_json}" | grep -o "\"browser_download_url\": *\"[^\"]*-${rid}\\.zip\"" | head -1 | sed -E 's/.*"(https:[^"]+)"/\1/')"
[ -n "${asset_url}" ] || fail "aucune archive '*-${rid}.zip' trouvée"

tag_name="$(printf '%s' "${release_json}" | grep -o '"tag_name": *"agent-v[^"]*"' | head -1 | sed -E 's/.*"(agent-v[^"]+)"$/\1/')"

echo "Téléchargement de $(basename "${asset_url}") (${tag_name})..."

tmp_dir="$(mktemp -d)"
trap 'rm -rf "${tmp_dir}"' EXIT

curl -fsSL "${asset_url}" -o "${tmp_dir}/xavier.zip" || fail "téléchargement échoué"

mkdir -p "${INSTALL_DIR}"
unzip -o -q "${tmp_dir}/xavier.zip" xavier -d "${tmp_dir}/extracted" || fail "extraction échouée (unzip requis)"
mv "${tmp_dir}/extracted/xavier" "${INSTALL_DIR}/xavier"
chmod +x "${INSTALL_DIR}/xavier"

# Retire l'attribut quarantine macOS si présent (no-op silencieux sous Linux ou si absent) : sans
# ça, Gatekeeper bloquerait le premier lancement comme documenté dans USER-DOC.txt.
xattr -d com.apple.quarantine "${INSTALL_DIR}/xavier" 2>/dev/null || true

echo
echo "Xavier installé dans ${INSTALL_DIR}/xavier"

case ":${PATH}:" in
  *":${INSTALL_DIR}:"*) ;;
  *)
    echo
    echo "${INSTALL_DIR} n'est pas dans votre PATH. Pour l'ajouter (zsh/bash) :"
    echo "  echo 'export PATH=\"${INSTALL_DIR}:\$PATH\"' >> ~/.zshrc  # ou ~/.bashrc"
    echo "  source ~/.zshrc"
    ;;
esac

echo
echo "Usage : xavier <serverUrl> <sessionCode> <candidateId> [certThumbprint]"
echo "Si votre surveillant vous a fourni un xavier.config.json prérempli, placez-le dans"
echo "${INSTALL_DIR}/ avant de lancer xavier."
