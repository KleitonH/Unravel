import { api } from "./client"
import type { JourneyPlan, ReinforcementQuiz, TrailMap } from "@/types/api"

export const journeyApi = {
  today: (trailId: number) =>
    api.get<JourneyPlan>("/api/journey/today", { params: { trailId } }).then((r) => r.data),

  /**
   * PR 40 — mapa de progressão SMW: ilhas (Contents) ordenadas com
   * status (Locked/Available/InProgress/Completed) + progresso de
   * desafios. Backend faz bootstrap automático (cria UserContent
   * Available pra 1ª ilha se aluno não tem nenhum).
   */
  map: (trailId: number) =>
    api.get<TrailMap>(`/api/journey/trails/${trailId}/map`).then((r) => r.data),

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
