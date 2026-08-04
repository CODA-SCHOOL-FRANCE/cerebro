import type { ExamSessionSummaryDto, MergedCandidate } from "./types.js";

export const candidates = new Map<string, MergedCandidate>();

export let currentSession: ExamSessionSummaryDto | null = null;

export function setCurrentSession(session: ExamSessionSummaryDto | null): void {
  currentSession = session;
}

export let plannedSessionCount = 0;

export function setPlannedSessionCount(count: number): void {
  plannedSessionCount = count;
}
