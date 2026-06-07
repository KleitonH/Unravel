/**
 * Types pra os endpoints de tracking de batch do forge (PR 52a-1 backend).
 *
 * Batch = uma chamada do moderador a POST /forge/{id} ou POST /forge/bulk.
 * Cada batch agrupa N jobs (1 por claim extraída do conteúdo); o painel
 * `<ForgeActivityPanel />` (PR 52a-2) consome esses tipos pra mostrar
 * progresso em real-time enquanto o worker processa.
 */

import type { QuestionShape } from "./api"

export type ForgeJobStatus = "Pending" | "Running" | "Done" | "Failed"
export type ForgeJobPriority = "Normal" | "Urgent"

/** Resumo agregado de um batch (lista no drawer). */
export type ForgeBatchSummary = {
  batchId:     string
  total:       number
  pending:     number
  running:     number
  done:        number
  failed:      number
  enqueuedAt:  string  // ISO
  lastUpdate:  string  // ISO
}

/** Job individual dentro de um batch (drill-down). */
export type ForgeBatchJob = {
  id:                   number
  contentId:            number
  contentTitle:         string
  status:               ForgeJobStatus
  priority:             ForgeJobPriority
  enqueuedAt:           string
  startedAt:            string | null
  completedAt:          string | null
  attemptCount:         number
  lastError:            string | null
  claimText:            string
  generatedChallengeId: number | null
  /** Presente apenas quando o job teve sucesso e a challenge foi
   * persistida; vem do BodyJson da generated_challenge. */
  shape:                QuestionShape | null
  prompt:               string | null
}

/** Detalhe completo de UM batch (response de GET /batches/{id}). */
export type ForgeBatchDetail = {
  batchId:        string
  total:          number
  enqueuedAt:     string
  isComplete:     boolean
  counts:         { pending: number; running: number; done: number; failed: number }
  shapeBreakdown: Partial<Record<QuestionShape, number>>
  jobs:           ForgeBatchJob[]
}
