import { api } from "./client"

export type ReportSessionItem = {
  id:               number
  joinCode:         string
  mode:             string
  status:           string
  createdAt:        string
  endedAt:          string | null
  participantCount: number
  questionCount:    number
}

export type ReportSummary = {
  sessionId:        number
  joinCode:         string
  mode:             string
  status:           string
  endedAt:          string | null
  participantCount: number
  questionCount:    number
  answersCount:     number
  overallAccuracy:  number // 0..1
  avgScore:         number
}

export type ReportQuestionRow = {
  orderIndex:         number
  prompt:             string
  correctIndex:       number
  totalAnswers:       number
  correctCount:       number
  accuracy:           number // 0..1
  avgMs:              number
  optionDistribution: number[]
}

export type ReportParticipantRow = {
  rank:         number
  userId:       string
  displayName:  string
  score:        number
  answered:     number
  correctCount: number
  accuracy:     number
}

export type ReportTopicRow = {
  topic:        string
  totalAnswers: number
  correctCount: number
  accuracy:     number
}

export type LiveQuizReport = {
  summary:      ReportSummary
  questions:    ReportQuestionRow[]
  participants: ReportParticipantRow[]
  topics:       ReportTopicRow[]
}

export const liveQuizReportsApi = {
  /** Sessões de Quiz ao Vivo hospedadas pelo professor (mais recentes primeiro). */
  sessions: () =>
    api.get<ReportSessionItem[]>("/api/live-quiz/reports/sessions").then((r) => r.data),

  /** Relatório pedagógico de uma sessão. */
  report: (sessionId: number) =>
    api.get<LiveQuizReport>(`/api/live-quiz/reports/${sessionId}`).then((r) => r.data),
}
