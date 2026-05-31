import { api } from "./client"
import type { DailyReplanReport } from "@/types/api"

export const adminApi = {
  replanNow: () =>
    api.post<DailyReplanReport>("/api/admin/replan-now").then((r) => r.data),
}
