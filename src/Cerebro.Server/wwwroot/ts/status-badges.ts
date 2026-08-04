import type { ExamSessionSummaryDto, MergedCandidate } from "./types.js";

export interface BadgeInfo {
  className: string;
  label: string;
}

export function candidateReadinessBadge(candidate: MergedCandidate): BadgeInfo {
  if (candidate.isReady === true) {
    return { className: "badge-ready", label: "PRÊT" };
  }
  if (candidate.isReady === false) {
    return {
      className: "badge-failed",
      label: `ÉCHEC (${candidate.failureReason}) ${candidate.failureDetail ?? ""}`
    };
  }
  return { className: "badge-pending", label: "EN ATTENTE" };
}

export function sessionStatusBadge(session: ExamSessionSummaryDto): BadgeInfo {
  if (session.endedAt) {
    return { className: "badge-ended", label: `TERMINÉE À ${new Date(session.endedAt).toLocaleTimeString()}` };
  }
  if (session.startedAt) {
    return { className: "badge-started", label: `DÉMARRÉE À ${new Date(session.startedAt).toLocaleTimeString()}` };
  }
  return { className: "badge-planned", label: "PLANIFIÉE (NON DÉMARRÉE)" };
}
