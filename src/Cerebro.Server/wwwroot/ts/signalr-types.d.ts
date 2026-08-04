// Le client SignalR tourne en global (UMD) via <script src="lib/signalr/signalr.min.js">, copié
// depuis le paquet npm @microsoft/signalr au build (voir package.json "build") plutôt que chargé
// en tant que module — pas de bundler dans ce projet. Cette déclaration ne fait que brancher les
// vrais types du paquet npm sur la variable globale `signalR`, pour un typage complet et exact
// (au lieu d'une déclaration minimale entretenue à la main).
import type * as SignalR from "@microsoft/signalr";

declare global {
  const signalR: typeof SignalR;
}
