import { api } from "./client"
import type { MyLeague } from "@/types/api"

/** PR 66 — cliente da liga semanal. */
export const leagueApi = {
  mine: () => api.get<MyLeague>("/api/league").then((r) => r.data),
}
