import { useState } from "react"
import { useMutation } from "@tanstack/react-query"
import { HubConnectionState } from "@microsoft/signalr"
import { Loader2, Play, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { adminApi } from "@/api/admin"
import { useJourneyHub } from "@/hooks/use-journey-hub"
import { useAuth } from "@/stores/auth"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Separator } from "@/components/ui/separator"
import { cn } from "@/lib/utils"
import type { DailyPlanGeneratedEvent, DailyReplanReport, StreakResetEvent } from "@/types/api"

type LiveEvent =
  | { kind: "DailyPlanGenerated"; at: string; data: DailyPlanGeneratedEvent }
  | { kind: "StreakReset";        at: string; data: StreakResetEvent }
  | { kind: "system";             at: string; message: string }

export function AdminPage() {
  const isModerator = useAuth((s) => s.isModerator())
  const [events, setEvents] = useState<LiveEvent[]>([])
  const [lastReport, setLastReport] = useState<DailyReplanReport | null>(null)

  const hubState = useJourneyHub({
    onDailyPlanGenerated: (e) => {
      const evt: LiveEvent = { kind: "DailyPlanGenerated", at: nowIso(), data: e }
      setEvents((prev) => [evt, ...prev].slice(0, 50))
    },
    onStreakReset: (e) => {
      const evt: LiveEvent = { kind: "StreakReset", at: nowIso(), data: e }
      setEvents((prev) => [evt, ...prev].slice(0, 50))
    },
  })

  const replanMutation = useMutation({
    mutationFn: adminApi.replanNow,
    onSuccess: (r) => {
      setLastReport(r)
      const evt: LiveEvent = {
        kind: "system",
        at: nowIso(),
        message: `Cron disparado — ${r.processed} processados (falhas: ${r.failures}, meta cumprida: ${r.yesterdayGoalMet}).`,
      }
      setEvents((prev) => [evt, ...prev].slice(0, 50))
      toast.success("Cron disparado")
    },
    onError: (err: unknown) => {
      const status = (err as { response?: { status?: number } })?.response?.status
      const msg = status === 403
        ? "Você não tem permissão (precisa ser Moderator)."
        : "Falha ao disparar cron."
      toast.error(msg)
      const evt: LiveEvent = { kind: "system", at: nowIso(), message: `Erro: ${msg}` }
      setEvents((prev) => [evt, ...prev].slice(0, 50))
    },
  })

  return (
    <div className="mx-auto w-full max-w-6xl p-6 lg:p-10 space-y-5">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-display font-extrabold tracking-tight">🛠️ Painel admin</h1>
          <p className="text-muted-foreground mt-1">
            Disparo manual do cron diário + log de eventos SignalR em tempo real.
          </p>
        </div>
      </header>

      {!isModerator && (
        <Card className="border-warning/40 bg-warning/5">
          <CardContent className="pt-6 text-sm">
            ⚠️ Esta página é para moderadores. Se você não vir os botões funcionando,
            o backend retornará 403 (defesa em profundidade).
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="text-lg">Hub SignalR</CardTitle>
              <CardDescription>{hubStateLabel(hubState)}</CardDescription>
            </div>
            <div>
              <CardTitle className="text-lg">Cron diário</CardTitle>
              <CardDescription>Auto às 00:05 UTC — manual abaixo.</CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          <Button
            onClick={() => replanMutation.mutate()}
            disabled={replanMutation.isPending}
          >
            {replanMutation.isPending
              ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Executando…</>
              : <><Play className="h-4 w-4 mr-1" />Disparar cron agora</>}
          </Button>
          <Button variant="outline" onClick={() => setEvents([])}>
            <Trash2 className="h-4 w-4 mr-1" />Limpar log
          </Button>
        </CardContent>
        {lastReport && (
          <CardContent className="grid grid-cols-3 gap-3 pt-0">
            <Stat label="Processados" value={lastReport.processed} />
            <Stat label="Meta cumprida" value={lastReport.yesterdayGoalMet} />
            <Stat label="Falhas" value={lastReport.failures} bad={lastReport.failures > 0} />
          </CardContent>
        )}
      </Card>

      <Separator />

      <section className="space-y-2">
        <h2 className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">
          Log de eventos ({events.length})
        </h2>
        {events.length === 0 ? (
          <p className="text-sm text-muted-foreground text-center py-6">
            Nenhum evento ainda. Dispare o cron acima.
          </p>
        ) : (
          <ul className="space-y-1">
            {events.map((ev, i) => <LogRow key={`${ev.at}-${i}`} ev={ev} />)}
          </ul>
        )}
      </section>
    </div>
  )
}

function hubStateLabel(s: HubConnectionState): string {
  switch (s) {
    case HubConnectionState.Connected:    return "🟢 Conectado"
    case HubConnectionState.Connecting:   return "🟡 Conectando…"
    case HubConnectionState.Reconnecting: return "🟡 Reconectando…"
    case HubConnectionState.Disconnecting:return "🟡 Desconectando…"
    case HubConnectionState.Disconnected: return "🔴 Desconectado"
  }
}

function Stat({ label, value, bad = false }: { label: string; value: number; bad?: boolean }) {
  return (
    <div className="rounded-md border border-border bg-popover/40 p-3">
      <p className={cn("text-2xl font-display font-extrabold", bad ? "text-destructive" : "text-primary")}>
        {value}
      </p>
      <p className="text-xs text-muted-foreground">{label}</p>
    </div>
  )
}

function LogRow({ ev }: { ev: LiveEvent }) {
  return (
    <li className={cn(
      "rounded-md border-l-2 bg-popover/40 px-3 py-1.5 font-mono text-[12px] flex flex-wrap gap-2 items-baseline",
      ev.kind === "DailyPlanGenerated" && "border-l-primary",
      ev.kind === "StreakReset"        && "border-l-warning",
      ev.kind === "system"             && "border-l-muted-foreground opacity-80",
    )}>
      <span className="text-muted-foreground w-24 shrink-0">{ev.at.slice(11, 23)}</span>
      <Badge variant="outline" className="text-[10px] font-bold">{ev.kind}</Badge>
      {ev.kind === "DailyPlanGenerated" && (
        <span>
          user={ev.data.userId.slice(0, 8)}… · trail={ev.data.trailId} · meta={ev.data.metaDia}
          {ev.data.extraPenalty > 0 && <strong className="text-destructive"> · +{ev.data.extraPenalty} pen.</strong>}
          {" · ontem="}
          {ev.data.metGoalYesterday === null ? "—" : ev.data.metGoalYesterday ? "✓" : "✗"}
        </span>
      )}
      {ev.kind === "StreakReset" && (
        <span>user={ev.data.userId.slice(0, 8)}… · streak anterior={ev.data.previousStreak}</span>
      )}
      {ev.kind === "system" && <span>{ev.message}</span>}
    </li>
  )
}

function nowIso() { return new Date().toISOString() }
