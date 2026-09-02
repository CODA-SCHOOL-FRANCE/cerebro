// Télécharge le binaire natif de Xavier correspondant à la plateforme courante, depuis les
// Releases du dépôt cerebro (voir docs/DEPLOYMENT-AGENT.md).
//
// N'extrait volontairement QUE le binaire (jamais xavier.config.json ni USER-DOC.txt) : le
// fichier de config livré dans l'archive n'est qu'un placeholder à remplir par le surveillant
// avant une distribution manuelle (voir docs/USER-DOC.txt) — l'embarquer ici casserait
// silencieusement la connexion des candidats qui installent via npm.
"use strict";

const fs = require("node:fs");
const path = require("node:path");
const AdmZip = require("adm-zip");

const REPO = "CODA-SCHOOL-FRANCE/cerebro";
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
      "votre système sur https://github.com/CODA-SCHOOL-FRANCE/cerebro/releases\n",
  );
  process.exit(1);
}

async function fetchJson(url) {
  const response = await fetch(url, {
    headers: { Accept: "application/vnd.github+json", "User-Agent": "xavier-agent-postinstall" },
  });

  if (!response.ok) {
    fail(`Impossible de récupérer la release (${url} -> ${response.status}).`);
  }

  return response.json();
}

async function resolveRelease(version) {
  if (version) {
    return fetchJson(`https://api.github.com/repos/${REPO}/releases/tags/agent-v${version}`);
  }

  // cerebro publie aussi des releases serveur (tags vX.Y.Z, sans binaire agent) entrelacées avec
  // celles de l'agent (tags agent-vX.Y.Z) : /releases/latest pourrait renvoyer l'une d'elles. On
  // liste donc les releases récentes et on garde la première taguée agent-v* (l'API les renvoie
  // triées du plus récent au plus ancien).
  const releases = await fetchJson(`https://api.github.com/repos/${REPO}/releases?per_page=100`);
  const release = releases.find((r) => r.tag_name.startsWith("agent-v"));
  if (!release) {
    fail("Aucune release agent (agent-vX.Y.Z) trouvée.");
  }

  return release;
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
