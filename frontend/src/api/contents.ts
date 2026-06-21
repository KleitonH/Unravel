import { api } from "./client"

/** Resumo de conteúdo (espelha ContentResponse do backend; só o que a UI usa). */
export type ContentSummary = {
  id:          number
  trailId:     number
  title:       string
  order:       number
  isCompleted: boolean
}

export const contentsApi = {
  /** Conteúdos de uma trilha (qualquer trilha publicada/visível ao usuário). */
  byTrail: (trailId: number) =>
    api.get<ContentSummary[]>("/api/contents", { params: { trailId } }).then((r) => r.data),
}
