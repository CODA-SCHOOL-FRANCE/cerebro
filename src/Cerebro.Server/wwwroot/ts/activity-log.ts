import { activityListEl, activityOverlay } from "./dom.js";
import type { CerebroHubClient } from "./hub-client.js";
import { currentSession } from "./state.js";

const EVENT_LABELS: Record<string, string> = {
  CandidateJoined: "Connexion",
  CandidateDisconnected: "Déconnexion",
  ReadinessReported: "Statut de préparation",
  ScreenshotReceived: "Screenshot reçu",
  SessionStarted: "Épreuve démarrée",
  SessionEnded: "Épreuve arrêtée"
};

function eventTypeLabel(eventType: string): string {
  return EVENT_LABELS[eventType] ?? eventType;
}

export async function loadActivity(hub: CerebroHubClient): Promise<void> {
  if (!currentSession) {
    return;
  }

  const events = await hub.getSessionActivity(currentSession.sessionCode);
  activityListEl.innerHTML = "";

  if (events.length === 0) {
    const empty = document.createElement("div");
    empty.className = "empty-hint";
    empty.textContent = "Aucune activité pour le moment.";
    activityListEl.append(empty);
    return;
  }

  for (const event of events) {
    const entry = document.createElement("div");
    entry.className = "activity-entry";

    const time = document.createElement("span");
    time.className = "time";
    time.textContent = new Date(event.timestamp).toLocaleTimeString();

    const candidatePart = event.candidateId ? `${event.candidateId} — ` : "";
    entry.append(
      time,
      document.createTextNode(
        ` — ${candidatePart}${eventTypeLabel(event.eventType)}${event.detail ? ` (${event.detail})` : ""}`
      )
    );
    activityListEl.append(entry);
  }
}

export function refreshActivityIfVisible(hub: CerebroHubClient): void {
  if (!activityOverlay.hidden) {
    loadActivity(hub).catch((err: unknown) => console.error("Chargement du journal échoué", err));
  }
}
