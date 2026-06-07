import { api } from "./client"
import type { ForgeBatchDetail, ForgeBatchSummary } from "@/types/forge"

/**
 * Cliente pros endpoints do PR 52a-1 (Forge Activity Panel).
 *
 * Convenções:
 * - `recentBatches` lista os últimos N batches do moderador autenticado.
 *   Polling refetch via TanStack staleTime curto (3-5s) quando o drawer
 *   está aberto OU quando há jobs ativos (pending/running > 0).
 * - `batch(id)` traz detalhes + lista de jobs com prompt/shape. Mais
 *   pesado; usar só quando o usuário expande um batch.
 */
export const forgeApi = {
  recentBatches: (take = 10) =>
    api.get<ForgeBatchSummary[]>("/api/admin/forge/batches/recent", {
      params: { take },
    }).then((r) => r.data),

  batch: (batchId: string) =>
    api.get<ForgeBatchDetail>(`/api/admin/forge/batches/${batchId}`)
       .then((r) => r.data),
}
