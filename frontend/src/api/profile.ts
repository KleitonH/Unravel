import { api } from "@/api/client"
import type { Profile, RankingEntry, StudentProfile } from "@/types/api"

/**
 * Profile API (PR 26).
 *
 * <para><c>GET /api/profile</c> retorna shape diferente por role
 * (Student vs Moderator). Mantemos uma única call e narrow no consumer
 * via discriminated union (<c>role</c>).</para>
 */
export const profileApi = {
  /** Retorna o profile do usuário autenticado.
   *  Student: xp/coins/stars/lives/streak/badges/cosmetics/trailProgress.
   *  Moderator: metrics globais + lista de trilhas (sem XP pessoal). */
  me: () =>
    api.get<Profile>("/api/profile").then((r) => r.data),

  /** Helper só pra Student — narrow do union. Lança se role != Student. */
  meStudent: () =>
    api.get<Profile>("/api/profile").then((r) => {
      if (r.data.role !== "Student") {
        throw new Error("Endpoint chamado por não-Student; rota errada?")
      }
      return r.data as StudentProfile
    }),

  /** Top-N ranking global por XP. Default 10. */
  ranking: (top = 10) =>
    api.get<RankingEntry[]>("/api/profile/ranking", { params: { top } }).then((r) => r.data),

  /** Atualiza título exibido no perfil; null limpa. */
  updateTitle: (title: string | null) =>
    api.put<{ activeTitle: string | null }>("/api/profile/title", { title }).then((r) => r.data),

  /** Equipa um cosmético do inventário do user. */
  equipCosmetic: (cosmeticId: number) =>
    api.put<{ message: string }>(`/api/profile/cosmetic/${cosmeticId}/equip`, {}).then((r) => r.data),
}
