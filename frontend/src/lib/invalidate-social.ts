import type { QueryClient } from "@tanstack/react-query"

/**
 * Invalida as queries de progresso social afetadas por um submit de quiz/boss:
 * missões do dia, novelo das parcerias e meta da caixinha. Como uma missão
 * concluída credita novelo + clã, os três precisam re-buscar juntos pra a UI
 * refletir na hora (✓ na missão, novelo desenrolando, barra do clã subindo).
 */
export function invalidateSocialProgress(qc: QueryClient) {
  qc.invalidateQueries({ queryKey: ["quests", "today"] })
  qc.invalidateQueries({ queryKey: ["partnerships"] })
  qc.invalidateQueries({ queryKey: ["caixinha", "mine"] })
}
