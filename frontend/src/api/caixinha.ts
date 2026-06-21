import { api } from "./client"
import type { CaixinhaDetail, CaixinhaEvent, CaixinhaEventDetail, CaixinhaSummary } from "@/types/api"

/**
 * PR 65 — cliente da Caixinha de Gatos (clã/grupo). Espelha o
 * CaixinhasController. `mine` retorna 204 (→ null) quando o usuário não
 * pertence a nenhuma caixinha.
 */
export const caixinhaApi = {
  mine: () =>
    api.get<CaixinhaDetail | "">("/api/caixinhas/mine").then((r) => (r.status === 204 ? null : (r.data as CaixinhaDetail))),

  browse: (q?: string) =>
    api.get<CaixinhaSummary[]>("/api/caixinhas", { params: q ? { q } : {} }).then((r) => r.data),

  leaderboard: (top = 10) =>
    api.get<CaixinhaSummary[]>("/api/caixinhas/leaderboard", { params: { top } }).then((r) => r.data),

  create: (name: string, emblem?: string) =>
    api.post<{ caixinhaId: number }>("/api/caixinhas", { name, emblem }).then((r) => r.data),

  join: (id: number) =>
    api.post<{ caixinhaId: number }>(`/api/caixinhas/${id}/join`, {}).then((r) => r.data),

  leave: () =>
    api.post<{ disbanded: boolean }>("/api/caixinhas/leave", {}).then((r) => r.data),

  kick: (targetUserId: string) =>
    api.delete(`/api/caixinhas/members/${targetUserId}`).then((r) => r.data),

  postMural: (text: string) =>
    api.post<{ ok: boolean }>("/api/caixinhas/mural", { text }).then((r) => r.data),

  // ── Eventos entre caixinhas (PR 65c) ──
  events: {
    list: () =>
      api.get<CaixinhaEvent[]>("/api/caixinhas/events").then((r) => r.data),

    detail: (id: number) =>
      api.get<CaixinhaEventDetail>(`/api/caixinhas/events/${id}`).then((r) => r.data),

    create: (body: { name: string; theme?: string; startsAt: string; endsAt: string }) =>
      api.post<{ eventId: number }>("/api/caixinhas/events", body).then((r) => r.data),

    join: (id: number) =>
      api.post<{ eventId: number }>(`/api/caixinhas/events/${id}/join`, {}).then((r) => r.data),
  },
}
