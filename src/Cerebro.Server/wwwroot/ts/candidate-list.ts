import { avatarColor, initials } from "./avatar.js";
import { candidateListEl, hudOnlineCount } from "./dom.js";
import { candidates, currentSession } from "./state.js";
import { candidateReadinessBadge } from "./status-badges.js";
import type { CandidateStatusDto } from "./types.js";

export function upsertCandidate(status: CandidateStatusDto, options: { render?: boolean } = {}): void {
  const { render = true } = options;
  const existing = candidates.get(status.candidateId);

  candidates.set(status.candidateId, {
    candidateId: status.candidateId,
    name: existing?.name ?? status.candidateId,
    // Un candidat qui vient de se (re)connecter a forcément déjà été vu au moins une fois ;
    // une déconnexion (isConnected: false) ne doit en revanche jamais effacer cet historique.
    hasConnectedOnce: Boolean(existing?.hasConnectedOnce) || status.isConnected === true,
    isConnected: status.isConnected,
    isReady: status.isReady,
    failureReason: status.failureReason,
    failureDetail: status.failureDetail,
    lastSeenAt: status.lastSeenAt,
    lastScreenshotAt: status.lastScreenshotAt
  });

  if (render) {
    renderCandidates();
  }
}

export function setCandidateScreenshotTimestamp(candidateId: string, timestamp: string): void {
  const candidate = candidates.get(candidateId);
  if (candidate) {
    candidate.lastScreenshotAt = timestamp;
    renderCandidates();
  }
}

export function renderCandidates(): void {
  candidateListEl.innerHTML = "";

  let onlineCount = 0;

  for (const candidate of candidates.values()) {
    if (candidate.isConnected) {
      onlineCount++;
    }

    const { className, label } = candidateReadinessBadge(candidate);
    const row = document.createElement("div");
    // Ligne teintée en rouge uniquement si le candidat s'est déjà connecté puis a été perdu
    // (cas préoccupant) ; un candidat qui n'a simplement jamais démarré son agent ne l'est pas.
    row.className = "candidate-row" + (!candidate.isConnected && candidate.hasConnectedOnce ? " disconnected" : "");

    const identityCell = document.createElement("div");
    identityCell.className = "candidate-identity";

    const connectIndicator = document.createElement("span");
    connectIndicator.className = "connect-indicator " + (candidate.hasConnectedOnce ? "connect-seen" : "connect-never");
    connectIndicator.title = candidate.hasConnectedOnce ? "Déjà connecté" : "Jamais connecté";

    const avatar = document.createElement("span");
    avatar.className = "avatar";
    avatar.style.background = avatarColor(candidate.candidateId);
    avatar.textContent = initials(candidate.name);

    const name = document.createElement("span");
    name.className = "candidate-name";
    name.textContent = candidate.name;

    identityCell.append(connectIndicator, avatar, name);

    const connectionCell = document.createElement("div");
    const connectionBadge = document.createElement("span");
    connectionBadge.className = "badge " +
      (candidate.isConnected ? "badge-connected" : candidate.hasConnectedOnce ? "badge-disconnected" : "badge-never");
    connectionBadge.textContent = candidate.isConnected
      ? "CONNECTÉ"
      : candidate.hasConnectedOnce ? "DÉCONNECTÉ" : "JAMAIS CONNECTÉ";
    connectionCell.append(connectionBadge);

    const statusCell = document.createElement("div");
    const statusBadge = document.createElement("span");
    statusBadge.className = "badge " + className;
    statusBadge.textContent = label;
    statusCell.append(statusBadge);

    const lastSeenCell = document.createElement("div");
    lastSeenCell.className = "candidate-lastseen";
    lastSeenCell.textContent = candidate.lastSeenAt
      ? new Date(candidate.lastSeenAt).toLocaleTimeString()
      : "-";

    const screenshotCell = document.createElement("div");
    screenshotCell.className = "candidate-screenshot";
    if (candidate.lastScreenshotAt) {
      const isFresh = Date.now() - new Date(candidate.lastScreenshotAt).getTime() < 15 * 60 * 1000;
      screenshotCell.classList.toggle("fresh", isFresh);
      screenshotCell.textContent = new Date(candidate.lastScreenshotAt).toLocaleTimeString();
    } else {
      screenshotCell.textContent = "-";
    }

    row.append(identityCell, connectionCell, statusCell, lastSeenCell, screenshotCell);
    candidateListEl.append(row);
  }

  hudOnlineCount.textContent = currentSession ? String(onlineCount) : "-";
}
