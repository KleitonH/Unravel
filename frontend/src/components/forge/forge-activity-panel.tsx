import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import {
  AlertTriangle, ChevronLeft, ChevronRight, Clock, Loader2, Sparkles,
  CheckCircle2, XCircle, X,
} from "lucide-react"
import { forgeApi } from "@/api/forge"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type {
  ForgeBatchDetail, ForgeBatchJob, ForgeBatchSummary, ForgeJobStatus,
} from "@/types/forge"

/**
 * PR 52a-2 — Sheet lateral mostrando atividade do forge do moderador.
 *
 * **Layout**: drawer ancorado à direita (mobile: bottom sheet full-height).
 * Lista os últimos N batches; cada um expandível pra ver progresso, shape
 * breakdown, lista de prompts gerados + falhas.
 *
 * **Auto-refresh**: o query do chip ja faz polling 3s quando há jobs
 * ativos. Aqui o detalhe expandido tem seu próprio query polling — só
 * quando o batch ainda não está completo (`isComplete=false`).
 *
 * **Estados visuais**:
 * - Done (verde) — barra de progresso 100%
 * - Running (warning + pulse) — barra animada
 * - Failed > 0 → indicador de falha lateral
 */
export function ForgeActivityPanel({
  batches, isLoading, onClose,
}: {
  batches:    ForgeBatchSummary[]
  isLoading:  boolean
  onClose:    () => void
}) {
  const [selectedBatchId, setSelectedBatchId] = useState<string | null>(null)
  const selected = selectedBatchId
    ? batches.find((b) => b.batchId === selectedBatchId) ?? null
    : null

  return (
    <>
      {/* Overlay */}
      <div
        className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm animate-in fade-in-0"
        onClick={onClose}
      />

      {/* Drawer */}
      <aside
        className={cn(
          "fixed right-0 top-0 z-50 h-full w-full sm:max-w-md",
          "bg-card border-l border-border shadow-2xl",
          "flex flex-col",
          "animate-in slide-in-from-right duration-200",
        )}
        role="dialog"
        aria-label="Atividade do forge"
      >
        <header className="flex items-center justify-between border-b border-border px-5 py-3">
          {selected ? (
            <button
              type="button"
              onClick={() => setSelectedBatchId(null)}
              className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
            >
              <ChevronLeft className="h-4 w-4" />
              Voltar
            </button>
          ) : (
            <div className="flex items-center gap-2">
              <Sparkles className="h-4 w-4 text-primary" />
              <h2 className="font-display font-semibold">Atividade do Forge</h2>
            </div>
          )}
          <Button variant="ghost" size="icon" onClick={onClose} aria-label="Fechar">
            <X className="h-4 w-4" />
          </Button>
        </header>

        <div className="flex-1 overflow-y-auto p-4">
          {selected
            ? <BatchDetailView batchId={selected.batchId} summary={selected} />
            : <BatchList batches={batches} isLoading={isLoading} onSelect={setSelectedBatchId} />}
        </div>
      </aside>
    </>
  )
}

// ── Lista de batches ────────────────────────────────────────────────

function BatchList({
  batches, isLoading, onSelect,
}: {
  batches:   ForgeBatchSummary[]
  isLoading: boolean
  onSelect:  (batchId: string) => void
}) {
  if (isLoading && batches.length === 0) {
    return (
      <div className="space-y-2">
        {[1, 2, 3].map((i) => <Skeleton key={i} className="h-20" />)}
      </div>
    )
  }
  if (batches.length === 0) {
    return (
      <div className="text-center py-10 text-sm text-muted-foreground">
        <Sparkles className="h-8 w-8 mx-auto mb-2 opacity-40" />
        <p>Nenhum batch ainda.</p>
        <p className="text-xs mt-1">Dispare uma geração pra ver o progresso aqui.</p>
      </div>
    )
  }

  return (
    <ul className="space-y-2">
      {batches.map((b) => <BatchRow key={b.batchId} batch={b} onClick={() => onSelect(b.batchId)} />)}
    </ul>
  )
}

