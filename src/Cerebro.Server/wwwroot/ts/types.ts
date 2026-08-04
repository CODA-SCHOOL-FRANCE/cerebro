// Miroir des DTOs C# (Cerebro.Shared/Realtime) tels que sérialisés en JSON par SignalR.
// Les dates restent des chaînes ISO côté client ; elles ne sont converties en Date qu'à l'affichage.

export interface ExamSessionSummaryDto {
  sessionCode: string;
  candidateCount: number;
  createdAt: string;
  startedAt: string | null;
  endedAt: string | null;
}

export interface CandidateRosterEntryDto {
  candidateId: string;
  name: string;
  hasConnectedOnce: boolean;
}

export interface CandidateStatusDto {
  candidateId: string;
  isConnected: boolean;
  isReady: boolean | null;
  failureReason: string | null;
  failureDetail: string | null;
  connectedAt: string;
  lastSeenAt: string;
  lastScreenshotAt: string | null;
}

export interface SessionActivityEventDto {
  timestamp: string;
  eventType: string;
  candidateId: string | null;
  detail: string | null;
}

// Vue fusionnée côté client : roster complet (persisté, tous les candidats inscrits) +
// statut temps réel (en mémoire côté serveur, seulement pour ceux déjà connectés).
export interface MergedCandidate {
  candidateId: string;
  name: string;
  hasConnectedOnce: boolean;
  isConnected: boolean;
  isReady: boolean | null;
  failureReason: string | null;
  failureDetail: string | null;
  lastSeenAt: string | null;
  lastScreenshotAt: string | null;
}
