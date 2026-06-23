import { api } from "./client"

export type ArenaMatch = {
  id:                 number
  status:             string // Pending | Active | Finished | Declined | Cancelled
  trailId:            number
  player1Id:          string
  player1Name:        string
  player2Id:          string | null
  player2Name:        string | null
  score1:             number
  score2:             number
  winnerId:           string | null
  currentRoundIndex:  number
  totalRounds:        number
  secondsPerQuestion: number
}

export type ArenaRound = {
  orderIndex:         number
  total:              number
  prompt:             string
  options:            string[]
  shape:              string
  secondsPerQuestion: number
}

export type ArenaRoundResult = {
  orderIndex:   number
  correctIndex: number
  score1:       number
  score2:       number
  finished:     boolean
  winnerId:     string | null
}

export type ArenaRankingRow = {
  rank:        number
  userId:      string
  displayName: string
  points:      number
  wins:        number
  losses:      number
  draws:       number
}

export type EnqueueResult = { matched: boolean; matchId: number | null }

export const arenaApi = {
  enqueue: (trailId: number) =>
    api.post<EnqueueResult>("/api/arena/queue", { trailId }).then((r) => r.data),

  leaveQueue: () => api.delete("/api/arena/queue").then((r) => r.data),

  challenge: (opponentId: string, trailId: number) =>
    api.post<{ matchId: number }>("/api/arena/challenge", { opponentId, trailId }).then((r) => r.data),

  accept:  (id: number) => api.put<{ matchId: number }>(`/api/arena/matches/${id}/accept`, {}).then((r) => r.data),
  decline: (id: number) => api.put(`/api/arena/matches/${id}/decline`, {}).then((r) => r.data),

  match: (id: number) =>
    api.get<ArenaMatch>(`/api/arena/matches/${id}`).then((r) => r.data),

  ranking: (top = 20) =>
    api.get<ArenaRankingRow[]>("/api/arena/ranking", { params: { top } }).then((r) => r.data),

  /** Partidas ativas + desafios pendentes do usuário. */
  myMatches: () =>
    api.get<ArenaMatch[]>("/api/arena/my-matches").then((r) => r.data),
}
