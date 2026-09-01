import {
  createSessionCodeInput,
  createSessionError,
  createSessionForm,
  createSessionJsonPanel,
  createSessionManualPanel,
  createSessionNamesInput,
  createSessionOverlay,
  createSessionResultsCopyButton,
  createSessionResultsDoneButton,
  createSessionResultsList,
  createSessionResultsPanel,
  createSessionRosterFileInput,
  createSessionRosterJsonInput,
  createSessionTabJson,
  createSessionTabManual
} from "./dom.js";
import type { CerebroHubClient } from "./hub-client.js";
import type { CandidateRosterEntryDto } from "./types.js";

type CreateSessionMode = "manual" | "json";

let mode: CreateSessionMode = "manual";

export function openCreateSessionForm(): void {
  createSessionForm.reset();
  hideError();
  setMode("manual");
  createSessionResultsPanel.hidden = true;
  createSessionForm.hidden = false;
  createSessionOverlay.hidden = false;
  createSessionCodeInput.focus();
}

export function closeCreateSessionForm(): void {
  createSessionOverlay.hidden = true;
}

export function initCreateSessionForm(hub: CerebroHubClient, onCreated: () => void): void {
  createSessionTabManual.addEventListener("click", () => setMode("manual"));
  createSessionTabJson.addEventListener("click", () => setMode("json"));

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
    const submit = mode === "manual" ? submitCreateSessionManual(hub, onCreated) : submitCreateSessionJson(hub, onCreated);
    submit.catch((err: unknown) => {
      console.error("Création de session échouée", err);
      showError("Impossible de créer la session.");
    });
  });

  createSessionResultsCopyButton.addEventListener("click", () => {
    copyResultsToClipboard().catch((err: unknown) => console.error("Copie de la liste échouée", err));
  });

  createSessionResultsDoneButton.addEventListener("click", () => {
    closeCreateSessionForm();
  });
}

function setMode(next: CreateSessionMode): void {
  mode = next;
  createSessionTabManual.classList.toggle("active", next === "manual");
  createSessionTabManual.setAttribute("aria-selected", String(next === "manual"));
  createSessionTabJson.classList.toggle("active", next === "json");
  createSessionTabJson.setAttribute("aria-selected", String(next === "json"));
  createSessionManualPanel.hidden = next !== "manual";
  createSessionJsonPanel.hidden = next !== "json";
}

async function submitCreateSessionManual(hub: CerebroHubClient, onCreated: () => void): Promise<void> {
  hideError();

  const sessionCode = createSessionCodeInput.value.trim();
  const studentNames = createSessionNamesInput.value
    .split("\n")
    .map((name) => name.trim())
    .filter((name) => name.length > 0);

  if (!sessionCode || studentNames.length === 0) {
    showError("Code de session et au moins un étudiant sont obligatoires.");
    return;
  }

  let candidates: CandidateRosterEntryDto[];
  try {
    candidates = await hub.createSessionFromNames(sessionCode, studentNames);
  } catch (err: unknown) {
    showError(hubExceptionMessage(err));
    return;
  }

  onCreated();
  showResults(candidates);
}

async function submitCreateSessionJson(hub: CerebroHubClient, onCreated: () => void): Promise<void> {
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

// Seule occasion d'afficher les identifiants générés côté serveur (ExamProvisioner.ProvisionFromNamesAsync) :
// ils ne sont pas restockés ailleurs dans l'UI du dashboard, à noter/copier avant de fermer.
function showResults(candidates: CandidateRosterEntryDto[]): void {
  createSessionForm.hidden = true;
  createSessionResultsList.innerHTML = "";

  for (const candidate of candidates) {
    const row = document.createElement("div");
    row.className = "create-session-result-row";

    const name = document.createElement("span");
    name.className = "name";
    name.textContent = candidate.name;

    const id = document.createElement("span");
    id.className = "id";
    id.textContent = candidate.candidateId;

    row.append(name, id);
    createSessionResultsList.append(row);
  }

  createSessionResultsPanel.hidden = false;
}

async function copyResultsToClipboard(): Promise<void> {
  const rows = createSessionResultsList.querySelectorAll<HTMLElement>(".create-session-result-row");
  const lines = Array.from(rows).map((row) => {
    const name = row.querySelector(".name")?.textContent ?? "";
    const id = row.querySelector(".id")?.textContent ?? "";
    return `${name}\t${id}`;
  });
  await navigator.clipboard.writeText(lines.join("\n"));
}

// Les erreurs métier (session déjà existante, JSON invalide, session en cours...) sont levées
// côté serveur comme HubException (voir ExamProvisioner.cs / CerebroHub) avec un message déjà
// destiné à un humain — mais le client SignalR l'enveloppe dans "An unexpected error occurred
// invoking '...' on the server. HubException: <message>", vérifié empiriquement (pas documenté).
// On extrait la partie utile ; si le format change ou qu'il s'agit d'une erreur de connexion (pas
// de préfixe "HubException:"), on retombe sur le message complet. Réutilisé par tout appel de hub
// dont l'échec doit remonter un message lisible côté dashboard (voir main.ts : deleteSession).
export function hubExceptionMessage(err: unknown): string {
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
