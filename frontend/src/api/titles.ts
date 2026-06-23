import { api } from "./client"

export type Title = {
  id:        number
  text:      string
  category:  string
  criterion: string // Manual | StreakDays | ArenaWins | XpTotal
  threshold: number
  owned:     boolean
  active:    boolean
}

export type GlobalRankingRow = {
  rank:        number
  userId:      string
  name:        string
  xp:          number
  activeTitle: string | null
}

export const titlesApi = {
  /** Catálogo de títulos com flags owned/active pro usuário. */
  list: () => api.get<Title[]>("/api/titles").then((r) => r.data),

  /** Ativa um título possuído (id=0 limpa o título ativo). */
  activate: (id: number) =>
    api.put<{ activated: number }>(`/api/titles/${id}/activate`, {}).then((r) => r.data),

  /** Concede os títulos já merecidos (streak/arena/xp). Idempotente. */
  evaluate: () =>
    api.post<{ granted: string[] }>("/api/titles/evaluate", {}).then((r) => r.data),

  /** Ranking global por XP. */
  globalRanking: (top = 20) =>
    api.get<GlobalRankingRow[]>("/api/ranking/global", { params: { top } }).then((r) => r.data),
}
