import {
  closeScreenshotViewerButton,
  screenshotViewerEmpty,
  screenshotViewerImage,
  screenshotViewerOverlay,
  screenshotViewerTime,
  screenshotViewerTitle
} from "./dom.js";
import { candidates, currentSession } from "./state.js";

// Candidat dont le dernier screenshot est affiché, pour rafraîchir l'image à chaque
// ScreenshotReceived le concernant tant que la fenêtre reste ouverte.
let viewedCandidateId: string | null = null;

export function openScreenshotViewer(candidateId: string): void {
  viewedCandidateId = candidateId;
  screenshotViewerOverlay.hidden = false;
  loadLatestScreenshot();
}

export function closeScreenshotViewer(): void {
  viewedCandidateId = null;
  screenshotViewerOverlay.hidden = true;
  screenshotViewerImage.removeAttribute("src");
}

export function refreshScreenshotViewerIfShowing(candidateId: string): void {
  if (viewedCandidateId === candidateId) {
    loadLatestScreenshot();
  }
}

function loadLatestScreenshot(): void {
  if (!currentSession || !viewedCandidateId) {
    return;
  }

  const candidate = candidates.get(viewedCandidateId);
  screenshotViewerTitle.textContent = candidate?.name ?? viewedCandidateId;
  screenshotViewerTime.textContent = candidate?.lastScreenshotAt
    ? `capturé à ${new Date(candidate.lastScreenshotAt).toLocaleTimeString()}`
    : "";

  // L'URL serveur est fixe (toujours "la dernière") : le paramètre t force le navigateur à
  // refaire la requête à chaque nouvelle capture au lieu de réafficher l'ancienne image.
  const url = `/api/sessions/${encodeURIComponent(currentSession.sessionCode)}` +
    `/candidates/${encodeURIComponent(viewedCandidateId)}/latest-screenshot?t=${Date.now()}`;
  screenshotViewerImage.src = url;
}

screenshotViewerImage.addEventListener("load", () => {
  screenshotViewerImage.hidden = false;
  screenshotViewerEmpty.hidden = true;
});

screenshotViewerImage.addEventListener("error", () => {
  if (!screenshotViewerImage.getAttribute("src")) {
    return;
  }
  screenshotViewerImage.hidden = true;
  screenshotViewerEmpty.hidden = false;
});

closeScreenshotViewerButton.addEventListener("click", () => closeScreenshotViewer());

screenshotViewerOverlay.addEventListener("click", (event) => {
  if (event.target === screenshotViewerOverlay) {
    closeScreenshotViewer();
  }
});
