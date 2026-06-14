import { useQuery, useQueries } from "@tanstack/react-query"
import { Link, useNavigate } from "@tanstack/react-router"
import { Brain, CheckCircle2, Coins, Flame, Heart, Plus, Star, TrendingUp } from "lucide-react"
import { useAuth } from "@/stores/auth"
import { trailsApi } from "@/api/trails"
import { journeyApi } from "@/api/journey"
import { profileApi } from "@/api/profile"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { JourneyPlan, JourneyReason, Profile, StudentProfile, Trail } from "@/types/api"

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

  // PR 26: profile real (XP, streak, vidas, coins). Tem que vir antes
  // do hero pra renderizar com os dados certos no first paint.
  // Stale 60s — perfil muda só no submit de quiz; já invalidamos lá via QC.
  const profileQuery = useQuery({
    queryKey: ["profile", "me"],
    queryFn:  profileApi.me,
    staleTime: 60_000,
  })

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
      <Hero
        name={user?.name ?? "estudante"}
        profile={profileQuery.data ?? null}
        loading={profileQuery.isLoading}
      />

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
              // PR 27: stagger ~80ms entre cards. Cap em 6 pra não criar
              // "wave" muito longa quando o user tiver 10+ trilhas.
              <div key={trail.id} className="animate-pop-in" style={{ animationDelay: `${Math.min(i, 6) * 80}ms` }}>
                <TrailCard
                  trail={trail}
                  plan={journeyQueries[i].data ?? null}
                  loading={journeyQueries[i].isLoading}
                />
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  )
}

function Hero({ name, profile, loading }: { name: string; profile: Profile | null; loading: boolean }) {
  // PR 26 — stats reais via /api/profile.
  // - Student: XP/streak/vidas/coins (chips animáveis em PR 27)
  // - Moderator: sem stats individuais — exibe métricas globais discretas
  // - Loading: skeletons no chip pra não pular layout
  const isStudent = profile?.role === "Student"
  const s = isStudent ? (profile as StudentProfile) : null

  return (
    <Card className="bg-gradient-to-br from-primary/10 via-card to-card border-primary/20">
      <CardHeader className="flex-row items-center justify-between gap-4">
        <div className="min-w-0">
          <CardTitle className="text-2xl truncate">
            Olá, {name}! 👋
            {s?.activeTitle && (
              <span className="ml-2 align-middle text-xs font-medium text-primary/80 italic">
                · {s.activeTitle}
              </span>
            )}
          </CardTitle>
          <CardDescription>
            {profile?.role === "Moderator"
              ? "Painel global — métricas da plataforma."
              : "Seu plano do dia está abaixo."}
          </CardDescription>
        </div>

        <div className="hidden sm:flex gap-2">
          {loading ? (
            <>
              <StatSkeleton /><StatSkeleton /><StatSkeleton />
            </>
          ) : isStudent && s ? (
            <>
              <Stat idx={0} icon={<Star  className="h-4 w-4" />} value={fmt(s.xp)}         label="XP" />
              <Stat idx={1} icon={<Flame className="h-4 w-4" />} value={fmt(s.streakDays)} label={s.streakDays === 1 ? "dia" : "dias"} />
              <Stat idx={2} icon={<Heart className="h-4 w-4" />} value={fmt(s.lives)}      label={s.lives === 1 ? "vida" : "vidas"} />
              <Stat idx={3} icon={<Coins className="h-4 w-4" />} value={fmt(s.coins)}      label="coins" />
            </>
          ) : profile?.role === "Moderator" ? (
            <>
              <Stat idx={0} icon={<Star className="h-4 w-4" />} value={fmt(profile.metrics.totalStudents)} label="alunos" />
              <Stat idx={1} icon={<Star className="h-4 w-4" />} value={fmt(profile.metrics.totalTrails)}   label="trilhas" />
            </>
          ) : null}
        </div>
      </CardHeader>
    </Card>
  )
}

