import { api } from "./client"
import type { Trail } from "@/types/api"

export const trailsApi = {
  list:   () => api.get<Trail[]>("/api/trails").then((r) => r.data),
  get:    (id: number) => api.get<Trail>(`/api/trails/${id}`).then((r) => r.data),
  /** Inscreve o user autenticado na trilha. Backend é idempotente —
   *  chamar duas vezes não duplica. Retorna o UserTrail criado/existente. */
  enroll: (id: number) =>
    api.post<{ id: number; trailId: number; progress: number }>(`/api/trails/${id}/enroll`).then((r) => r.data),
}
