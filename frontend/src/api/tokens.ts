import { api } from "./client"
import type { TokenBalance, TokenTransaction } from "@/types/tokens"

/** PR 52 — endpoints do saldo de lã do moderador. */
export const tokensApi = {
  /** Saldo + tier + display formatado. Garante welcome bonus
   *  no primeiro chamado. */
  balance: () =>
    api.get<TokenBalance>("/api/tokens/balance").then((r) => r.data),

  /** Histórico de transações (paginado). */
  history: (take = 30, skip = 0) =>
    api
      .get<TokenTransaction[]>("/api/tokens/history", { params: { take, skip } })
      .then((r) => r.data),
}
