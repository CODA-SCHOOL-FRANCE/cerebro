import type {
  CandidateRosterEntryDto,
  CandidateStatusDto,
  ExamSessionSummaryDto,
  SessionActivityEventDto
} from "./types.js";

export type CandidateEventHandler = (status: CandidateStatusDto) => void;

// Encapsule la connexion SignalR brute (global `signalR`, chargé en UMD) derrière une API
// typée : c'est le seul module qui touche l'objet global `signalR`.
export interface CerebroHubClient {
  start(): Promise<void>;
  getPlannedSessions(): Promise<ExamSessionSummaryDto[]>;
  getCandidateRoster(sessionCode: string): Promise<CandidateRosterEntryDto[]>;
  getSnapshot(sessionCode: string): Promise<CandidateStatusDto[]>;
  getSessionActivity(sessionCode: string): Promise<SessionActivityEventDto[]>;
  createSession(sessionCode: string, rosterJson: string): Promise<number>;
  createSessionFromNames(sessionCode: string, studentNames: string[]): Promise<CandidateRosterEntryDto[]>;
  joinAsDashboard(sessionCode: string): Promise<void>;
  startSession(sessionCode: string): Promise<void>;
  stopSession(sessionCode: string): Promise<void>;
  deleteSession(sessionCode: string): Promise<void>;
  onCandidateJoined(handler: CandidateEventHandler): void;
  onCandidateReadinessUpdated(handler: CandidateEventHandler): void;
  onCandidateHeartbeat(handler: CandidateEventHandler): void;
  onCandidateDisconnected(handler: CandidateEventHandler): void;
  onScreenshotReceived(handler: (candidateId: string, timestamp: string) => void): void;
  onSessionStarted(handler: (sessionCode: string, startedAt: string) => void): void;
  onSessionEnded(handler: (sessionCode: string, endedAt: string) => void): void;
}

export function createHubClient(): CerebroHubClient {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/cerebro")
    .withAutomaticReconnect()
    .build();

  return {
    async start() {
      if (connection.state === signalR.HubConnectionState.Disconnected) {
        await connection.start();
      }
    },
    getPlannedSessions: () => connection.invoke<ExamSessionSummaryDto[]>("GetPlannedSessions"),
    getCandidateRoster: (sessionCode) =>
      connection.invoke<CandidateRosterEntryDto[]>("GetCandidateRoster", sessionCode),
    getSnapshot: (sessionCode) => connection.invoke<CandidateStatusDto[]>("GetSnapshot", sessionCode),
    getSessionActivity: (sessionCode) =>
      connection.invoke<SessionActivityEventDto[]>("GetSessionActivity", sessionCode),
    createSession: (sessionCode, rosterJson) =>
      connection.invoke<number>("CreateSession", sessionCode, rosterJson),
    createSessionFromNames: (sessionCode, studentNames) =>
      connection.invoke<CandidateRosterEntryDto[]>("CreateSessionFromNames", sessionCode, studentNames),
    joinAsDashboard: (sessionCode) => connection.invoke("JoinAsDashboard", sessionCode),
    startSession: (sessionCode) => connection.invoke("StartSession", sessionCode),
    stopSession: (sessionCode) => connection.invoke("StopSession", sessionCode),
    deleteSession: (sessionCode) => connection.invoke("DeleteSession", sessionCode),
    onCandidateJoined: (handler) => connection.on("CandidateJoined", handler),
    onCandidateReadinessUpdated: (handler) => connection.on("CandidateReadinessUpdated", handler),
    onCandidateHeartbeat: (handler) => connection.on("CandidateHeartbeat", handler),
    onCandidateDisconnected: (handler) => connection.on("CandidateDisconnected", handler),
    onScreenshotReceived: (handler) => connection.on("ScreenshotReceived", handler),
    onSessionStarted: (handler) => connection.on("SessionStarted", handler),
    onSessionEnded: (handler) => connection.on("SessionEnded", handler)
  };
}
