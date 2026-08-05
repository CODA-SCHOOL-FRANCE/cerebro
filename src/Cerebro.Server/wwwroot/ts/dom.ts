// Centralise les lookups document.getElementById avec le bon type d'élément, pour éviter de
// disperser des casts `as HTML...Element` dans tous les autres modules.

function required<T extends Element>(id: string): T {
  const el = document.getElementById(id);
  if (!el) {
    throw new Error(`Élément #${id} introuvable dans le DOM.`);
  }
  return el as unknown as T;
}

export const sessionPickerSection = required<HTMLElement>("sessionPicker");
export const sessionList = required<HTMLDivElement>("sessionList");
export const refreshButton = required<HTMLButtonElement>("refreshButton");
export const refreshFlash = required<HTMLElement>("refreshFlash");
export const refreshFlashTime = required<HTMLElement>("refreshFlashTime");

export const createSessionButton = required<HTMLButtonElement>("createSessionButton");
export const createSessionOverlay = required<HTMLElement>("createSessionOverlay");
export const closeCreateSessionButton = required<HTMLButtonElement>("closeCreateSessionButton");
export const createSessionForm = required<HTMLFormElement>("createSessionForm");
export const createSessionCodeInput = required<HTMLInputElement>("createSessionCode");
export const createSessionRosterFileInput = required<HTMLInputElement>("createSessionRosterFile");
export const createSessionRosterJsonInput = required<HTMLTextAreaElement>("createSessionRosterJson");
export const createSessionError = required<HTMLElement>("createSessionError");

export const sessionDetailSection = required<HTMLElement>("sessionDetail");
export const currentSessionTitle = required<HTMLHeadingElement>("currentSessionTitle");
export const currentSessionStatus = required<HTMLElement>("currentSessionStatus");
export const backButton = required<HTMLButtonElement>("backButton");
export const startButton = required<HTMLButtonElement>("startButton");
export const stopButton = required<HTMLButtonElement>("stopButton");
export const candidateListEl = required<HTMLDivElement>("candidateList");
export const toggleActivityButton = required<HTMLButtonElement>("toggleActivityButton");
export const activityOverlay = required<HTMLElement>("activityOverlay");
export const closeActivityButton = required<HTMLButtonElement>("closeActivityButton");
export const activityListEl = required<HTMLDivElement>("activityList");

export const hudSessionCount = required<HTMLElement>("hudSessionCount");
export const hudOnlineCount = required<HTMLElement>("hudOnlineCount");
export const logoutButton = required<HTMLButtonElement>("logoutButton");
export const footerYear = required<HTMLElement>("footerYear");
