import { api } from "./client"
import type { AppNotification } from "@/types/api"

/** PR 69 — central de notificações in-app. */
export const notificationsApi = {
  list: () =>
    api.get<AppNotification[]>("/api/notifications").then((r) => r.data),

  unreadCount: () =>
    api.get<{ count: number }>("/api/notifications/unread-count").then((r) => r.data.count),

  markRead: (id: number) =>
    api.put(`/api/notifications/${id}/read`, {}).then((r) => r.data),

  markAllRead: () =>
    api.put("/api/notifications/read-all", {}).then((r) => r.data),
}
