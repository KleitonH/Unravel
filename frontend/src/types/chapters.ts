import type { PoolChallenge } from "./api"

/**
 * PR 60-b — Content fatiado em capítulos H2, com perguntas alocadas
 * adaptativamente (4-7 por capítulo). Consumido pelo `<ChapteredQuizPage />`
 * que implementa o fluxo "Estudo guiado" estilo Duolingo:
 * lê chunk → pratica → próximo chunk.
 */

export type Chapter = {
  chunkIndex: number
  title:      string
  bodyMd:     string
  challenges: PoolChallenge[]
}

export type ChapteredContent = {
  contentId:     number
  contentTitle:  string
  trailId:       number
  minPerChapter: number
  maxPerChapter: number
  chapters:      Chapter[]
}

/**
 * PR 60-a — Estado de publicação por capítulo. Usado pelo manager admin
 * pra mostrar badges "⚠ 2 capítulos sem perguntas" e bloquear publish.
 */
export type ChapterReadiness = {
  chunkIndex: number
  title:      string
  current:    number
  required:   number
  ready:      boolean
}

export type PublicationReadiness = {
  contentId:             number
  ready:                 boolean
  minRequiredPerChapter: number
  chapters:              ChapterReadiness[]
}

/**
 * PR 60-f — pergunta do pool (gerada pela IA OU autoral do moderador),
 * com chunkIndex pra agrupar por capítulo. `authored=true` ⇒ escrita à
 * mão (editável); senão é gerada pela IA (só desativável).
 */
export type ContentQuestion = {
  id:                  number
  chunkIndex:          number
  strategy:            string
  shape:               string
  prompt:              string
  options:             string[]
  correctIndex:        number
  explanation:         string | null
  estimatedDifficulty: number
  authored:            boolean
  flagsOpen:           number   // bandeirinhas Abertas de alunos (0 = nenhuma)
}

/**
 * Bandeirinha (feedback de qualidade) de um aluno sobre uma pergunta.
 * `reason`/`status` chegam como string do enum do backend.
 */
export type ChallengeFeedback = {
  id:                   number
  generatedChallengeId: number
  reason:               FeedbackReasonKey
  comment:              string | null
  status:               FeedbackStatusKey
  studentName:          string
  createdAt:            string
  reviewedAt:           string | null
}

export type FeedbackReasonKey =
  | "GabaritoErrado"
  | "Ambigua"
  | "MultiplaCorreta"
  | "ForaDoConteudo"
  | "Outro"

export type FeedbackStatusKey = "Aberto" | "Revisado" | "Descartado"

/**
 * PR 62b — saúde da geração por IA de um conteúdo. Quando recommendGold,
 * a UI sugere criar gold questions pra calibrar a IA ali.
 */
export type GenerationHealth = {
  contentId:     number
  done:          number
  failed:        number
  total:         number
  successRate:   number   // 0..1
  recommendGold: boolean
  threshold:     number   // ex.: 0.85
  minSample:     number
}

/** Payload pra criar/editar pergunta autoral. */
export type AuthorQuestionRequest = {
  chunkIndex:      number
  prompt:          string
  correctAnswer:   string
  distractors:     string[]   // exatamente 3
  explanation?:    string | null
  difficultyHint?: number | null
}
