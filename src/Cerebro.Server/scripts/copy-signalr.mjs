// Copie le client SignalR (build navigateur, UMD) depuis le paquet npm vers wwwroot/lib, à charger
// en <script> classique (pas de bundler dans ce projet - voir wwwroot/ts/signalr-types.d.ts).
import { copyFileSync, mkdirSync } from "node:fs";

const source = "node_modules/@microsoft/signalr/dist/browser/signalr.min.js";
const destinationDir = "wwwroot/lib/signalr";

mkdirSync(destinationDir, { recursive: true });
copyFileSync(source, `${destinationDir}/signalr.min.js`);
