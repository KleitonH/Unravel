import { useState } from "react"
import { useNavigate } from "@tanstack/react-router"
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query"
import { Bell, CheckCheck } from "lucide-react"
import { notificationsApi } from "@/api/notifications"
import { Button } from "@/components/ui/button"
import { DropdownMenu, DropdownMenuContent, DropdownMenuTrigger } from "@/components/ui/dropdown-menu"
import { cn } from "@/lib/utils"
import type { AppNotification } from "@/types/api"

const ICON: Record<string, string> = {
  FriendRequest: "👋", FriendAccepted: "🤝", CaixinhaGoal: "🎯", CaixinhaStreak: "🔥",
  LeaguePromoted: "⬆️", LeagueRelegated: "⬇️", EventStarted: "⚔️", System: "🔔",
  ClassInvite: "🎓", LiveQuizStarted: "📣", ArenaChallenge: "⚔️",
  PartnershipRequest: "🧶", PartnershipAccepted: "🧶", YarnPassed: "🧶",
}

/**
 * PR 69 — sino de notificações fixo no canto do shell. Badge de não-lidas
 * (poll a cada 60s); dropdown lista as recentes; clicar marca como lida e
 * navega pro link.
 */
export function NotificationBell({ inline = false }: { inline?: boolean }) {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)

  const countQuery = useQuery({
    queryKey: ["notifications", "count"],
    queryFn:  notificationsApi.unreadCount,
    refetchInterval: 60_000,
  })
  const listQuery = useQuery({
    queryKey: ["notifications", "list"],
    queryFn:  notificationsApi.list,
    enabled:  open,
  })

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["notifications", "count"] })
    qc.invalidateQueries({ queryKey: ["notifications", "list"] })
  }
  const markRead = useMutation({ mutationFn: (id: number) => notificationsApi.markRead(id), onSuccess: refresh })
  const markAll  = useMutation({ mutationFn: () => notificationsApi.markAllRead(), onSuccess: refresh })

  const count = countQuery.data ?? 0
  const items = listQuery.data ?? []

  function onClickItem(n: AppNotification) {
    if (!n.isRead) markRead.mutate(n.id)
    if (n.link) {
      setOpen(false)
      // Links com query (ex.: /ao-vivo?code=XYZ) o router tipado não navega
      // bem via `to`; usamos navegação real pra carregar a query.
      if (n.link.includes("?")) window.location.assign(n.link)
      else navigate({ to: n.link })
    }
  }

  const bell = (
      <DropdownMenu open={open} onOpenChange={setOpen}>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon" className="relative rounded-full" title="Notificações">
            <Bell className="h-4 w-4" />
            {count > 0 && (
              <span className="absolute -top-1 -right-1 h-5 min-w-5 px-1 rounded-full bg-accent text-accent-foreground text-[10px] font-bold inline-flex items-center justify-center">
                {count > 9 ? "9+" : count}
              </span>
            )}
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-80 p-0">
          <div className="flex items-center justify-between px-3 py-2 border-b border-border">
            <span className="text-sm font-display font-bold">Notificações</span>
            {count > 0 && (
              <button onClick={() => markAll.mutate()} className="text-xs text-primary inline-flex items-center gap-1 hover:underline">
                <CheckCheck className="h-3.5 w-3.5" /> marcar todas
              </button>
            )}
          </div>
          <div className="max-h-80 overflow-y-auto">
            {listQuery.isLoading && <p className="text-sm text-muted-foreground text-center py-6">Carregando…</p>}
            {!listQuery.isLoading && items.length === 0 && (
              <p className="text-sm text-muted-foreground text-center py-8">Nada por aqui ainda. 🐾</p>
            )}
            {items.map((n) => (
              <button key={n.id} onClick={() => onClickItem(n)}
                className={cn("w-full text-left flex gap-2.5 px-3 py-2.5 border-b border-border/60 hover:bg-popover/60 transition-colors",
                  !n.isRead && "bg-primary/5")}>
                <span className="text-lg leading-none mt-0.5">{ICON[n.type] ?? "🔔"}</span>
                <div className="flex-1 min-w-0">
                  <p className={cn("text-sm truncate", !n.isRead && "font-semibold")}>{n.title}</p>
                  <p className="text-xs text-muted-foreground line-clamp-2">{n.body}</p>
                  <p className="text-[10px] text-muted-foreground/70 mt-0.5">{n.createdAt}</p>
                </div>
                {!n.isRead && <span className="h-2 w-2 rounded-full bg-accent mt-1.5 shrink-0" />}
              </button>
            ))}
          </div>
        </DropdownMenuContent>
      </DropdownMenu>
  )

  if (inline) return bell
  return <div className="fixed top-3 right-3 z-50">{bell}</div>
}
