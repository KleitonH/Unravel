import { api } from "./client"
import type { Turma, TurmaDetail, TurmaInvite, TurmaStudentSearch } from "@/types/api"

/**
 * Cliente de Turmas — espelha o TurmasController. Endpoints de professor
 * (owned/detail/create/invite/...) exigem papel Moderator no backend; os de
 * aluno (mine/invites/accept/decline/leave) são abertos a qualquer autenticado.
 */
export const turmasApi = {
  // ── professor ─────────────────────────────────────────────────────
  owned: () =>
    api.get<Turma[]>("/api/turmas/owned").then((r) => r.data),

  detail: (id: number) =>
    api.get<TurmaDetail>(`/api/turmas/${id}`).then((r) => r.data),

  create: (body: { name: string; description?: string | null; emblem?: string | null }) =>
    api.post<Turma>("/api/turmas", body).then((r) => r.data),

  searchStudents: (id: number, q: string) =>
    api.get<TurmaStudentSearch[]>(`/api/turmas/${id}/search-students`, { params: { q } }).then((r) => r.data),

  invite: (id: number, studentId: string) =>
    api.post<{ memberId: number }>(`/api/turmas/${id}/invite`, { studentId }).then((r) => r.data),

  removeMember: (id: number, studentId: string) =>
    api.delete(`/api/turmas/${id}/members/${studentId}`).then((r) => r.data),

  archive: (id: number) =>
    api.delete(`/api/turmas/${id}`).then((r) => r.data),

  // ── aluno ─────────────────────────────────────────────────────────
  mine: () =>
    api.get<Turma[]>("/api/turmas/mine").then((r) => r.data),

  invites: () =>
    api.get<TurmaInvite[]>("/api/turmas/invites").then((r) => r.data),

  accept: (memberId: number) =>
    api.put<{ memberId: number }>(`/api/turmas/invites/${memberId}/accept`, {}).then((r) => r.data),

  decline: (memberId: number) =>
    api.put<{ memberId: number }>(`/api/turmas/invites/${memberId}/decline`, {}).then((r) => r.data),

  leave: (id: number) =>
    api.delete(`/api/turmas/${id}/leave`).then((r) => r.data),
}
