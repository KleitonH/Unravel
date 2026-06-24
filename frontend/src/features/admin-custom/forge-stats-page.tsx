import { useQuery } from "@tanstack/react-query"
import { Link } from "@tanstack/react-router"
import { Activity, AlertTriangle, CheckCircle2, ChevronLeft, Clock, Loader2, Sparkles, XCircle } from "lucide-react"
import { api } from "@/api/client"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { QuestionShape } from "@/types/api"

/**
 * PR 34d — Dashboard agregado do forge do moderador. Mostra totais,
 * yield rate, breakdown por shape (stacked bar) e top motivos de falha.
 *
 * Complementa o `<ForgeActivityPanel />` (drawer com batches individuais):
 * o drawer foca em "o que está acontecendo agora"; essa página foca em
 * "como meu forge se comportou no agregado".
 *
 * **Polling**: 10s — slow refresh, dados agregados não mudam tão rápido.
 */
type ForgeStats = {
  totalBatches:      number
  totalJobs:         number
  doneJobs:          number
  failedJobs:        number
  pendingJobs:       number
  runningJobs:       number
  yieldRate:         number | null
  shapeCounts:       Partial<Record<QuestionShape, number>>
  topFailureReasons: { reason: string; count: number }[]
}

export function ForgeStatsPage() {
  const statsQuery = useQuery({
    queryKey: ["forge", "stats"],
    queryFn:  () => api.get<ForgeStats>("/api/admin/forge/stats").then((r) => r.data),
    refetchInterval: 10_000,
    staleTime: 5_000,
  })

  const s = statsQuery.data

  return (
    <div className="mx-auto w-full max-w-6xl p-6 lg:p-10 space-y-5">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/admin/trails">
          <ChevronLeft className="h-4 w-4 mr-1" />
          Voltar
        </Link>
      </Button>

      <header className="space-y-1">
        <h1 className="text-3xl font-display font-extrabold tracking-tight flex items-center gap-2">
          <Activity className="h-7 w-7 text-primary" />
          Estatísticas do Forge
        </h1>
        <p className="text-sm text-muted-foreground">
          Visão agregada de todos os batches que você disparou. Use a aba{" "}
          <strong>Atividade</strong> no header pra acompanhar batches em
          tempo real.
        </p>
      </header>

      {statsQuery.isLoading && (
        <div className="space-y-3">
          <Skeleton className="h-24" />
          <Skeleton className="h-40" />
          <Skeleton className="h-60" />
        </div>
      )}

      {statsQuery.error && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardContent className="pt-6 text-sm text-destructive">
            Falha ao carregar estatísticas.
          </CardContent>
        </Card>
      )}

      {s && (
        <>
          {/* Stats principais */}
          <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
            <StatCard label="Batches" value={s.totalBatches} icon={<Sparkles className="h-4 w-4" />} />
            <StatCard label="Total jobs" value={s.totalJobs} icon={<Activity className="h-4 w-4" />} />
            <StatCard label="OK" value={s.doneJobs} icon={<CheckCircle2 className="h-4 w-4" />} tone="success" />
            <StatCard label="Falhas" value={s.failedJobs} icon={<XCircle className="h-4 w-4" />} tone="destructive" />
            <StatCard
              label="Em fila/proc."
              value={s.pendingJobs + s.runningJobs}
              icon={s.runningJobs > 0
                ? <Loader2 className="h-4 w-4 animate-spin" />
                : <Clock className="h-4 w-4" />}
              tone={s.pendingJobs + s.runningJobs > 0 ? "warning" : "muted"}
            />
          </div>

          {/* Yield */}
          {s.yieldRate !== null && (
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base">Yield acumulado</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                <p className="font-display font-extrabold text-5xl">
                  {Math.round(s.yieldRate * 100)}<span className="text-xl text-muted-foreground">%</span>
                </p>
                <p className="text-xs text-muted-foreground">
                  {s.doneJobs} válidas / {s.doneJobs + s.failedJobs} processadas.
                  Baseline esperado: <strong>~69%</strong> (PR 33h).
                  {s.yieldRate >= 0.65 && <span className="text-success ml-1">↑ acima do baseline</span>}
                  {s.yieldRate < 0.50 && <span className="text-destructive ml-1">↓ abaixo — investigar topo de falhas</span>}
                </p>
              </CardContent>
            </Card>
          )}

          {/* Shape breakdown */}
          <ShapeBreakdownCard shapeCounts={s.shapeCounts} totalDone={s.doneJobs} />

          {/* Top failure reasons */}
          <TopFailuresCard reasons={s.topFailureReasons} totalFailed={s.failedJobs} />
        </>
      )}
    </div>
  )
}

function StatCard({ label, value, icon, tone = "default" }: {
  label: string; value: number; icon: React.ReactNode
  tone?: "default" | "success" | "destructive" | "warning" | "muted"
}) {
  return (
    <Card className={cn(
      "transition-colors",
      tone === "warning" && "border-warning/40 bg-warning/5",
    )}>
      <CardContent className="pt-4 pb-3">
        <div className={cn(
          "flex items-center gap-1.5 text-xs text-muted-foreground mb-1",
          tone === "success"     && "text-success",
          tone === "destructive" && "text-destructive",
          tone === "warning"     && "text-warning",
        )}>
          {icon}<span className="uppercase tracking-wider font-semibold">{label}</span>
        </div>
        <p className={cn(
          "font-display font-extrabold text-2xl",
          tone === "success"     && "text-success",
          tone === "destructive" && "text-destructive",
          tone === "warning"     && "text-warning",
          tone === "muted"       && "text-muted-foreground",
        )}>{value.toLocaleString("pt-BR")}</p>
      </CardContent>
    </Card>
  )
}