function BatchRow({ batch, onClick }: { batch: ForgeBatchSummary; onClick: () => void }) {
  const active   = batch.pending > 0 || batch.running > 0
  const allDone  = !active && batch.failed === 0
  const hasFail  = batch.failed > 0
  const progress = batch.total === 0 ? 0 : (batch.done + batch.failed) / batch.total

  return (
    <li>
      <button
        type="button"
        onClick={onClick}
        className="w-full text-left rounded-md border border-border bg-popover/40 p-3 hover:border-primary/40 transition-colors group"
      >
        <div className="flex items-center justify-between gap-2 mb-2">
          <div className="flex items-center gap-2 min-w-0">
            {active && <Loader2 className="h-3.5 w-3.5 text-warning animate-spin shrink-0" />}
            {allDone && <CheckCircle2 className="h-3.5 w-3.5 text-success shrink-0" />}
            {!active && hasFail && <AlertTriangle className="h-3.5 w-3.5 text-destructive shrink-0" />}
            <p className="text-xs font-mono truncate">{batch.batchId.slice(0, 8)}…</p>
          </div>
          <span className="text-[10px] text-muted-foreground shrink-0">{fmtAgo(batch.enqueuedAt)}</span>
        </div>

        {/* Barra de progresso */}
        <div className="h-1.5 rounded-full bg-muted/40 overflow-hidden">
          <div
            className={cn(
              "h-full transition-all duration-500",
              allDone     && "bg-success",
              active      && "bg-warning",
              hasFail && !active && "bg-destructive",
            )}
            style={{ width: `${Math.round(progress * 100)}%` }}
          />
        </div>

        <div className="mt-2 flex items-center gap-2 text-[11px] text-muted-foreground">
          <span><strong className="text-foreground">{batch.done}</strong>/{batch.total} ok</span>
          {batch.running > 0 && <span className="text-warning">· {batch.running} processando</span>}
          {batch.pending > 0 && <span>· {batch.pending} fila</span>}
          {batch.failed  > 0 && <span className="text-destructive">· {batch.failed} falha{batch.failed > 1 ? "s" : ""}</span>}
          <ChevronRight className="h-3 w-3 ml-auto opacity-60 group-hover:opacity-100" />
        </div>
      </button>
    </li>
  )
}

// ── Detalhe de batch ────────────────────────────────────────────────

function BatchDetailView({
  batchId, summary,
}: {
  batchId: string
  summary: ForgeBatchSummary
}) {
  const detailQuery = useQuery({
    queryKey: ["forge", "batch", batchId],
    queryFn:  () => forgeApi.batch(batchId),
    refetchInterval: (q) => {
      const d = q.state.data
      if (!d) return 3_000
      return d.isComplete ? false : 3_000   // false = stop polling
    },
    staleTime: 1_000,
  })

  const detail = detailQuery.data
  if (!detail && detailQuery.isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-16" />
        <Skeleton className="h-32" />
      </div>
    )
  }
  if (!detail) return <p className="text-sm text-destructive">Falha ao carregar batch.</p>

  return (
    <div className="space-y-4">
      <BatchHeader detail={detail} summary={summary} />
      <ShapeBreakdown detail={detail} />
      <JobList jobs={detail.jobs} />
    </div>
  )
}

function BatchHeader({ detail, summary: _summary }: { detail: ForgeBatchDetail; summary: ForgeBatchSummary }) {
  const { counts, total, isComplete } = detail
  const progress = total === 0 ? 0 : (counts.done + counts.failed) / total
  const validRate = (counts.done + counts.failed) === 0
    ? null
    : counts.done / (counts.done + counts.failed)

  return (
    <section className="space-y-3">
      <div className="flex items-baseline justify-between">
        <p className="text-xs font-mono text-muted-foreground">{detail.batchId.slice(0, 12)}…</p>
        <span className="text-[10px] text-muted-foreground">{fmtAgo(detail.enqueuedAt)}</span>
      </div>

      <div className="h-2 rounded-full bg-muted/40 overflow-hidden">
        <div
          className={cn(
            "h-full transition-all duration-500",
            isComplete && counts.failed === 0 && "bg-success",
            !isComplete && "bg-warning",
            isComplete && counts.failed > 0   && "bg-destructive",
          )}
          style={{ width: `${Math.round(progress * 100)}%` }}
        />
      </div>

      <div className="grid grid-cols-4 gap-2 text-center">
        <Stat label="Total" value={total} />
        <Stat label="OK" value={counts.done} tone="success" />
        <Stat label="Falhas" value={counts.failed} tone={counts.failed > 0 ? "destructive" : "muted"} />
        <Stat label="Em fila" value={counts.pending + counts.running} tone={counts.pending + counts.running > 0 ? "warning" : "muted"} />
      </div>

      {validRate !== null && (
        <p className="text-[11px] text-center text-muted-foreground">
          Yield: <strong className="text-foreground">{Math.round(validRate * 100)}%</strong>
          {" "}({counts.done} válidas / {counts.done + counts.failed} processadas)
        </p>
      )}
    </section>
  )
}

