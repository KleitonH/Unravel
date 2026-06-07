import { api } from "./client"
import type { CreateGoldRequest, ModeratorGoldItem, UpdateGoldRequest } from "@/types/gold"

/**
 * Cliente pros endpoints de gold-set do moderador.
 *
 * Backend: AdminController PR 33d (list/create/delete) + PR 56-a (update).
 * Endpoints exigem role Moderator.
 */
export const moderatorGoldApi = {
  /** Lista itens ativos de gold pro Content. */
  list: (contentId: number) =>
    api.get<ModeratorGoldItem[]>(`/api/admin/gold/${contentId}`).then((r) => r.data),

  /** Cria novo gold (manual OU promover gerada). */
  create: (contentId: number, body: CreateGoldRequest) =>
    api.post<{ id: number }>(`/api/admin/gold/${contentId}`, body).then((r) => r.data),

  /** Edita gold existente. Source/curator ficam intocados. */
  update: (goldId: number, body: UpdateGoldRequest) =>
    api.put<{ id: number; updatedAt: string }>(`/api/admin/gold/${goldId}`, body).then((r) => r.data),

  /** Soft-delete (IsActive=false). Histórico fica. */
  remove: (goldId: number) =>
    api.delete<void>(`/api/admin/gold/${goldId}`).then((r) => r.data),
}
