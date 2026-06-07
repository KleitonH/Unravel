import { api } from "./client"
import type {
  BossFightResultResponse, BossFightStartResponse, BossFightSubmitRequest,
  JourneyPlan, ReinforcementQuiz, TrailMap, TrailMasteryReport,
} from "@/types/api"

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

  /**
   * PR 41 — radar de fraquezas: lista de topics com effective mastery,
   * confidence, SRS state e severity. Ordenado por (severity, score asc).
   */
  mastery: (trailId: number) =>
    api.get<TrailMasteryReport>(`/api/journey/trails/${trailId}/mastery`).then((r) => r.data),

  /**
   * PR 50 — Boss Fight: inicia uma sessão com N=10 perguntas balanceadas
   * por cobertura + difficulty + strategy mix.
   */
  bossFightStart: (trailId: number) =>
    api
      .post<BossFightStartResponse>(`/api/journey/trails/${trailId}/boss-fight/start`)
      .then((r) => r.data),

  bossFightSubmit: (trailId: number, body: BossFightSubmitRequest) =>
    api
      .post<BossFightResultResponse>(`/api/journey/trails/${trailId}/boss-fight/submit`, body)
      .then((r) => r.data),

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