function ShapeBreakdown({ detail }: { detail: ForgeBatchDetail }) {
  const entries = Object.entries(detail.shapeBreakdown).filter(([, n]) => (n ?? 0) > 0)
  if (entries.length === 0) return null

  return (
    <section className="rounded-md border border-border bg-popover/40 p-3 space-y-2">
      <h3 className="text-[10px] font-display font-bold uppercase tracking-wider text-muted-foreground">
        Por shape (válidas)
      </h3>
      <div className="flex flex-wrap gap-1.5">
        {entries.map(([shape, count]) => (
          <Badge
            key={shape}
            variant="outline"
            className={cn(
              "text-[10px] gap-1",
              shape === "FillInTheBlank" && "border-primary/40 text-primary",
              shape === "MultipleChoice" && "border-success/40 text-success",
            )}
          >
            {shape === "FillInTheBlank" ? "🧩" : shape === "MultipleChoice" ? "📋" : "❓"}
            {shape} · <strong>{count}</strong>
          </Badge>
        ))}
      </div>
    </section>
  )
}

function JobList({ jobs }: { jobs: ForgeBatchJob[] }) {
  return (
    <section className="space-y-1">
      <h3 className="text-[10px] font-display font-bold uppercase tracking-wider text-muted-foreground mb-2">
        Jobs ({jobs.length})
      </h3>
      <ul className="space-y-1.5">
        {jobs.map((j) => <JobRow key={j.id} job={j} />)}
      </ul>
    </section>
  )
}

function JobRow({ job }: { job: ForgeBatchJob }) {
  return (
    <li className={cn(
      "rounded-md border px-2.5 py-2 text-xs space-y-1",
      job.status === "Done"    && "border-success/30 bg-success/5",
      job.status === "Failed"  && "border-destructive/30 bg-destructive/5",
      job.status === "Running" && "border-warning/40 bg-warning/5",
      job.status === "Pending" && "border-border bg-popover/30",
    )}>
      <div className="flex items-center justify-between gap-2">
        <span className="font-mono text-[10px] truncate">{job.contentTitle}</span>
        <StatusBadge status={job.status} />
      </div>
      {job.prompt && (
        <p className="text-[11px] text-muted-foreground line-clamp-2">{job.prompt}</p>
      )}
      {job.lastError && (
        <p className="text-[10px] text-destructive line-clamp-2 font-mono">⚠ {job.lastError}</p>
      )}
      {job.shape && (
        <Badge variant="outline" className={cn(
          "text-[9px]",
          job.shape === "FillInTheBlank" && "border-primary/40 text-primary",
        )}>
          {job.shape === "FillInTheBlank" ? "🧩 fill-blank" : "📋 mcq"}
        </Badge>
      )}
    </li>
  )
}

function StatusBadge({ status }: { status: ForgeJobStatus }) {
  const map: Record<ForgeJobStatus, { icon: React.ReactNode; cls: string; label: string }> = {
    Pending: { icon: <Clock        className="h-3 w-3" />, cls: "text-muted-foreground", label: "fila" },
    Running: { icon: <Loader2      className="h-3 w-3 animate-spin" />, cls: "text-warning", label: "rodando" },
    Done:    { icon: <CheckCircle2 className="h-3 w-3" />, cls: "text-success", label: "ok" },
    Failed:  { icon: <XCircle      className="h-3 w-3" />, cls: "text-destructive", label: "falha" },
  }
  const m = map[status]
  return (
    <span className={cn("inline-flex items-center gap-1 text-[10px] font-semibold", m.cls)}>
      {m.icon}{m.label}
    </span>
  )
}

function Stat({ label, value, tone = "default" }: {
  label: string; value: number
  tone?: "default" | "success" | "warning" | "destructive" | "muted"
}) {
  return (
    <div className="rounded-md border border-border bg-popover/40 px-1.5 py-2">
      <p className={cn(
        "font-display text-lg font-extrabold",
        tone === "success"     && "text-success",
        tone === "warning"     && "text-warning",
        tone === "destructive" && "text-destructive",
        tone === "muted"       && "text-muted-foreground",
      )}>{value}</p>
      <p className="text-[9px] uppercase tracking-wider text-muted-foreground">{label}</p>
    </div>
  )
}

// ── helpers ────────────────────────────────────────────────────────

function fmtAgo(iso: string): string {
  const ms = Date.now() - new Date(iso).getTime()
  const sec = Math.floor(ms / 1000)
  if (sec < 60)  return `${sec}s`
  const min = Math.floor(sec / 60)
  if (min < 60)  return `${min}min`
  const hr = Math.floor(min / 60)
  if (hr < 24)   return `${hr}h`
  const day = Math.floor(hr / 24)
  return `${day}d`
}
