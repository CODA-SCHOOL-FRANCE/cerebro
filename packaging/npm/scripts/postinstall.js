// Télécharge le binaire natif de Xavier correspondant à la plateforme courante, depuis les
// Releases du dépôt cerebro (voir docs/DEPLOYMENT-AGENT.md).
//
// N'extrait volontairement QUE le binaire et xavier.config.json (jamais USER-DOC.txt, qui n'a pas
// sa place dans un package npm). Le config.json livré dans l'archive a des champs à null par
// défaut (docs/xavier.config.json) : tant qu'il n'est pas édité par le surveillant, l'agent
// retombe simplement sur les prompts interactifs (voir Program.cs, `serverUrl ??= configFile?.ServerUrl`).
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
  // Un seul tag vX.Y.Z publie à la fois l'image Docker du serveur et les archives agent (voir
  // release.yml) : /releases/latest pointe donc toujours vers une release contenant les archives
  // recherchées ci-dessous, pas besoin de filtrer par nom de tag.
  if (version) {
    return fetchJson(`https://api.github.com/repos/${REPO}/releases/tags/v${version}`);
  }

  return fetchJson(`https://api.github.com/repos/${REPO}/releases/latest`);
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

  // Ne jamais écraser un xavier.config.json déjà présent : le surveillant a pu le remplir avec les
  // vraies valeurs de la session (voir docs/DEPLOYMENT-AGENT.md) - une réinstallation (mise à jour
  // de version, par exemple) ne doit pas silencieusement le réinitialiser.
  const configDestination = path.join(NATIVE_DIR, "xavier.config.json");
  if (!fs.existsSync(configDestination)) {
    const configEntry = zip.getEntries().find((e) => e.entryName === "xavier.config.json");
    if (configEntry) {
      fs.writeFileSync(configDestination, configEntry.getData());
    }
  }

  console.log(`[xavier-agent] Installé (${release.tag_name}, ${rid}).`);
  console.log(
    "[xavier-agent] Usage : xavier <serverUrl> <sessionCode> <candidateId> [certThumbprint]",
  );
  console.log(
    `[xavier-agent] Un xavier.config.json a été déposé dans ${NATIVE_DIR} : si votre surveillant ` +
      "vous a communiqué les valeurs de la session, éditez-le pour ne plus avoir à les saisir à " +
      "chaque lancement (sinon, laissez-le tel quel — xavier les redemandera simplement).",
  );
}

main().catch((err) => fail(err.stack || String(err)));
