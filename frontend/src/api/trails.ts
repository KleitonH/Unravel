import { api } from "./client"
import type { Trail } from "@/types/api"

export const trailsApi = {
  list: () => api.get<Trail[]>("/api/trails").then((r) => r.data),
  get:  (id: number) => api.get<Trail>(`/api/trails/${id}`).then((r) => r.data),
}
