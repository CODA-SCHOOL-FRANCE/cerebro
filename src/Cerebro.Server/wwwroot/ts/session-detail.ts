import { renderCandidates, upsertCandidate } from "./candidate-list.js";
import {
  activityOverlay,
  currentSessionStatus,
  currentSessionTitle,
  downloadScreenshotsButton,
  sessionDetailSection,
  sessionPickerSection,
  startButton,
  stopButton
} from "./dom.js";
import type { CerebroHubClient } from "./hub-client.js";
import { candidates, currentSession, setCurrentSession } from "./state.js";
import type { ExamSessionSummaryDto } from "./types.js";

export async function selectSession(hub: CerebroHubClient, session: ExamSessionSummaryDto): Promise<void> {
  setCurrentSession(session);
  candidates.clear();
  activityOverlay.hidden = true;

  await hub.joinAsDashboard(session.sessionCode);

  // La liste part du roster complet (tous les candidats inscrits, même jamais connectés) ; le
  // statut temps réel (GetSnapshot, en mémoire côté serveur) est ensuite fusionné par-dessus pour
  // ceux qui se sont déjà connectés depuis le dernier démarrage du serveur.
  const roster = await hub.getCandidateRoster(session.sessionCode);
  for (const entry of roster) {
    candidates.set(entry.candidateId, {
      candidateId: entry.candidateId,
      name: entry.name,
      hasConnectedOnce: entry.hasConnectedOnce,
      isConnected: false,
      isReady: null,
      failureReason: null,
      failureDetail: null,
      lastSeenAt: null,
      lastScreenshotAt: null
    });
  }

  const snapshot = await hub.getSnapshot(session.sessionCode);
  for (const status of snapshot) {
    upsertCandidate(status, { render: false });
  }

  currentSessionTitle.textContent = `Session ${session.sessionCode}`;
  downloadScreenshotsButton.href = `/api/sessions/${encodeURIComponent(session.sessionCode)}/screenshots.zip`;
  renderCandidates();
  renderSessionDetail();

  sessionPickerSection.hidden = true;
  sessionDetailSection.hidden = false;
}

export function renderSessionDetail(): void {
  if (!currentSession) {
    return;
  }

  const running = Boolean(currentSession.startedAt) && !currentSession.endedAt;
  const ended = Boolean(currentSession.endedAt);

  currentSessionStatus.className = "session-live-status" + (running ? "" : " not-running") + (ended ? " ended" : "");
  currentSessionStatus.innerHTML = "";

  if (running) {
    const dot = document.createElement("span");
    dot.className = "dot";
    currentSessionStatus.append(
      dot,
      document.createTextNode(`DÉMARRÉE À ${new Date(currentSession.startedAt!).toLocaleTimeString()}`)
    );
  } else if (ended) {
    currentSessionStatus.textContent = `TERMINÉE À ${new Date(currentSession.endedAt!).toLocaleTimeString()}`;
  } else {
    currentSessionStatus.textContent = "non démarrée";
  }

  startButton.disabled = running;
  startButton.textContent = ended ? "↻ REDÉMARRER L'ÉPREUVE" : "▶ DÉMARRER L'ÉPREUVE";
  stopButton.disabled = !running;
}
