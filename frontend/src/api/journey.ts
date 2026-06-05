import { api } from "./client"
import type { JourneyPlan, ReinforcementQuiz } from "@/types/api"

export const journeyApi = {
  today: (trailId: number) =>
    api.get<JourneyPlan>("/api/journey/today", { params: { trailId } }).then((r) => r.data),

  replan: (trailId: number) =>
    api.post<JourneyPlan>("/api/journey/replan", null, { params: { trailId } }).then((r) => r.data),

  /**
   * PR 37 — "Treinar fraquezas". Retorna até `count` perguntas focadas
   * nos tópicos com mastery efetiva < 0.6, excluindo perguntas que o
   * aluno já respondeu. Backend pode disparar geração urgent se pool
   * insuficiente — `moreComing=true` sinaliza pra UI mostrar "mais
   * perguntas em breve" sem bloquear.
   */
  reinforce: (trailId: number, count = 5) =>
    api
      .post<ReinforcementQuiz>(`/api/journey/reinforce/${trailId}`, null, { params: { count } })
      .then((r) => r.data),
}
