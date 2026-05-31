import { useQuery, useQueries } from "@tanstack/react-query"
import { Link, useNavigate } from "@tanstack/react-router"
import { Coins, Flame, Heart, Plus, Star } from "lucide-react"
import { useAuth } from "@/stores/auth"
import { trailsApi } from "@/api/trails"
import { journeyApi } from "@/api/journey"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import type { JourneyPlan, JourneyReason, Trail } from "@/types/api"

const REASON_LABEL: Record<JourneyReason, string> = {
  NewLearning: "Novo",
  DueReview: "Revisão",
  Reinforce: "Reforço",
}

/**
 * Dashboard = saudação + stats do user + cards por trilha inscrita
 * (cada um mostrando metaDia + 2 itens do today + CTA p/ /jornada).
 */
export function DashboardPage() {
  const user        = useAuth((s) => s.user)
  const navigate    = useNavigate()

  const trailsQuery = useQuery({
    queryKey: ["trails"],
    queryFn:  trailsApi.list,
  })
  const enrolled = (trailsQuery.data ?? []).filter((t) => t.userProgress >= 0)

  // Um query por trilha inscrita — TanStack paraleliza, falha isolada não derruba outras.
  const journeyQueries = useQueries({
    queries: enrolled.map((t) => ({
      queryKey: ["journey", "today", t.id] as const,
      queryFn:  () => journeyApi.today(t.id),
      retry: false,
    })),
  })

  const isLoading = trailsQuery.isLoading

  return (
    <div className="p-6 lg:p-10 space-y-6">
      <Hero name={user?.name ?? "estudante"} />

      {isLoading ? (
        <SkeletonList />
      ) : enrolled.length === 0 ? (
        <EmptyState onStart={() => navigate({ to: "/onboarding" })} />
      ) : (
        <section className="space-y-4">
          <header className="flex items-center justify-between">
            <h2 className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">
              Suas trilhas
            </h2>
            <Button variant="link" size="sm" asChild>
              <Link to="/onboarding"><Plus className="h-4 w-4 mr-1" />Adicionar</Link>
            </Button>
          </header>

          <div className="grid gap-4 md:grid-cols-2">
            {enrolled.map((trail, i) => (
              <TrailCard
                key={trail.id}
                trail={trail}
                plan={journeyQueries[i].data ?? null}
                loading={journeyQueries[i].isLoading}
              />
            ))}
          </div>
        </section>
      )}
    </div>
  )
}

function Hero({ name }: { name: string }) {
  // PR 24 vai trazer o profile real; por enquanto stats vazios — não
  // criamos fake pra evitar confundir o usuário.
  return (
    <Card className="bg-gradient-to-br from-primary/10 via-card to-card border-primary/20">
      <CardHeader className="flex-row items-center justify-between gap-4">
        <div>
          <CardTitle className="text-2xl">Olá, {name}! 👋</CardTitle>
          <CardDescription>Seu plano do dia está abaixo.</CardDescription>
        </div>
        <div className="hidden sm:flex gap-2">
          <Stat icon={<Star className="h-4 w-4" />}  value="0" label="XP" />
          <Stat icon={<Flame className="h-4 w-4" />} value="0" label="dias" />
          <Stat icon={<Heart className="h-4 w-4" />} value="5" label="vidas" />
        </div>
      </CardHeader>
    </Card>
  )
}

function Stat({ icon, value, label }: { icon: React.ReactNode; value: string; label: string }) {
  return (
    <div className="flex flex-col items-center min-w-[60px] rounded-md bg-popover/60 px-2 py-1.5 border border-border">
      <div className="flex items-center gap-1 text-primary">{icon}<strong className="font-display text-base">{value}</strong></div>
      <span className="text-[10px] uppercase tracking-wider text-muted-foreground">{label}</span>
    </div>
  )
}

function EmptyState({ onStart }: { onStart: () => void }) {
  return (
    <Card>
      <CardHeader className="text-center">
        <CardTitle className="text-xl">Vamos começar? 🐾</CardTitle>
        <CardDescription>
          Escolha suas trilhas e responda um teste rápido para o Navi calibrar seu nível.
        </CardDescription>
      </CardHeader>
      <CardFooter className="justify-center">
        <Button onClick={onStart}>🐾 Iniciar onboarding</Button>
      </CardFooter>
    </Card>
  )
}

function SkeletonList() {
  return (
    <div className="grid gap-4 md:grid-cols-2">
      {[1, 2, 3].map((i) => (
        <Card key={i}><CardContent className="py-4 space-y-3"><Skeleton className="h-4 w-24" /><Skeleton className="h-3 w-full" /><Skeleton className="h-3 w-3/4" /></CardContent></Card>
      ))}
    </div>
  )
}

function TrailCard({ trail, plan, loading }: { trail: Trail; plan: JourneyPlan | null; loading: boolean }) {
  const navigate = useNavigate()

  return (
    <Card
      className="border-l-4 hover:border-l-primary transition-colors"
      style={{ borderLeftColor: trail.accentColor }}
    >
      <CardHeader>
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-start gap-3">
            <span className="text-3xl leading-none">{trail.icon}</span>
            <div>
              <CardTitle className="text-base">{trail.name}</CardTitle>
              <CardDescription className="text-xs">
                {trail.level} · Progresso geral {trail.userProgress}%
              </CardDescription>
            </div>
          </div>
          {plan && (
            <Badge variant="default" className="shrink-0">
              {plan.metaDia} hoje
            </Badge>
          )}
        </div>
      </CardHeader>

      <CardContent className="space-y-2">
        {loading ? (
          <><Skeleton className="h-4 w-2/3" /><Skeleton className="h-3 w-full" /></>
        ) : plan ? (
          plan.today.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-2">
              Nada novo hoje. Volte mais tarde 🐾
            </p>
          ) : (
            <ul className="space-y-1.5">
              {plan.today.slice(0, 2).map((item) => (
                <li
                  key={item.topicId}
                  className="flex items-center justify-between gap-2 rounded-md bg-popover/40 px-2.5 py-1.5 border border-border"
                >
                  <span className="text-sm font-medium truncate">{item.title}</span>
                  <Badge
                    variant={item.reason.toLowerCase() as "newlearning" | "duereview" | "reinforce"}
                    className="text-[10px]"
                  >
                    {REASON_LABEL[item.reason]}
                  </Badge>
                </li>
              ))}
            </ul>
          )
        ) : (
          <p className="text-xs text-muted-foreground">
            Sem plano ainda — passe pelo <Link to="/onboarding" className="text-primary underline">onboarding</Link> para calibrar.
          </p>
        )}
      </CardContent>

      <CardFooter className="justify-end">
        <Button size="sm" onClick={() => navigate({ to: "/jornada/$trailId", params: { trailId: String(trail.id) } })}>
          Ver jornada →
        </Button>
      </CardFooter>
    </Card>
  )
}

// 🪙 e <Coins /> evitam tree-shake do icone usado nos hero stats (mantém compatibilidade futura)
void Coins
