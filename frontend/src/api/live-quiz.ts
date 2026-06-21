import { api } from "./client"

export type LiveSession = {
  id:                   number
  joinCode:             string
  mode:                 string
  status:               string
  questionCount:        number
  secondsPerQuestion:   number
  showRankBetween:      boolean
  participantCount:     number
  currentQuestionIndex: number
}

export type LiveQuestion = {
  orderIndex:         number
  total:              number
  prompt:             string
  options:            string[]
  shape:              string
  secondsPerQuestion: number
}

export type LiveLeaderboardRow = {
  rank:        number
  userId:      string
  displayName: string
  score:       number
}

export type LiveQuestionResult = {
  orderIndex:   number
  correctIndex: number
  explanation:  string | null
}

export type CreateLiveQuizBody = {
  mode:                 "Turma" | "Livre"
  secondsPerQuestion:   number
  showRankBetween:      boolean
  shuffleQuestions:     boolean
  shuffleOptions:       boolean
  questionChallengeIds: number[]
  allowedUserIds:       string[]
}

export type LiveActiveSession = {
  id:            number
  joinCode:      string
  ownerName:     string
  status:        string
  questionCount: number
}

export const liveQuizApi = {
  create: (body: CreateLiveQuizBody) =>
    api.post<LiveSession>("/api/live-quiz", body).then((r) => r.data),

  get: (id: number) =>
    api.get<LiveSession>(`/api/live-quiz/${id}`).then((r) => r.data),

  byCode: (code: string) =>
    api.get<LiveSession>(`/api/live-quiz/by-code/${code}`).then((r) => r.data),

  /** Sessões de turma ativas que o aluno pode entrar (pra banner "Minhas turmas"). */
  activeForMe: () =>
    api.get<LiveActiveSession[]>("/api/live-quiz/active-for-me").then((r) => r.data),
}
