import { api } from "./client"

/** Uma missão diária do aluno (espelha DailyQuestView do backend). */
export type DailyQuest = {
  key:         string
  title:       string
  description: string
  icon:        string
  target:      number
  progress:    number
  completed:   boolean
}

export const questsApi = {
  /** Missões de hoje (com progresso). Atribui o conjunto do dia no primeiro acesso. */
  today: () => api.get<DailyQuest[]>("/api/quests/today").then((r) => r.data),
}
