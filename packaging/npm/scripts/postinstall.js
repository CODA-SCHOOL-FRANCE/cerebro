// Télécharge le binaire natif de Xavier correspondant à la plateforme courante, depuis les
// Releases du dépôt xavier-releases (voir docs/DEPLOYMENT.md §2 : ce dépôt miroir ne contient
// aucun code source, seulement les archives déjà publiées par .github/workflows/agent-release.yml).
//
// N'extrait volontairement QUE le binaire (jamais xavier.config.json ni USER-DOC.txt) : le
// fichier de config livré dans l'archive n'est qu'un placeholder à remplir par le surveillant
// avant une distribution manuelle (voir docs/USER-DOC.txt) — l'embarquer ici casserait
// silencieusement la connexion des candidats qui installent via npm.
"use strict";

const fs = require("node:fs");
const path = require("node:path");
const AdmZip = require("adm-zip");

const RELEASES_REPO = "CODA-SCHOOL-FRANCE/xavier-releases";
const NATIVE_DIR = path.join(__dirname, "..", "native");

const RID_BY_PLATFORM_ARCH = {
  "win32:x64": "win-x64",
  "darwin:x64": "osx-x64",
  "darwin:arm64": "osx-arm64",
  "linux:x64": "linux-x64",
};

function fail(message) {
  console.error(`\n[xavier-agent] ${message}`);
  console.error(
    "[xavier-agent] Installation manuelle possible : téléchargez l'archive correspondant à " +
      "votre système sur https://github.com/CODA-SCHOOL-FRANCE/xavier-releases/releases\n",
  );
  process.exit(1);
}

async function resolveRelease(version) {
  const url = version
    ? `https://api.github.com/repos/${RELEASES_REPO}/releases/tags/agent-v${version}`
    : `https://api.github.com/repos/${RELEASES_REPO}/releases/latest`;

  const response = await fetch(url, {
    headers: { Accept: "application/vnd.github+json", "User-Agent": "xavier-agent-postinstall" },
  });

  if (!response.ok) {
    fail(`Impossible de récupérer la release (${url} -> ${response.status}).`);
  }

  return response.json();
}

async function downloadAsset(asset) {
  const response = await fetch(asset.browser_download_url, {
    headers: { "User-Agent": "xavier-agent-postinstall" },
  });

  if (!response.ok) {
    fail(`Téléchargement échoué (${asset.browser_download_url} -> ${response.status}).`);
  }

  return Buffer.from(await response.arrayBuffer());
}

async function main() {
  const rid = RID_BY_PLATFORM_ARCH[`${process.platform}:${process.arch}`];
  if (!rid) {
    fail(
      `Plateforme non supportée (${process.platform}/${process.arch}) — builds disponibles : ` +
        "Windows x64, macOS Intel/Apple Silicon, Linux x64.",
    );
  }

  const release = await resolveRelease(process.env.XAVIER_VERSION);
  const asset = release.assets.find((a) => a.name.endsWith(`-${rid}.zip`));
  if (!asset) {
    fail(`Aucune archive '*-${rid}.zip' trouvée dans la release ${release.tag_name}.`);
  }

  console.log(`[xavier-agent] Téléchargement de ${asset.name} (${release.tag_name})...`);
  const zipBuffer = await downloadAsset(asset);

  fs.mkdirSync(NATIVE_DIR, { recursive: true });

  const zip = new AdmZip(zipBuffer);
  const binaryName = rid === "win-x64" ? "xavier.exe" : "xavier";
  const entry = zip.getEntries().find((e) => e.entryName === binaryName);
  if (!entry) {
    fail(`Binaire '${binaryName}' introuvable dans ${asset.name}.`);
  }

  fs.writeFileSync(path.join(NATIVE_DIR, binaryName), entry.getData());
  if (rid !== "win-x64") {
    fs.chmodSync(path.join(NATIVE_DIR, binaryName), 0o755);
  }

  console.log(`[xavier-agent] Installé (${release.tag_name}, ${rid}).`);
  console.log(
    "[xavier-agent] Usage : xavier <serverUrl> <sessionCode> <candidateId> [certThumbprint]",
  );
  console.log(
    "[xavier-agent] Si votre surveillant vous a fourni un xavier.config.json prérempli, " +
      `placez-le dans ${NATIVE_DIR} avant de lancer xavier.`,
  );
}

main().catch((err) => fail(err.stack || String(err)));
