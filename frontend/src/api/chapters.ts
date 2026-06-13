import { api } from "./client"
import type {
  AuthorQuestionRequest,
  ChapteredContent,
  ContentQuestion,
  PublicationReadiness,
} from "@/types/chapters"

/**
 * Endpoints de Capítulos (PR 60-a backend).
 *
 * - `get`: público pro aluno autenticado — consumido pelo
 *   `<ChapteredQuizPage />` (estudo guiado).
 * - `readiness`: escopo admin — alimenta badges e gate de publish na
 *   trail-detail-page.
 */
export const chaptersApi = {
  get: (contentId: number, opts?: { min?: number; max?: number }) =>
    api.get<ChapteredContent>(`/api/contents/${contentId}/chapters`, {
      params: { minPerChapter: opts?.min ?? 4, maxPerChapter: opts?.max ?? 7 },
    }).then((r) => r.data),

  readiness: (contentId: number) =>
    api.get<PublicationReadiness>(
      `/api/admin/contents/${contentId}/publication-readiness`,
    ).then((r) => r.data),

  // ── PR 60-f — perguntas por capítulo (escopo admin) ──────────────

  /** Lista TODAS as perguntas ativas do content (geradas + autorais),
   *  com chunkIndex — pra agrupar por capítulo. Sem cap de 20. */
  listQuestions: (contentId: number) =>
    api.get<ContentQuestion[]>(
      `/api/admin/contents/${contentId}/questions`,
    ).then((r) => r.data),

  /** Cria pergunta autoral (vira GeneratedChallenge ModeratorAuthored). */
  createQuestion: (contentId: number, body: AuthorQuestionRequest) =>
    api.post<{ id: number; chunkIndex: number }>(
      `/api/admin/contents/${contentId}/questions`, body,
    ).then((r) => r.data),

  /** Edita pergunta autoral existente. */
  updateQuestion: (contentId: number, challengeId: number, body: AuthorQuestionRequest) =>
    api.put<{ id: number; chunkIndex: number }>(
      `/api/admin/contents/${contentId}/questions/${challengeId}`, body,
    ).then((r) => r.data),

  /** Desativa (soft-delete) qualquer pergunta do pool — autoral ou IA. */
  deleteQuestion: (contentId: number, challengeId: number) =>
    api.delete<void>(
      `/api/admin/contents/${contentId}/questions/${challengeId}`,
    ).then((r) => r.data),
}
