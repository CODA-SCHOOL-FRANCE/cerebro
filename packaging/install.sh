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

# Un seul tag vX.Y.Z publie à la fois l'image Docker du serveur et les archives agent (voir
# release.yml) : /releases/latest pointe donc toujours vers une release contenant les archives
# recherchées ci-dessous, pas besoin de filtrer par nom de tag.
if [ -n "${XAVIER_VERSION:-}" ]; then
  release_url="https://api.github.com/repos/${REPO}/releases/tags/v${XAVIER_VERSION}"
else
  release_url="https://api.github.com/repos/${REPO}/releases/latest"
fi
release_json="$(curl -fsSL "${release_url}")" || fail "impossible de récupérer la release (${release_url})"

asset_url="$(printf '%s' "${release_json}" | grep -o "\"browser_download_url\": *\"[^\"]*-${rid}\\.zip\"" | head -1 | sed -E 's/.*"(https:[^"]+)"/\1/')"
[ -n "${asset_url}" ] || fail "aucune archive '*-${rid}.zip' trouvée"

tag_name="$(printf '%s' "${release_json}" | grep -o '"tag_name": *"v[^"]*"' | head -1 | sed -E 's/.*"(v[^"]+)"$/\1/')"

echo "Téléchargement de $(basename "${asset_url}") (${tag_name})..."

tmp_dir="$(mktemp -d)"
trap 'rm -rf "${tmp_dir}"' EXIT

curl -fsSL "${asset_url}" -o "${tmp_dir}/xavier.zip" || fail "téléchargement échoué"

mkdir -p "${INSTALL_DIR}"
unzip -o -q "${tmp_dir}/xavier.zip" xavier xavier.config.json -d "${tmp_dir}/extracted" || fail "extraction échouée (unzip requis)"
mv "${tmp_dir}/extracted/xavier" "${INSTALL_DIR}/xavier"
chmod +x "${INSTALL_DIR}/xavier"

# Retire l'attribut quarantine macOS si présent (no-op silencieux sous Linux ou si absent) : sans
# ça, Gatekeeper bloquerait le premier lancement comme documenté dans USER-DOC.txt.
xattr -d com.apple.quarantine "${INSTALL_DIR}/xavier" 2>/dev/null || true

# Ne jamais écraser un xavier.config.json déjà présent : le surveillant a pu le remplir avec les
# vraies valeurs de la session (voir docs/DEPLOYMENT-AGENT.md) - un ré-lancement de ce script (mise
# à jour de version, par exemple) ne doit pas silencieusement le réinitialiser. Le fichier livré
# dans l'archive a des champs à null par défaut (docs/xavier.config.json) : tant qu'il n'est pas
# édité, l'agent retombe sur les prompts interactifs exactement comme en son absence.
if [ ! -f "${INSTALL_DIR}/xavier.config.json" ]; then
  mv "${tmp_dir}/extracted/xavier.config.json" "${INSTALL_DIR}/xavier.config.json"
fi

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
echo "Un xavier.config.json a été déposé dans ${INSTALL_DIR}/ : si votre surveillant vous a"
echo "communiqué les valeurs de la session, éditez-le pour ne plus avoir à les saisir à chaque"
echo "lancement (sinon, laissez-le tel quel — xavier les redemandera simplement)."
