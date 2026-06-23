import { useEffect } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Award, Check, Crown, Loader2, Lock, RefreshCw } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useAuth } from "@/stores/auth"
import { titlesApi } from "@/api/titles"
import { cn } from "@/lib/utils"

const CRITERION_LABEL: Record<string, string> = {
  StreakDays: "ofensiva",
  ArenaWins:  "vitórias na Arena",
  XpTotal:    "XP total",
  Manual:     "evento",
}

/**
 * Seção de Títulos no perfil: lista o catálogo (possuídos/bloqueados),
 * permite ativar o título exibido e tem um botão "Verificar conquistas"
 * (avalia streak/arena/xp). Avalia uma vez ao montar pra refletir o estado.
 */
export function TitlesSection() {
  const qc = useQueryClient()

  const titles = useQuery({ queryKey: ["titles"], queryFn: titlesApi.list })

  // Avalia concessões ao abrir o perfil (idempotente); se ganhou algo, recarrega.
  const evaluateMut = useMutation({
    mutationFn: titlesApi.evaluate,
    onSuccess: (r) => {
      if (r.granted.length > 0) {
        toast.success(`Novo título: ${r.granted.join(", ")} 🎉`)
        qc.invalidateQueries({ queryKey: ["titles"] })
        qc.invalidateQueries({ queryKey: ["profile", "me"] })
      } else {
        toast.info("Nenhum título novo por enquanto. Continue estudando!")
      }
    },
  })

  const evaluateRef = evaluateMut.mutate
  useEffect(() => { evaluateRef() }, [evaluateRef])

  const activateMut = useMutation({
    mutationFn: (id: number) => titlesApi.activate(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["titles"] })
      qc.invalidateQueries({ queryKey: ["profile", "me"] })
    },
    onError: () => toast.error("Não foi possível ativar o título."),
  })

  const owned = (titles.data ?? []).filter((t) => t.owned)
  const locked = (titles.data ?? []).filter((t) => !t.owned)

  return (
    <Card className="animate-pop-in" style={{ animationDelay: "200ms" }}>
      <CardHeader className="flex-row items-center justify-between">
        <CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
          <Award className="h-4 w-4" /> Títulos
        </CardTitle>
        <Button
          variant="outline"
          size="sm"
          onClick={() => evaluateMut.mutate()}
          disabled={evaluateMut.isPending}
        >
          <RefreshCw className={cn("mr-1 h-3.5 w-3.5", evaluateMut.isPending && "animate-spin")} />
          Verificar
        </Button>
      </CardHeader>
      <CardContent className="space-y-3">
        {titles.isLoading ? (
          <div className="space-y-2">{[0, 1, 2].map((i) => <Skeleton key={i} className="h-10 w-full" />)}</div>
        ) : (
          <>
            {owned.length > 0 && (
              <ul className="space-y-2">
                {owned.map((t) => (
                  <li
                    key={t.id}
                    className={cn(
                      "flex items-center gap-3 rounded-lg border px-3 py-2",
                      t.active ? "border-primary bg-primary/10" : "border-border",
                    )}
                  >
                    <Crown className={cn("h-4 w-4 shrink-0", t.active ? "text-primary" : "text-muted-foreground")} />
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-semibold truncate">{t.text}</p>
                      <p className="text-[11px] text-muted-foreground">por {CRITERION_LABEL[t.criterion] ?? t.criterion}</p>
                    </div>
                    {t.active ? (
                      <span className="flex items-center gap-1 text-xs font-medium text-primary">
                        <Check className="h-3.5 w-3.5" /> Em uso
                      </span>
                    ) : (
                      <Button size="sm" variant="ghost" onClick={() => activateMut.mutate(t.id)} disabled={activateMut.isPending}>
                        Usar
                      </Button>
                    )}
                  </li>
                ))}
                {owned.some((t) => t.active) && (
                  <li>
                    <Button size="sm" variant="ghost" className="text-muted-foreground" onClick={() => activateMut.mutate(0)}>
                      Não exibir título
                    </Button>
                  </li>
                )}
              </ul>
            )}

            {locked.length > 0 && (
              <div>
                <p className="mb-2 text-[11px] font-semibold uppercase text-muted-foreground">A desbloquear</p>
                <ul className="space-y-1.5">
                  {locked.map((t) => (
                    <li key={t.id} className="flex items-center gap-3 rounded-lg border border-dashed border-border/70 px-3 py-2 opacity-70">
                      <Lock className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                      <div className="min-w-0 flex-1">
                        <p className="text-sm truncate">{t.text}</p>
                        <p className="text-[11px] text-muted-foreground">
                          {CRITERION_LABEL[t.criterion] ?? t.criterion} ≥ {t.threshold}
                        </p>
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {owned.length === 0 && locked.length === 0 && (
              <p className="text-sm text-muted-foreground">Nenhum título no catálogo ainda.</p>
            )}
          </>
        )}
      </CardContent>
    </Card>
  )
}

/** Ranking global por XP (top N), destacando o usuário atual. */
export function GlobalRankingSection() {
  const me = useAuth((s) => s.user)?.id
  const ranking = useQuery({ queryKey: ["ranking", "global"], queryFn: () => titlesApi.globalRanking(20) })

  return (
    <Card className="animate-pop-in" style={{ animationDelay: "240ms" }}>
      <CardHeader>
        <CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
          <Crown className="h-4 w-4 text-warning" /> Ranking global (XP)
        </CardTitle>
      </CardHeader>
      <CardContent>
        {ranking.isLoading ? (
          <div className="space-y-2">{[0, 1, 2].map((i) => <Skeleton key={i} className="h-8 w-full" />)}</div>
        ) : (ranking.data ?? []).length === 0 ? (
          <p className="text-sm text-muted-foreground flex items-center gap-2"><Loader2 className="h-4 w-4" /> Sem dados.</p>
        ) : (
          <ol className="space-y-1">
            {ranking.data!.map((row) => (
              <li
                key={row.userId}
                className={cn(
                  "flex items-center gap-3 rounded-lg px-3 py-2 text-sm",
                  row.userId === me ? "bg-primary/10" : "odd:bg-muted/30",
                )}
              >
                <span className="w-6 text-center font-bold tabular-nums">
                  {row.rank === 1 ? <Crown className="mx-auto h-4 w-4 text-warning" /> : row.rank}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium">
                    {row.name} {row.userId === me && <span className="text-primary">(você)</span>}
                  </p>
                  {row.activeTitle && <p className="text-[11px] italic text-muted-foreground truncate">{row.activeTitle}</p>}
                </div>
                <span className="font-bold tabular-nums">{row.xp} XP</span>
              </li>
            ))}
          </ol>
        )}
      </CardContent>
    </Card>
  )
}
