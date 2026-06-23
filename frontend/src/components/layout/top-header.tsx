import { useQuery } from "@tanstack/react-query"
import { Link } from "@tanstack/react-router"
import { Coins, Flame, Zap } from "lucide-react"
import { profileApi } from "@/api/profile"
import { useAuth } from "@/stores/auth"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { LivesIndicator } from "@/components/gamification/lives-indicator"
import { LifeRefillTimer } from "@/components/gamification/life-refill-timer"
import { xpLevel } from "@/components/gamification/xp-level"
import { NaviFace } from "@/components/navi/navi-face"
import { NotificationBell } from "./notification-bell"
import { cn } from "@/lib/utils"
import type { StudentProfile } from "@/types/api"

/**
 * Header global do sistema (autenticado). Consolida as informações de
 * gamificação num só lugar, visível em todas as telas:
 *
 *   [ vidas · nível · ofensiva · moedas ]  ……  [ 🔔 ]  [ NAVI + nome ]
 *
 * Stats só aparecem pro aluno (moderador não tem gamificação individual).
 * À direita, o sino de notificações + o atalho do perfil com o preview do
 * rosto do NAVI personalizado do usuário.
 */
export function TopHeader() {
  const user = useAuth((s) => s.user)

  const profileQuery = useQuery({
    queryKey: ["profile", "me"],
    queryFn: profileApi.me,
    staleTime: 60_000,
  })
  const p = profileQuery.data
  const s: StudentProfile | null = p && p.role === "Student" ? p : null
  const lvl = s ? xpLevel(s.xp) : null

  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-2 border-b border-border bg-card/80 px-3 backdrop-blur sm:px-4">
      {/* Stats do aluno — rolagem horizontal em telas estreitas */}
      <div className="flex min-w-0 flex-1 items-center gap-1.5 overflow-x-auto sm:gap-2 [scrollbar-width:none]">
        {s && (
          <>
            <span className="flex shrink-0 items-center gap-1.5">
              <LivesIndicator lives={s.lives} max={s.maxLives} size="sm" />
              {s.lives < s.maxLives && <LifeRefillTimer seconds={s.secondsToNextLife} />}
            </span>
            {lvl && (
              <StatPill
                icon={<Zap className="h-3.5 w-3.5" />}
                value={`Nv ${lvl.level}`}
                tone="primary"
                title={`Nível ${lvl.level} · ${s.xp} XP · faltam ${lvl.toNext} pro próximo`}
              />
            )}
            <StatPill
              icon={<Flame className="h-3.5 w-3.5" />}
              value={s.streakDays}
              tone={s.streakDays > 0 ? "warning" : "muted"}
              title={`Ofensiva: ${s.streakDays} dia(s) seguidos`}
            />
            <StatPill
              icon={<Coins className="h-3.5 w-3.5" />}
              value={s.coins}
              tone="muted"
              title={`${s.coins} moedas`}
            />
          </>
        )}
      </div>

      {/* Notificações + perfil */}
      <div className="flex shrink-0 items-center gap-1.5 sm:gap-2">
        <NotificationBell inline />
        <Link
          to="/profile"
          className="flex items-center gap-2 rounded-full border border-border bg-popover/40 py-1 pl-1 pr-1 transition-colors hover:border-primary/50 sm:pr-3"
          title="Seu perfil"
        >
          {s ? (
            <NaviFace size={32} />
          ) : (
            <Avatar className="h-8 w-8">
              <AvatarFallback>{initials(user?.name)}</AvatarFallback>
            </Avatar>
          )}
          <span className="hidden max-w-[120px] truncate text-sm font-medium sm:block">
            {user?.name ?? "Perfil"}
          </span>
        </Link>
      </div>
    </header>
  )
}

function StatPill({
  icon,
  value,
  tone,
  title,
}: {
  icon: React.ReactNode
  value: string | number
  tone: "primary" | "warning" | "muted"
  title: string
}) {
  return (
    <span
      title={title}
      className={cn(
        "inline-flex shrink-0 items-center gap-1 rounded-full border px-2.5 py-1 text-xs font-bold tabular-nums",
        tone === "primary" && "border-primary/40 bg-primary/10 text-primary",
        tone === "warning" && "border-warning/40 bg-warning/10 text-warning",
        tone === "muted" && "border-border bg-popover/60 text-foreground",
      )}
    >
      {icon}
      {value}
    </span>
  )
}

function initials(name?: string | null) {
  if (!name) return "?"
  return name
    .trim()
    .split(/\s+/)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .slice(0, 2)
    .join("")
}
