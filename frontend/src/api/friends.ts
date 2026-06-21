import { api } from "./client"
import type { Friend, FriendRequests, UserSearchResult } from "@/types/api"

/**
 * PR 64 — cliente de Amigos/Parcerias. Espelha o FriendsController.
 */
export const friendsApi = {
  list: () =>
    api.get<Friend[]>("/api/friends").then((r) => r.data),

  requests: () =>
    api.get<FriendRequests>("/api/friends/requests").then((r) => r.data),

  search: (q: string) =>
    api.get<UserSearchResult[]>("/api/friends/search", { params: { q } }).then((r) => r.data),

  send: (addresseeId: string) =>
    api.post<{ friendshipId: number }>("/api/friends/requests", { addresseeId }).then((r) => r.data),

  accept: (friendshipId: number) =>
    api.put<{ friendshipId: number }>(`/api/friends/requests/${friendshipId}/accept`, {}).then((r) => r.data),

  decline: (friendshipId: number) =>
    api.put<{ friendshipId: number }>(`/api/friends/requests/${friendshipId}/decline`, {}).then((r) => r.data),

  remove: (otherUserId: string) =>
    api.delete(`/api/friends/${otherUserId}`).then((r) => r.data),
}
