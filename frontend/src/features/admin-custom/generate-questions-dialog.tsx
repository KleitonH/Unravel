import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { AlertTriangle, Loader2, Sparkles, Zap } from "lucide-react"
import { toast } from "sonner"
import { api } from "@/api/client"
import { tokensApi } from "@/api/tokens"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { YarnBall } from "@/components/yarn/yarn-ball"
import { cn } from "@/lib/utils"

/**
 * PR 52 — Modal genérico de geração de perguntas (por content OU por trilha).
 *
 * <para>Mostra ao admin antes de disparar:</para>
 * <list type="bullet">
 *   <item>Slider de quantidade (5-50, default 15)</item>
 *   <item>Toggle Urgente (custo 3× normal)</item>
 *   <item>Saldo atual de lã + custo total estimado + saldo após</item>
 *   <item>Bloqueia botão se saldo insuficiente</item>
 *   <item>Bloqueio gracioso se backend retornar 402</item>
 * </list>
 */
export function GenerateQuestionsDialog({
  open, onClose, scope, scopeId, scopeName, contentsCount = 1,
}: {
  open:          boolean
  onClose:       () => void
  /** "content" = por conteúdo individual (POST /api/admin/forge/{id})
   *  "trail"   = por trilha inteira  (POST /api/admin/forge/bulk?trailSlug=...) */
  scope:         "content" | "trail"
  /** contentId quando scope=content; trailSlug quando scope=trail */
  scopeId:       string | number
  scopeName:     string
  /** Apenas usado quando scope=trail pra calcular custo total */
  contentsCount?: number
}) {
  const qc = useQueryClient()
  const [maxPerContent, setMaxPerContent] = useState(15)
  const [urgent, setUrgent]               = useState(false)

  const balanceQuery = useQuery({
    queryKey: ["tokens", "balance"],
    queryFn:  tokensApi.balance,
    enabled:  open,
    staleTime: 10_000,
  })

  const costPerJob   = urgent ? 3 : 1
  const totalJobs    = scope === "trail" ? maxPerContent * contentsCount : maxPerContent
  const totalCost    = totalJobs * costPerJob
  const balance      = balanceQuery.data?.balanceCm ?? 0
  const hasEnough    = balance >= totalCost
  const balanceAfter = balance - totalCost
  const estimated    = Math.round(totalJobs * 0.69)

  const dispatchMutation = useMutation({
    mutationFn: async () => {
      const url = scope === "content"
        ? `/api/admin/forge/${scopeId}?max=${maxPerContent}&urgent=${urgent}`
        : `/api/admin/forge/bulk?trailSlug=${scopeId}&max=${maxPerContent}&urgent=${urgent}`
      return api.post(url).then((r) => r.data)
    },
    onSuccess: (data: any) => {
      const enqueued = data.totalQueued ?? data.enqueued ?? 0
      const spent    = data.tokensSpentCm ?? totalCost
      // PR 52a-2 — toast com referência ao batch criado.
      // O chip "Forge" no header começa a piscar/animar automaticamente
      // (polling da query ["forge","batches","recent"]) — usuário vê
      // progresso lá. Aqui só damos o trigger inicial.
      const short = data.batchId ? String(data.batchId).slice(0, 8) : null
      toast.success(
        `${enqueued} jobs enfileirados (−${spent} cm)`,
        {
          description: short
            ? `Batch ${short}… — acompanhe no chip "Forge" do header.`
            : undefined,
          duration: 6000,
        },
      )
      qc.invalidateQueries({ queryKey: ["tokens"] })
      // Força refresh imediato do chip do header pra ele já mostrar o
      // novo batch sem esperar o próximo tick de polling.
      qc.invalidateQueries({ queryKey: ["forge", "batches", "recent"] })
      onClose()
    },
    onError: (err: any) => {
      const status = err?.response?.status
      if (status === 402) {
        const body = err?.response?.data
        toast.error(`Saldo insuficiente: ${body?.balanceCm ?? "?"} cm. Necessário: ${body?.requiredCm ?? "?"} cm.`)
      } else {
        toast.error("Falha ao disparar geração.")
      }
    },
  })

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Sparkles className="h-5 w-5 text-primary" />
            Gerar perguntas — {scopeName}
          </DialogTitle>
          <DialogDescription>
            {scope === "content"
              ? "O OpenAI vai gerar perguntas LLM-grounded baseadas neste conteúdo."
              : `Bulk em todos os ${contentsCount} conteúdos da trilha.`}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Saldo atual */}
          {balanceQuery.isLoading ? (
            <Skeleton className="h-12" />
          ) : balanceQuery.data ? (
            <div className="flex items-center justify-between rounded-md border border-border bg-popover/40 p-3">
              <span className="text-xs text-muted-foreground uppercase tracking-wider">Seu saldo</span>
              <YarnBall
                tier={balanceQuery.data.tier}
                balanceCm={balanceQuery.data.balanceCm}
                displayBalance={balanceQuery.data.displayBalance}
                size="sm"
              />
            </div>
          ) : null}

          {/* Slider de quantidade */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label htmlFor="qcount" className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                {scope === "trail" ? "Perguntas por conteúdo" : "Quantas perguntas?"}
              </Label>
              <Badge variant="outline" className="text-[11px] font-bold tabular-nums">{maxPerContent}</Badge>
            </div>
            <input
              id="qcount"
              type="range"
              min={5}
              max={50}
              step={1}
              value={maxPerContent}
              onChange={(e) => setMaxPerContent(Number(e.target.value))}
              className="w-full accent-primary"
            />
            <div className="flex justify-between text-[10px] text-muted-foreground">
              <span>5</span><span>50</span>
            </div>
          </div>

          {/* Toggle urgente */}
          <label className="flex items-start gap-3 rounded-md border border-border p-3 cursor-pointer hover:bg-popover/40">
            <input
              type="checkbox"
              checked={urgent}
              onChange={(e) => setUrgent(e.target.checked)}
              className="mt-1 accent-warning"
            />
            <div className="flex-1">
              <div className="flex items-center gap-2 text-sm font-semibold">
                <Zap className="h-3.5 w-3.5 text-warning" />
                Urgente
                <Badge variant="outline" className="text-[10px] border-warning/40 text-warning">
                  3× custo
                </Badge>
              </div>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Preempta a fila atual e processa esses jobs primeiro. Custa 3 cm por job em vez de 1.
              </p>
            </div>
          </label>

          {/* Resumo */}
          <div className="rounded-md border border-border bg-popover/40 p-3 space-y-1.5">
            <Row label={`${totalJobs} jobs × ${costPerJob} cm`} value={`${totalCost} cm`} bold />
            <Row label="Yield estimado (~69%)" value={`≈ ${estimated} perguntas válidas`} />
            <Row label="Saldo após" value={`${balanceAfter} cm`}
                 tone={hasEnough ? "default" : "destructive"} />
          </div>

          {!hasEnough && balanceQuery.data && (
            <div className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-xs flex gap-2 items-start">
              <AlertTriangle className="h-4 w-4 text-destructive shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold text-destructive">Saldo insuficiente</p>
                <p className="text-foreground/80">
                  Faltam <strong>{Math.abs(balanceAfter)} cm</strong>. Reduza a quantidade,
                  desligue Urgente, ou aguarde refill / engajamento dos alunos pra ganhar mais lã.
                </p>
              </div>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancelar</Button>
          <Button
            onClick={() => dispatchMutation.mutate()}
            disabled={!hasEnough || dispatchMutation.isPending}
          >
            {dispatchMutation.isPending
              ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Disparando…</>
              : <><Sparkles className="h-4 w-4 mr-1" />Disparar geração</>}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function Row({ label, value, bold = false, tone = "default" }: {
  label: string; value: string; bold?: boolean
  tone?: "default" | "destructive"
}) {
  return (
    <div className="flex items-baseline justify-between text-xs">
      <span className="text-muted-foreground">{label}</span>
      <span className={cn(
        "tabular-nums",
        bold && "font-bold",
        tone === "destructive" && "text-destructive font-bold",
      )}>{value}</span>
    </div>
  )
}

