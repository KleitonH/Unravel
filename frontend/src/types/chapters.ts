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
