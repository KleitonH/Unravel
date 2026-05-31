import { useQuery, useQueryClient } from "@tanstack/react-query"
import { useNavigate, useParams } from "@tanstack/react-router"
import { Flame, RefreshCw, Sparkles, Zap } from "lucide-react"
import { toast } from "sonner"
import { journeyApi } from "@/api/journey"
import { useJourneyHub } from "@/hooks/use-journey-hub"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { JourneyItem, JourneyReason } from "@/types/api"

const REASON_META: Record<JourneyReason, { label: string; Icon: typeof Sparkles; variant: "newlearning" | "duereview" | "reinforce" }> = {
  NewLearning: { label: "Novo",    Icon: Sparkles, variant: "newlearning" },
  DueReview:   { label: "Revisão", Icon: RefreshCw, variant: "duereview" },
  Reinforce:   { label: "Reforço", Icon: Zap,      variant: "reinforce" },
}

export function JornadaPage() {
  const { trailId } = useParams({ from: "/authed/jornada/$trailId" })
  const trailIdNum = Number(trailId)
  const navigate = useNavigate()
  const qc = useQueryClient()

  const planQuery = useQuery({
    queryKey: ["journey", "today", trailIdNum],
    queryFn:  () => journeyApi.today(trailIdNum),
  })

  // SignalR: quando o cron emitir DailyPlanGenerated pra essa trilha,
  // invalidamos o cache → re-fetch automático.
  useJourneyHub({
    onDailyPlanGenerated: (e) => {
      if (e.trailId === trailIdNum) {
        toast.info(`📅 Nova meta gerada: ${e.metaDia} desafios${e.extraPenalty > 0 ? ` (+${e.extraPenalty} de penalidade)` : ""}`)
        qc.invalidateQueries({ queryKey: ["journey", "today", trailIdNum] })
      }
    },
    onStreakReset: (e) => {
      toast.warning(`🔥 Sua sequência foi resetada (era ${e.previousStreak} dias)`)
    },
  })

  return (
    <div className="p-6 lg:p-10 max-w-4xl space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-display font-extrabold tracking-tight">
            📍 Sua jornada hoje
          </h1>
          {planQuery.data && <p className="text-muted-foreground mt-1">{planQuery.data.trailName}</p>}
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => planQuery.refetch()}
          disabled={planQuery.isFetching}
        >
          <RefreshCw className={cn("h-4 w-4 mr-1", planQuery.isFetching && "animate-spin")} />
          Atualizar
        </Button>
      </header>

      {planQuery.isLoading ? (
        <SkeletonView />
      ) : planQuery.isError ? (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardHeader>
            <CardTitle className="text-base text-destructive">Não foi possível carregar</CardTitle>
            <CardDescription>
              Trilha não encontrada ou você ainda não fez onboarding nela.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button onClick={() => navigate({ to: "/onboarding" })}>Ir pro onboarding</Button>
          </CardContent>
        </Card>
      ) : planQuery.data && (
        <>
          {/* Card de meta */}
          <Card className="bg-gradient-to-br from-primary/15 via-card to-card border-primary/20">
            <CardContent className="pt-6 flex items-baseline gap-3">
              <Flame className="h-6 w-6 text-primary" />
              <span className="text-5xl font-display font-extrabold text-primary leading-none">
                {planQuery.data.metaDia}
              </span>
              <span className="text-muted-foreground">
                desafio{planQuery.data.metaDia === 1 ? "" : "s"} hoje
              </span>
            </CardContent>
            <CardContent className="pt-0">
              <p className="text-sm text-muted-foreground">
                Calibrado pelo seu nível atual, suas vidas e o que está próximo da revisão.
              </p>
            </CardContent>
          </Card>

          {/* Para hoje */}
          <section className="space-y-3">
            <h2 className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">
              Para hoje
            </h2>
            {planQuery.data.today.length === 0 ? (
              <Card><CardContent className="py-6 text-center text-muted-foreground">
                Nada novo por agora. Volte mais tarde 🐾
              </CardContent></Card>
            ) : (
              <ul className="space-y-2">
                {planQuery.data.today.map((item) =>
                  <ItemRow key={item.topicId} item={item} ghost={false}
                    onPraticar={() => navigate({ to: "/quiz/$contentId", params: { contentId: String(item.contentId) } })}
                  />)}
              </ul>
            )}
          </section>

          {/* Em breve */}
          {planQuery.data.upcoming.length > 0 && (
            <section className="space-y-3">
              <h2 className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">
                Em breve
              </h2>
              <ul className="space-y-2">
                {planQuery.data.upcoming.map((item) =>
                  <ItemRow key={item.topicId} item={item} ghost />)}
              </ul>
            </section>
          )}
        </>
      )}
    </div>
  )
}

function ItemRow({ item, ghost, onPraticar }: { item: JourneyItem; ghost: boolean; onPraticar?: () => void }) {
  const meta = REASON_META[item.reason]
  return (
    <li>
      <Card
        className={cn(
          "border-l-4 transition-opacity",
          ghost && "opacity-70",
        )}
        style={{
          borderLeftColor:
            meta.variant === "newlearning" ? "hsl(var(--primary))"
          : meta.variant === "duereview"   ? "hsl(var(--warning))"
          :                                  "hsl(var(--success))",
        }}
      >
        <CardContent className="py-3 flex items-center gap-3">
          <meta.Icon className="h-5 w-5 text-muted-foreground shrink-0" />
          <div className="flex-1 min-w-0">
            <h3 className="text-sm font-semibold truncate">{item.title}</h3>
            <div className="flex gap-1.5 flex-wrap mt-1">
              <Badge variant={meta.variant} className="text-[10px]">{meta.label}</Badge>
              <Badge variant="outline" className="text-[10px]">
                Domínio: {Math.round(item.effectiveMastery * 100)}%
              </Badge>
              <Badge variant="outline" className="text-[10px]">
                Dificuldade: {Math.round(item.difficultyScore * 100)}%
              </Badge>
            </div>
          </div>
          {!ghost && onPraticar && (
            <Button size="sm" onClick={onPraticar}>Praticar →</Button>
          )}
        </CardContent>
      </Card>
    </li>
  )
}

function SkeletonView() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-28 w-full" />
      <Skeleton className="h-5 w-32" />
      {[1, 2, 3].map((i) => <Skeleton key={i} className="h-20 w-full" />)}
    </div>
  )
}
