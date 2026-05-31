import { api } from "./client"
import type { JourneyPlan } from "@/types/api"

export const journeyApi = {
  today: (trailId: number) =>
    api.get<JourneyPlan>("/api/journey/today", { params: { trailId } }).then((r) => r.data),

  replan: (trailId: number) =>
    api.post<JourneyPlan>("/api/journey/replan", null, { params: { trailId } }).then((r) => r.data),
}
