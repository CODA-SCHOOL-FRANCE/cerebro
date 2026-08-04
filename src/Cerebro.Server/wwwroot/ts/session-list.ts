import { hudSessionCount, refreshFlash, refreshFlashTime, sessionList } from "./dom.js";
import { setPlannedSessionCount } from "./state.js";
import { sessionStatusBadge } from "./status-badges.js";
import type { ExamSessionSummaryDto } from "./types.js";

let refreshFlashTimeout: number | undefined;

export function renderSessionList(
  sessions: ExamSessionSummaryDto[],
  onSelect: (session: ExamSessionSummaryDto) => void
): void {
  sessionList.innerHTML = "";
  setPlannedSessionCount(sessions.length);
  hudSessionCount.textContent = String(sessions.length);

  if (sessions.length === 0) {
    const empty = document.createElement("div");
    empty.className = "empty-hint";
    empty.textContent = "Aucune épreuve planifiée pour le moment.";
    sessionList.append(empty);
    return;
  }

  for (const session of sessions) {
    const { className, label } = sessionStatusBadge(session);
    const row = document.createElement("div");
    row.className = "session-row";

    const idCell = document.createElement("div");
    const idLine = document.createElement("div");
    idLine.className = "session-row-id";
    idLine.textContent = session.sessionCode;
    idCell.append(idLine);

    const countCell = document.createElement("div");
    countCell.className = "session-row-count";
    countCell.textContent = `👤 ${session.candidateCount}`;

    const statusCell = document.createElement("div");
    const statusBadge = document.createElement("span");
    statusBadge.className = "badge " + className;
    statusBadge.textContent = label;
    statusCell.append(statusBadge);

    const createdCell = document.createElement("div");
    createdCell.className = "session-row-created";
    createdCell.textContent = new Date(session.createdAt).toLocaleString();

    const actionCell = document.createElement("div");
    const selectButton = document.createElement("button");
    selectButton.className = "btn btn-teal";
    selectButton.textContent = "▶ SÉLECTIONNER";
    selectButton.addEventListener("click", () => onSelect(session));
    actionCell.append(selectButton);

    row.append(idCell, countCell, statusCell, createdCell, actionCell);
    sessionList.append(row);
  }
}

export function flashRefreshConfirmation(): void {
  refreshFlashTime.textContent = new Date().toLocaleTimeString();
  refreshFlash.hidden = false;
  window.clearTimeout(refreshFlashTimeout);
  refreshFlashTimeout = window.setTimeout(() => {
    refreshFlash.hidden = true;
  }, 2500);
}
