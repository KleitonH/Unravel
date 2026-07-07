import { useQuery } from "@tanstack/react-query"
import { Link } from "@tanstack/react-router"
import { ChevronRight, Crown, Target } from "lucide-react"
import { partnershipsApi, type Partnership } from "@/api/partnerships"
import { caixinhaApi } from "@/api/caixinha"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { YarnBall } from "@/components/novelo/yarn-ball"
import { cn } from "@/lib/utils"
import type { CaixinhaDetail } from "@/types/api"

/**
 * "Metas do dia" — visão unificada do que rende progresso social hoje:
 * o(s) novelo(s) das parcerias e a meta coletiva da caixinha, cada um com
 * sua barra de progresso, mais um guia honesto de COMO ganhar progresso
 * (só responder perguntas de quiz e boss creditam os dois hoje).
 *
 * Some sozinho pra quem não tem parceria nem caixinha.
 */
export function DailyGoalsWidget() {
  const partnerships = useQuery({ queryKey: ["partnerships"], queryFn: partnershipsApi.list, staleTime: 60_000 })
  const caixinha     = useQuery({ queryKey: ["caixinha", "mine"], queryFn: caixinhaApi.mine, staleTime: 60_000 })

  const novelos = (partnerships.data ?? []).filter((p) => p.yarn).slice(0, 2)
  const cx      = caixinha.data ?? null

  const loading = partnerships.isLoading || caixinha.isLoading
  if (loading) return <Skeleton className="h-40 w-full" />
  if (novelos.length === 0 && !cx) return null

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Target className="h-4 w-4 text-primary" /> Metas do dia
        </CardTitle>
        <CardDescription>
          Responda perguntas pra girar o novelo e fazer o clã subir.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-3">
        {novelos.map((p) => <NoveloGoal key={p.id} p={p} />)}
        {cx && <CaixinhaGoal cx={cx} />}

        {/* Guia honesto: só quiz e boss creditam novelo + clã hoje. */}
        <div className="rounded-md border border-dashed border-border p-3">
          <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            Como ganhar progresso
          </p>
          <ul className="space-y-1 text-xs text-muted-foreground">
            <li className="flex items-start gap-1.5">
              <span className="text-success">✓</span>
              <span>Responda perguntas de qualquer trilha, reforço ou quiz — <b>cada acerto desenrola o novelo e soma pro clã</b>.</span>
            </li>
            <li className="flex items-start gap-1.5">
              <Crown className="mt-0.5 h-3 w-3 shrink-0 text-warning" />
              <span>Enfrente um <b>Boss</b> da trilha pra um empurrão extra.</span>
            </li>
          </ul>
          <Link
            to="/trails"
            className="mt-3 inline-flex items-center text-xs font-semibold text-primary hover:underline"
          >
            Praticar agora <ChevronRight className="h-3.5 w-3.5" />
          </Link>
        </div>
      </CardContent>
    </Card>
  )
}

/** Uma meta de novelo (parceria). Clicável pra aba Parcerias. */
function NoveloGoal({ p }: { p: Partnership }) {
  const y   = p.yarn!
  const pct = Math.min(100, Math.round((y.progress / Math.max(y.dailyGoal, 1)) * 100))
  const done = y.progress >= y.dailyGoal

  return (
    <Link to="/parcerias" className="flex items-center gap-3 rounded-md p-1 transition-colors hover:bg-accent/5">
      <YarnBall pct={pct} active={y.isMyTurn} tangled={y.state === "Tangled"} dropped={y.state === "Dropped"} size={40} />
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-2">
          <span className="truncate text-sm font-medium">Novelo com {firstName(p.partnerName)}</span>
          <span className="shrink-0 text-xs tabular-nums text-muted-foreground">
            {Math.min(y.progress, y.dailyGoal)}/{y.dailyGoal}
          </span>
        </div>
        <Bar pct={pct} complete={done} />
        <p className="mt-0.5 text-[11px] text-muted-foreground">
          {y.isMyTurn ? "Sua vez — responda pra passar o novelo" : "Aguardando o parceiro"}
          {" · "}ciclo {y.cyclesCompleted}/{y.totalCycles}
        </p>
      </div>
    </Link>
  )
}

/** Meta coletiva da caixinha (clã). Clicável pra tela da Caixinha. */
function CaixinhaGoal({ cx }: { cx: CaixinhaDetail }) {
  const pct  = Math.min(100, Math.round((cx.dailyPoints / Math.max(cx.dailyGoal, 1)) * 100))
  const left = Math.max(0, cx.dailyGoal - cx.dailyPoints)

  return (
    <Link to="/caixinha" className="flex items-center gap-3 rounded-md p-1 transition-colors hover:bg-accent/5">
      <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-accent/10 text-xl">
        {cx.emblem || "🐈"}
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-2">
          <span className="truncate text-sm font-medium">Meta do clã · {cx.name}</span>
          <span className="shrink-0 text-xs tabular-nums text-muted-foreground">
            {cx.dailyPoints}/{cx.dailyGoal}
          </span>
        </div>
        <Bar pct={pct} complete={cx.goalReachedToday} />
        <p className="mt-0.5 text-[11px] text-muted-foreground">
          {cx.goalReachedToday
            ? "Meta batida hoje! 🎉"
            : `Faltam ${left} pts · ${cx.activeTodayCount} ativo(s) hoje`}
        </p>
      </div>
    </Link>
  )
}

/** Barrinha de progresso compartilhada (verde quando completa). */
function Bar({ pct, complete }: { pct: number; complete: boolean }) {
  return (
    <div className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-border">
      <div
        className={cn("h-full rounded-full transition-all", complete ? "bg-success" : "bg-primary")}
        style={{ width: `${pct}%` }}
      />
    </div>
  )
}

function firstName(name: string): string {
  return name.trim().split(/\s+/)[0] ?? name
}
