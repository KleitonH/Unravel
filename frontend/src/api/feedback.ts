import { api } from "./client"
import type { ChallengeFeedback } from "@/types/chapters"

/**
 * Bandeirinhas — feedback de qualidade das perguntas.
 *
 * - `submit`: aluno sinaliza uma pergunta inadequada (do quiz).
 * - `listForQuestion` / `resolve`: moderador vê o histórico e tria.
 *
 * `reason`/`status` viajam como **inteiros** (índice do enum do backend),
 * não string — o binder do ASP.NET liga enum a número por padrão.
 */
export const feedbackApi = {
  // Aluno — POST /api/challenges/{id}/feedback
  submit: (challengeId: number, body: { reason: number; comment?: string | null }) =>
    api
      .post<{ id: number; updated: boolean }>(
        `/api/challenges/${challengeId}/feedback`,
        body,
      )
      .then((r) => r.data),

  // Moderador — GET /api/admin/questions/{id}/feedback
  listForQuestion: (challengeId: number) =>
    api
      .get<ChallengeFeedback[]>(`/api/admin/questions/${challengeId}/feedback`)
      .then((r) => r.data),

  // Moderador — PATCH /api/admin/feedback/{id}  (status: 1=Revisado, 2=Descartado)
  resolve: (feedbackId: number, status: 1 | 2) =>
    api
      .patch<{ id: number; status: string; reviewedAt: string }>(
        `/api/admin/feedback/${feedbackId}`,
        { status },
      )
      .then((r) => r.data),
}

/** Tipos de problema que o aluno pode apontar. `value` = índice do enum
 *  `FeedbackReason` no backend. Ordem = ordem de exibição no diálogo. */
export const FEEDBACK_REASONS: {
  value: number
  key: string
  label: string
  hint: string
}[] = [
  { value: 0, key: "GabaritoErrado",  label: "O gabarito parece errado",       hint: "A alternativa marcada como certa não é a correta." },
  { value: 1, key: "Ambigua",         label: "Pergunta ambígua ou confusa",    hint: "O enunciado dá margem a mais de uma interpretação." },
  { value: 2, key: "MultiplaCorreta", label: "Mais de uma alternativa correta", hint: "Duas ou mais opções poderiam ser aceitas." },
  { value: 3, key: "ForaDoConteudo",  label: "Fora do conteúdo estudado",      hint: "O assunto não foi coberto neste material." },
  { value: 4, key: "Outro",           label: "Outro problema",                 hint: "Descreva no comentário (obrigatório)." },
]

/** Rótulos legíveis por chave de enum — pro painel do moderador. */
export const FEEDBACK_REASON_LABELS: Record<string, string> = Object.fromEntries(
  FEEDBACK_REASONS.map((r) => [r.key, r.label]),
)
