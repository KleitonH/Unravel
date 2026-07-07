import { useQuery } from "@tanstack/react-query"
import { Link } from "@tanstack/react-router"
import { ChevronRight } from "lucide-react"
import { partnershipsApi, type Partnership } from "@/api/partnerships"
import { Card, CardContent } from "@/components/ui/card"
import { YarnBall } from "@/components/novelo/yarn-ball"
import { cn } from "@/lib/utils"

/**
 * Widget do Novelo no Dashboard (Ideia 1 — "abaixo dos chips de progresso
 * pessoal"). Resume as parcerias ativas: mostra o novelo de cada uma, de
 * quem é a vez e a meta do dia, linkando pra aba de Parcerias.
 *
 * Não renderiza nada quando o aluno não tem parceria com novelo ativo —
 * assim não polui o dashboard de quem ainda não joga em dupla.
 */
export function NoveloWidget() {
  const q = useQuery({ queryKey: ["partnerships"], queryFn: partnershipsApi.list, staleTime: 60_000 })

  const withYarn = (q.data ?? []).filter((p) => p.yarn)
  if (q.isLoading || withYarn.length === 0) return null

  // No máximo 2 no widget; o resto vive na aba Parcerias.
  const shown = withYarn.slice(0, 2)

  return (
    <Link to="/parcerias" className="block">
      <Card className="transition-colors hover:border-primary/50">
        <CardContent className="flex items-center gap-4 py-3">
          <div className="flex items-center -space-x-3">
            {shown.map((p) => {
              const y = p.yarn!
              const pct = Math.min(100, Math.round((y.progress / Math.max(y.dailyGoal, 1)) * 100))
              return (
                <div key={p.id} className="rounded-full bg-card">
                  <YarnBall
                    pct={pct}
                    active={y.isMyTurn}
                    tangled={y.state === "Tangled"}
                    dropped={y.state === "Dropped"}
                    size={44}
                  />
                </div>
              )
            })}
          </div>

          <div className="min-w-0 flex-1">
            <p className="font-display text-sm font-bold">🧶 Novelo de Trilha</p>
            <p className="truncate text-xs text-muted-foreground">
              {summary(shown)}
            </p>
          </div>

          {withYarn.length > shown.length && (
            <span className="shrink-0 text-xs font-medium text-muted-foreground">
              +{withYarn.length - shown.length}
            </span>
          )}
          <ChevronRight className="h-5 w-5 shrink-0 text-muted-foreground" />
        </CardContent>
      </Card>
    </Link>
  )
}

/** Frase-resumo: prioriza a parceria em que é a vez do aluno. */
function summary(ps: Partnership[]): string {
  const mine = ps.find((p) => p.yarn!.isMyTurn)
  if (mine) {
    const y = mine.yarn!
    return `Sua vez com ${firstName(mine.partnerName)} — meta ${Math.min(y.progress, y.dailyGoal)}/${y.dailyGoal} hoje`
  }
  if (ps.length === 1) return `Aguardando ${firstName(ps[0].partnerName)} cumprir a meta…`
  return "Aguardando seus parceiros cumprirem a meta…"
}

function firstName(name: string): string {
  return name.trim().split(/\s+/)[0] ?? name
}