function ShapeBreakdownCard({
  shapeCounts, totalDone,
}: {
  shapeCounts: Partial<Record<QuestionShape, number>>
  totalDone: number
}) {
  const entries = Object.entries(shapeCounts).filter(([, n]) => (n ?? 0) > 0) as [QuestionShape, number][]
  if (entries.length === 0) {
    return (
      <Card>
        <CardHeader className="pb-3"><CardTitle className="text-base">Por shape</CardTitle></CardHeader>
        <CardContent>
          <p className="text-xs text-muted-foreground">Nenhuma pergunta válida ainda — dispare um forge pra começar.</p>
        </CardContent>
      </Card>
    )
  }

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center justify-between">
          <span>Por shape (válidas)</span>
          <Badge variant="outline" className="text-[10px]">{totalDone} total</Badge>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {/* Stacked bar */}
        <div className="flex h-3 rounded-full overflow-hidden border border-border">
          {entries.map(([shape, count]) => {
            const pct = totalDone === 0 ? 0 : (count / totalDone) * 100
            return (
              <div
                key={shape}
                className={cn(
                  "transition-all duration-500",
                  shape === "MultipleChoice"    && "bg-success",
                  shape === "FillInTheBlank"    && "bg-primary",
                  shape === "TrueFalseGrounded" && "bg-warning",
                )}
                style={{ width: `${pct}%` }}
                title={`${shape}: ${count} (${pct.toFixed(1)}%)`}
              />
            )
          })}
        </div>

        {/* Legenda */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          {entries.map(([shape, count]) => {
            const pct = totalDone === 0 ? 0 : (count / totalDone) * 100
            return (
              <div key={shape} className="flex items-center justify-between rounded-md border border-border bg-popover/40 px-3 py-2">
                <div className="flex items-center gap-2 text-sm">
                  <span className={cn(
                    "h-2.5 w-2.5 rounded-full inline-block",
                    shape === "MultipleChoice"    && "bg-success",
                    shape === "FillInTheBlank"    && "bg-primary",
                    shape === "TrueFalseGrounded" && "bg-warning",
                  )} />
                  <span className="font-medium">
                    {shape === "FillInTheBlank" ? "🧩 Fill-in-the-blank"
                     : shape === "MultipleChoice" ? "📋 Multiple choice"
                     : "❓ " + shape}
                  </span>
                </div>
                <div className="text-xs">
                  <strong className="text-foreground">{count}</strong>
                  <span className="text-muted-foreground ml-1">({pct.toFixed(0)}%)</span>
                </div>
              </div>
            )
          })}
        </div>
      </CardContent>
    </Card>
  )
}

function TopFailuresCard({
  reasons, totalFailed,
}: {
  reasons: { reason: string; count: number }[]
  totalFailed: number
}) {
  if (reasons.length === 0) {
    return (
      <Card>
        <CardHeader className="pb-3"><CardTitle className="text-base">Top motivos de falha</CardTitle></CardHeader>
        <CardContent>
          <p className="text-xs text-success flex items-center gap-1.5">
            <CheckCircle2 className="h-4 w-4" />Nenhuma falha registrada — bom trabalho!
          </p>
        </CardContent>
      </Card>
    )
  }

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center justify-between">
          <span className="flex items-center gap-1.5">
            <AlertTriangle className="h-4 w-4 text-warning" />Top motivos de falha
          </span>
          <Badge variant="outline" className="text-[10px]">{totalFailed} falhas totais</Badge>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="space-y-1.5">
          {reasons.map((r) => {
            const pct = totalFailed === 0 ? 0 : (r.count / totalFailed) * 100
            return (
              <li key={r.reason} className="flex items-center gap-3 rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center justify-between gap-2 text-sm">
                    <span className="font-mono font-semibold text-destructive truncate">{r.reason}</span>
                    <span className="text-xs shrink-0">
                      <strong>{r.count}</strong>
                      <span className="text-muted-foreground ml-1">({pct.toFixed(0)}%)</span>
                    </span>
                  </div>
                  {/* Mini progress bar */}
                  <div className="mt-1 h-1 rounded-full bg-muted/40 overflow-hidden">
                    <div className="h-full bg-destructive transition-all duration-500"
                         style={{ width: `${pct}%` }} />
                  </div>
                </div>
              </li>
            )
          })}
        </ul>
        <p className="text-[11px] text-muted-foreground mt-3">
          {KNOWN_REASONS_HINT}
        </p>
      </CardContent>
    </Card>
  )
}

/**
 * Dica curta sobre o que cada bucket de erro significa. Estática agora;
 * futuramente vira tooltip por item com explicação específica.
 */
const KNOWN_REASONS_HINT =
  "Buckets comuns: LlmEmpty (modelo travou ou key inválida), AnswerLeakage " +
  "(prompt vazou a resposta), AnswerNotGrounded (cosine baixo vs chunk), " +
  "DistractorsPoor (distratores muito similares), SchemaInvalid (JSON malformado)."
