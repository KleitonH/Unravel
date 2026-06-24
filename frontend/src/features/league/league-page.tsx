import { useQuery } from "@tanstack/react-query"
import { ArrowDown, ArrowUp, CalendarDays, Trophy } from "lucide-react"
import { leagueApi } from "@/api/league"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { MyLeague } from "@/types/api"

const TIER: Record<string, { emoji: string; color: string }> = {
  Bronze:   { emoji: "🥉", color: "#cd7f32" },
  Prata:    { emoji: "🥈", color: "#9fb0c3" },
  Ouro:     { emoji: "🥇", color: "#facc15" },
  Diamante: { emoji: "💎", color: "#60a5fa" },
  Mestre:   { emoji: "👑", color: "#c084fc" },
}

/**
 * PR 66 — liga semanal (estilo Duolingo). Tier, sua posição, XP da semana e o
 * leaderboard com zonas de promoção (topo) e rebaixamento (fundo) destacadas.
 */
export function LeaguePage() {
  const q = useQuery({ queryKey: ["league"], queryFn: leagueApi.mine })

  if (q.isLoading) return <div className="mx-auto w-full max-w-3xl p-6 lg:p-10 space-y-4"><Skeleton className="h-10 w-48" /><Skeleton className="h-64" /></div>
  if (!q.data) return <div className="p-6 lg:p-10 text-muted-foreground">Não foi possível carregar a liga.</div>

  const d = q.data
  const t = TIER[d.tier] ?? TIER.Bronze

  return (
    <div className="mx-auto w-full max-w-3xl p-6 lg:p-10 space-y-5">
      <header className="space-y-1">
        <h1 className="text-3xl font-display font-extrabold tracking-tight">🏆 Liga Semanal</h1>
        <p className="text-sm text-muted-foreground flex items-center gap-1.5">
          <CalendarDays className="h-3.5 w-3.5" /> Termina em {d.weekEndsAt}
        </p>
      </header>

      {d.lastResult && d.lastResult !== "stayed" && (
        <Card className={cn("border", d.lastResult === "promoted" ? "border-success/40 bg-success/5" : "border-destructive/40 bg-destructive/5")}>
          <CardContent className="py-3 flex items-center gap-2 text-sm">
            {d.lastResult === "promoted"
              ? <><ArrowUp className="h-4 w-4 text-success" /> Você <strong>subiu de liga</strong> na semana passada! 🎉</>
              : <><ArrowDown className="h-4 w-4 text-destructive" /> Você foi rebaixado na semana passada. Bora reagir!</>}
          </CardContent>
        </Card>
      )}

      {/* Faixa atual */}
      <Card className="bg-gradient-to-br from-primary/10 via-card to-card" style={{ borderColor: `${t.color}55` }}>
        <CardContent className="py-5 flex items-center gap-4">
          <span className="text-5xl">{t.emoji}</span>
          <div className="flex-1">
            <p className="font-display text-2xl font-extrabold" style={{ color: t.color }}>Liga {d.tier}</p>
            <p className="text-sm text-muted-foreground">
              Sua posição: <strong className="text-foreground">{d.rank}º</strong> de {d.size} · {fmt(d.weeklyXp)} XP esta semana
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Leaderboard */}
      <Card>
        <CardContent className="py-4 space-y-1">
          {d.promoteZone > 0 && <ZoneLabel kind="promote" text={`Sobem para ${d.nextTier}`} />}
          {d.leaderboard.map((m, i) => {
            const promo = m.rank <= d.promoteZone
            const releg = d.relegateZone > 0 && m.rank > d.size - d.relegateZone
            const showRelegLabel = releg && (i === 0 || !(d.relegateZone > 0 && d.leaderboard[i - 1].rank > d.size - d.relegateZone))
            return (
              <div key={m.userId}>
                {showRelegLabel && <ZoneLabel kind="relegate" text={`Caem para ${d.prevTier}`} />}
                <div className={cn("flex items-center gap-3 rounded-md px-3 py-2 border",
                  m.isMine ? "border-primary bg-primary/10" : "border-transparent",
                  promo && "bg-success/5", releg && "bg-destructive/5")}>
                  <span className={cn("w-6 text-center font-display font-extrabold",
                    m.rank === 1 ? "text-warning" : m.rank === 2 ? "text-muted-foreground" : m.rank === 3 ? "text-accent" : "text-muted-foreground/60")}>
                    {m.rank}
                  </span>
                  <Avatar className="h-8 w-8"><AvatarFallback>{initials(m.name)}</AvatarFallback></Avatar>
                  <span className="flex-1 min-w-0 truncate font-medium">{m.name}{m.isMine && <span className="text-xs text-primary/80"> · você</span>}</span>
                  <span className="inline-flex items-center gap-1 text-muted-foreground tabular-nums"><Trophy className="h-3 w-3" />{fmt(m.weeklyXp)}</span>
                </div>
              </div>
            )
          })}
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground text-center">
        Ganhe XP estudando para subir no ranking. No fim da semana, o topo sobe de liga e o fundo desce.
      </p>
    </div>
  )
}

function ZoneLabel({ kind, text }: { kind: "promote" | "relegate"; text: string }) {
  return (
    <div className={cn("flex items-center gap-1.5 px-1 py-1 text-[11px] font-bold uppercase tracking-wider",
      kind === "promote" ? "text-success" : "text-destructive")}>
      {kind === "promote" ? <ArrowUp className="h-3 w-3" /> : <ArrowDown className="h-3 w-3" />}
      {text}
      <span className="flex-1 h-px ml-1" style={{ background: "currentColor", opacity: 0.25 }} />
    </div>
  )
}

function fmt(n: number): string {
  if (n < 1_000) return n.toString()
  return `${(n / 1_000).toFixed(n < 10_000 ? 1 : 0).replace(/\.0$/, "")}k`
}

function initials(name?: string | null) {
  if (!name) return "?"
  return name.trim().split(/\s+/).map((p) => p[0]?.toUpperCase() ?? "").slice(0, 2).join("")
}

export type { MyLeague }
