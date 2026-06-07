import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { Activity, Loader2 } from "lucide-react"
import { forgeApi } from "@/api/forge"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import { ForgeActivityPanel } from "./forge-activity-panel"

/**
 * PR 52a-2 — Chip "Atividade do Forge" pro header das telas admin.
 * Mostra contagem total de batches recentes; quando há jobs ativos
 * (pending OU running > 0 em algum batch dos últimos N), anima
 * com spinner e cor warning pra chamar atenção.
 *
 * Click abre o `<ForgeActivityPanel />` (Sheet lateral).
 *
 * **Polling**:
 * - 3s quando algum batch tem jobs ativos (worker ainda processando)
 * - 30s quando tudo concluído (refresh ocasional, não fica martelando)
 *
 * **Threshold**: lista os 10 batches mais recentes (configurável).
 */
export function ForgeActivityChip({ take = 10 }: { take?: number }) {
  const [open, setOpen] = useState(false)

  const batchesQuery = useQuery({
    queryKey: ["forge", "batches", "recent", take],
    queryFn:  () => forgeApi.recentBatches(take),
    refetchInterval: (q) => {
      const data = q.state.data
      if (!data || data.length === 0) return 30_000
      // Algum batch tem trabalho pendente?
      const active = data.some((b) => b.pending > 0 || b.running > 0)
      return active ? 3_000 : 30_000
    },
    staleTime: 1_000,
  })

  const batches = batchesQuery.data ?? []
  const activeCount = batches.filter((b) => b.pending > 0 || b.running > 0).length
  const hasActive   = activeCount > 0

  // Não renderiza nada se nunca houve batches (moderador novo sem
  // histórico nenhum) — evita poluir o header com chip sempre vazio.
  if (!batchesQuery.isLoading && batches.length === 0) return null

  return (
    <>
      <Button
        variant="outline"
        size="sm"
        onClick={() => setOpen(true)}
        className={cn(
          "h-9 gap-2 px-3 transition-colors",
          hasActive && "border-warning/60 text-warning hover:bg-warning/10",
        )}
        aria-label={hasActive ? `${activeCount} batch(es) em andamento` : "Atividade do forge"}
      >
        {hasActive
          ? <Loader2 className="h-4 w-4 animate-spin" />
          : <Activity className="h-4 w-4" />}
        <span className="text-xs font-semibold">Forge</span>
        {hasActive && (
          <Badge
            variant="outline"
            className="h-5 px-1.5 text-[10px] font-bold border-warning/60 text-warning bg-warning/10"
          >
            {activeCount}
          </Badge>
        )}
      </Button>

      {open && (
        <ForgeActivityPanel
          batches={batches}
          isLoading={batchesQuery.isLoading}
          onClose={() => setOpen(false)}
        />
      )}
    </>
  )
}
