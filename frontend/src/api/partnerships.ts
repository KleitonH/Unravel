import { api } from "./client"

export type YarnBall = {
  id:              number
  ownerId:         string
  isMyTurn:        boolean
  progress:        number
  dailyGoal:       number
  cyclesCompleted: number
  totalCycles:     number
  state:           string // Ok | Tangled | Dropped
}

export type Partnership = {
  id:               number
  partnerId:        string
  partnerName:      string
  partnerXp:        number
  partnerTitle:     string | null
  status:           string
  novelosCompleted: number
  yarn:             YarnBall | null
}

export type PartnershipRequest = {
  id:          number
  partnerId:   string
  partnerName: string
  partnerXp:   number
  direction:   "incoming" | "outgoing"
  createdAt:   string
}

export type PartnershipRequests = {
  incoming: PartnershipRequest[]
  outgoing: PartnershipRequest[]
}

export const partnershipsApi = {
  /** Parcerias ativas (com estado do novelo). */
  list: () => api.get<Partnership[]>("/api/partnerships").then((r) => r.data),

  /** Convites pendentes (recebidos + enviados). */
  requests: () => api.get<PartnershipRequests>("/api/partnerships/requests").then((r) => r.data),

  /** Convida um amigo pra formar parceria. */
  invite: (addresseeId: string) =>
    api.post<{ partnershipId: number }>("/api/partnerships/requests", { addresseeId }).then((r) => r.data),

  accept:  (id: number) => api.put(`/api/partnerships/requests/${id}/accept`, {}).then((r) => r.data),
  decline: (id: number) => api.put(`/api/partnerships/requests/${id}/decline`, {}).then((r) => r.data),

  /** Desfaz a parceria. */
  leave: (id: number) => api.delete(`/api/partnerships/${id}`).then((r) => r.data),
}
