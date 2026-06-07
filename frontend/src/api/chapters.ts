import { api } from "./client"
import type { ChapteredContent, PublicationReadiness } from "@/types/chapters"

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
}
