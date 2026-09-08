#!/usr/bin/env node
"use strict";

const path = require("node:path");
const { spawnSync } = require("node:child_process");

const binaryName = process.platform === "win32" ? "xavier.exe" : "xavier";
const binaryPath = path.join(__dirname, "..", "native", binaryName);

const result = spawnSync(binaryPath, process.argv.slice(2), { stdio: "inherit" });

if (result.error) {
  console.error(
    `[xavier-agent] Impossible de lancer ${binaryPath} (${result.error.message}). ` +
      "Réinstallez le package (npm install xavier-agent) ou téléchargez l'archive manuellement " +
      "sur https://github.com/CODA-SCHOOL-FRANCE/cerebro/releases",
  );
  process.exit(1);
}

process.exit(result.status ?? 1);
