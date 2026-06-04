import { api } from "./client"
import type {
  CreateCustomContentRequest,
  CreateCustomTrailRequest,
  CustomContentDto,
  CustomTrailDto,
  ForgeEnqueueResponse,
  UpdateCustomContentRequest,
  UpdateCustomTrailRequest,
} from "@/types/admin-custom"

/**
 * Cliente HTTP pros endpoints de Trilha/Content custom (PR 35).
 * Espelha 8 endpoints em /api/admin/{trails,contents,forge}.
 *
 * Não trata erro nem 401 — interceptor global já cobre. Componente
 * captura via TanStack Query `error`/`onError` se quiser exibir toast.
 */
export const adminCustomApi = {
  // ── Trails ────────────────────────────────────────────────────
  listTrails: () =>
    api.get<CustomTrailDto[]>("/api/admin/trails").then((r) => r.data),

  createTrail: (body: CreateCustomTrailRequest) =>
    api.post<CustomTrailDto>("/api/admin/trails", body).then((r) => r.data),

  updateTrail: (trailId: number, body: UpdateCustomTrailRequest) =>
    api.patch<void>(`/api/admin/trails/${trailId}`, body).then((r) => r.data),

  deleteTrail: (trailId: number) =>
    api.delete<void>(`/api/admin/trails/${trailId}`).then((r) => r.data),

  // ── Contents ──────────────────────────────────────────────────
  listContents: (trailId: number) =>
    api.get<CustomContentDto[]>(`/api/admin/trails/${trailId}/contents`).then((r) => r.data),

  createContent: (trailId: number, body: CreateCustomContentRequest) =>
    api.post<CustomContentDto>(`/api/admin/trails/${trailId}/contents`, body).then((r) => r.data),

  updateContent: (contentId: number, body: UpdateCustomContentRequest) =>
    api.patch<void>(`/api/admin/contents/${contentId}`, body).then((r) => r.data),

  deleteContent: (contentId: number) =>
    api.delete<void>(`/api/admin/contents/${contentId}`).then((r) => r.data),

  // ── Forge bridge ──────────────────────────────────────────────
  enqueueForge: (contentId: number, max = 20, urgent = false) =>
    api
      .post<ForgeEnqueueResponse>(`/api/admin/forge/${contentId}`, null, {
        params: { max, urgent },
      })
      .then((r) => r.data),
}
