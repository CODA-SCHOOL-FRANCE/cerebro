import {
  createSessionCodeInput,
  createSessionError,
  createSessionForm,
  createSessionOverlay,
  createSessionRosterFileInput,
  createSessionRosterJsonInput
} from "./dom.js";
import type { CerebroHubClient } from "./hub-client.js";

export function openCreateSessionForm(): void {
  createSessionForm.reset();
  hideError();
  createSessionOverlay.hidden = false;
  createSessionCodeInput.focus();
}

export function closeCreateSessionForm(): void {
  createSessionOverlay.hidden = true;
}

export function initCreateSessionForm(hub: CerebroHubClient, onCreated: () => void): void {
  createSessionRosterFileInput.addEventListener("change", () => {
    const file = createSessionRosterFileInput.files?.[0];
    if (!file) {
      return;
    }
    file.text()
      .then((text) => {
        createSessionRosterJsonInput.value = text;
      })
      .catch((err: unknown) => console.error("Lecture du fichier roster échouée", err));
  });

  createSessionForm.addEventListener("submit", (event) => {
    event.preventDefault();
    submitCreateSession(hub, onCreated).catch((err: unknown) => {
      console.error("Création de session échouée", err);
      showError("Impossible de créer la session.");
    });
  });
}

async function submitCreateSession(hub: CerebroHubClient, onCreated: () => void): Promise<void> {
  hideError();

  const sessionCode = createSessionCodeInput.value.trim();
  const rosterJson = createSessionRosterJsonInput.value.trim();

  if (!sessionCode || !rosterJson) {
    showError("Code de session et roster JSON sont obligatoires.");
    return;
  }

  try {
    await hub.createSession(sessionCode, rosterJson);
  } catch (err: unknown) {
    showError(hubExceptionMessage(err));
    return;
  }

  closeCreateSessionForm();
  onCreated();
}

// Les erreurs métier (session déjà existante, JSON invalide, roster vide...) sont levées côté
// serveur comme HubException (voir ExamProvisioner.cs / CerebroHub.CreateSession) avec un message
// déjà destiné à un humain — mais le client SignalR l'enveloppe dans
// "An unexpected error occurred invoking '...' on the server. HubException: <message>", vérifié
// empiriquement (pas documenté). On extrait la partie utile ; si le format change ou qu'il s'agit
// d'une erreur de connexion (pas de préfixe "HubException:"), on retombe sur le message complet.
function hubExceptionMessage(err: unknown): string {
  if (!(err instanceof Error)) {
    return "Erreur inconnue.";
  }
  const match = /HubException:\s*(.+)$/.exec(err.message);
  return match?.[1] ?? err.message;
}

function showError(message: string): void {
  createSessionError.textContent = message;
  createSessionError.hidden = false;
}

function hideError(): void {
  createSessionError.hidden = true;
}
