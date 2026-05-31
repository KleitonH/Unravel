import { api } from "./client"
import type {
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
}
