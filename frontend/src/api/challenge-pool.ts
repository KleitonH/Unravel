import { api } from "./client"
import type {
  AdaptiveHistoryItem,
  AdaptiveNextResponse,
  ChallengePool,
  SubmitPoolChallengeRequest,
  SubmitPoolChallengeResponse,
} from "@/types/api"

export const challengePoolApi = {
  get: (contentId: number, targetCount = 5) =>
    api
      .get<ChallengePool>(`/api/contents/${contentId}/challenge-pool`, {
        params: { targetCount },
      })
      .then((r) => r.data),

  submit: (contentId: number, body: SubmitPoolChallengeRequest) =>
    api
      .post<SubmitPoolChallengeResponse>(
        `/api/contents/${contentId}/challenge-pool/submit`,
        body,
      )
      .then((r) => r.data),

  /**
   * PR 42 — CAT-lite: pede a próxima pergunta dado o histórico curto da
   * sessão. Backend calcula ability online + zona proximal + critério
   * de parada. Resposta com `done=true` encerra a sessão.
   */
  adaptiveNext: (contentId: number, history: AdaptiveHistoryItem[]) =>
    api
      .post<AdaptiveNextResponse>(
        `/api/contents/${contentId}/challenge-pool/adaptive/next`,
        { history },
      )
      .then((r) => r.data),
}
