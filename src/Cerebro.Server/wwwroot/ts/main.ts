import { loadActivity, refreshActivityIfVisible } from "./activity-log.js";
import { setCandidateScreenshotTimestamp, upsertCandidate } from "./candidate-list.js";
import { closeCreateSessionForm, initCreateSessionForm, openCreateSessionForm } from "./create-session.js";
import {
  activityOverlay,
  backButton,
  closeActivityButton,
  closeCreateSessionButton,
  createSessionButton,
  createSessionOverlay,
  footerYear,
  hudOnlineCount,
  logoutButton,
  refreshButton,
  sessionDetailSection,
  sessionPickerSection,
  startButton,
  stopButton,
  toggleActivityButton
} from "./dom.js";
import { createHubClient } from "./hub-client.js";
import { flashRefreshConfirmation, renderSessionList } from "./session-list.js";
import { renderSessionDetail, selectSession } from "./session-detail.js";
import { currentSession, setCurrentSession } from "./state.js";
import type { ExamSessionSummaryDto } from "./types.js";

const hub = createHubClient();

hub.onCandidateJoined((status) => {
  upsertCandidate(status);
  refreshActivityIfVisible(hub);
});
hub.onCandidateReadinessUpdated((status) => {
  upsertCandidate(status);
  refreshActivityIfVisible(hub);
});
hub.onCandidateHeartbeat((status) => upsertCandidate(status));
hub.onCandidateDisconnected((status) => {
  upsertCandidate(status);
  refreshActivityIfVisible(hub);
});

hub.onScreenshotReceived((candidateId, timestamp) => {
  setCandidateScreenshotTimestamp(candidateId, timestamp);
  refreshActivityIfVisible(hub);
});

hub.onSessionStarted((sessionCode, startedAt) => {
  if (currentSession?.sessionCode === sessionCode) {
    currentSession.startedAt = startedAt;
    currentSession.endedAt = null;
    renderSessionDetail();
    refreshActivityIfVisible(hub);
  }
});

hub.onSessionEnded((sessionCode, endedAt) => {
  if (currentSession?.sessionCode === sessionCode) {
    currentSession.endedAt = endedAt;
    renderSessionDetail();
    refreshActivityIfVisible(hub);
  }
});

async function loadSessions(): Promise<void> {
  await hub.start();
  const sessions = await hub.getPlannedSessions();
  renderSessionList(sessions, (session: ExamSessionSummaryDto) => {
    selectSession(hub, session).catch((err: unknown) => console.error("Sélection de session échouée", err));
  });
}

toggleActivityButton.addEventListener("click", () => {
  activityOverlay.hidden = false;
  loadActivity(hub).catch((err: unknown) => console.error("Chargement du journal échoué", err));
});

closeActivityButton.addEventListener("click", () => {
  activityOverlay.hidden = true;
});

activityOverlay.addEventListener("click", (event) => {
  if (event.target === activityOverlay) {
    activityOverlay.hidden = true;
  }
});

backButton.addEventListener("click", () => {
  setCurrentSession(null);
  sessionDetailSection.hidden = true;
  sessionPickerSection.hidden = false;
  hudOnlineCount.textContent = "—";
  loadSessions().catch((err: unknown) => console.error("Chargement des sessions échoué", err));
});

startButton.addEventListener("click", () => {
  if (!currentSession) {
    return;
  }
  hub.startSession(currentSession.sessionCode)
    .catch((err: unknown) => console.error("Démarrage de session échoué", err));
});

stopButton.addEventListener("click", () => {
  if (!currentSession) {
    return;
  }
  hub.stopSession(currentSession.sessionCode)
    .catch((err: unknown) => console.error("Arrêt de session échoué", err));
});

refreshButton.addEventListener("click", () => {
  loadSessions()
    .then(() => flashRefreshConfirmation())
    .catch((err: unknown) => console.error("Chargement des sessions échoué", err));
});

initCreateSessionForm(hub, () => {
  loadSessions()
    .then(() => flashRefreshConfirmation())
    .catch((err: unknown) => console.error("Chargement des sessions échoué", err));
});

createSessionButton.addEventListener("click", () => openCreateSessionForm());

closeCreateSessionButton.addEventListener("click", () => closeCreateSessionForm());

createSessionOverlay.addEventListener("click", (event) => {
  if (event.target === createSessionOverlay) {
    closeCreateSessionForm();
  }
});

logoutButton.addEventListener("click", () => {
  fetch("/account/logout", { method: "POST" })
    .then(() => {
      window.location.href = "/login.html";
    })
    .catch((err: unknown) => console.error("Déconnexion échouée", err));
});

footerYear.textContent = String(new Date().getFullYear());

loadSessions().catch((err: unknown) => console.error("Connexion SignalR échouée", err));