function Stat({ idx = 0, icon, value, label }: { idx?: number; icon: React.ReactNode; value: string; label: string }) {
  // PR 27 — `animate-pop-in` no chip (entra em escala) com stagger ~50ms
  // entre eles. O <strong> usa `key={value}` pra re-renderizar com
  // `animate-count-pop` quando o número muda (ex: ganho de XP após quiz).
  return (
    <div
      className="flex flex-col items-center min-w-[60px] rounded-md bg-popover/60 px-2 py-1.5 border border-border animate-pop-in"
      style={{ animationDelay: `${idx * 50}ms` }}
    >
      <div className="flex items-center gap-1 text-primary">
        {icon}
        <strong key={value} className="font-display text-base animate-count-pop inline-block">
          {value}
        </strong>
      </div>
      <span className="text-[10px] uppercase tracking-wider text-muted-foreground">{label}</span>
    </div>
  )
}

function StatSkeleton() {
  return (
    <div className="min-w-[60px] rounded-md bg-popover/60 px-2 py-1.5 border border-border space-y-1">
      <Skeleton className="h-4 w-12" />
      <Skeleton className="h-2 w-8" />
    </div>
  )
}

/** Formata números do hero: <1k cru, ≥1k em "k", ≥1M em "M".
 *  Mantém o chip estreito mesmo com XP grande (ex: 12500 → "12.5k"). */
function fmt(n: number): string {
  if (n < 1_000)     return n.toString()
  if (n < 1_000_000) return `${(n / 1_000).toFixed(n < 10_000 ? 1 : 0).replace(/\.0$/, "")}k`
  return `${(n / 1_000_000).toFixed(1).replace(/\.0$/, "")}M`
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

/**
 * PR 61 — indicador de meta do dia. Mostra "X/N hoje" com barra de
 * progresso (concluídos hoje ÷ meta), marca ✓ quando a meta é batida e
 * sinaliza quando a meta subiu por penalidade (não bateu ontem).
 */
function DailyGoal({ plan }: { plan: JourneyPlan }) {
  const meta = Math.max(plan.metaDia, 1)
  const done = plan.completedToday ?? 0
  const pct  = Math.min(100, Math.round((done / meta) * 100))
  const complete = done >= plan.metaDia
  const penalty  = plan.metaPenalty ?? 0

  return (
    <div className="shrink-0 w-28 text-right space-y-1">
      <div className="flex items-center justify-end gap-1">
        {complete && <CheckCircle2 className="h-3.5 w-3.5 text-success" />}
        <span className={cn(
          "text-xs font-bold tabular-nums",
          complete ? "text-success" : "text-foreground",
        )}>
          {done}/{plan.metaDia} hoje
        </span>
      </div>
      <div className="h-1.5 w-full rounded-full bg-border overflow-hidden">
        <div
          className={cn("h-full rounded-full transition-all", complete ? "bg-success" : "bg-primary")}
          style={{ width: `${pct}%` }}
        />
      </div>
      {penalty > 0 && !complete && (
        <div
          className="flex items-center justify-end gap-1 text-[10px] text-warning"
          title="A meta subiu porque você não bateu a meta de ontem."
        >
          <TrendingUp className="h-2.5 w-2.5" />
          +{penalty} de ontem
        </div>
      )}
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
          {plan && <DailyGoal plan={plan} />}
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

      <CardFooter className="justify-end gap-2">
        {/* PR 37 — atalho "Treinar fraquezas". Visível em toda trilha
            inscrita (backend decide se há fraqueza real e responde
            no_weaknesses se não houver). Variant ghost pra não competir
            visualmente com o CTA principal "Ver jornada". */}
        <Button
          size="sm"
          variant="ghost"
          className="text-accent hover:text-accent hover:bg-accent/10"
          onClick={() => navigate({ to: "/reinforce/$trailId", params: { trailId: String(trail.id) } })}
        >
          <Brain className="h-4 w-4 mr-1" />Reforçar
        </Button>
        <Button size="sm" onClick={() => navigate({ to: "/jornada/$trailId", params: { trailId: String(trail.id) } })}>
          Ver jornada →
        </Button>
      </CardFooter>
    </Card>
  )
}
