import { useState } from "react"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import {
  Award, Check, ChevronLeft, ChevronRight, Crown, Lock, Loader2, Skull, Sparkles, Swords, X,
} from "lucide-react"
import { toast } from "sonner"
import { journeyApi } from "@/api/journey"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { BossFightAnswer, BossFightResultResponse } from "@/types/api"

type Stage = "intro" | "quiz" | "result"

/**
 * PR 50 — Boss Fight: desafio final da trilha com N=10 perguntas
 * combinatorialmente balanceadas (cobertura + difficulty + strategy mix).
 *
 * <para>Backend retorna pacote completo na chamada start. Cliente
 * navega entre perguntas localmente e submete tudo de uma vez. Sem
 * adaptação — é prova.</para>
 */
export function BossFightPage() {
  const { trailId } = useParams({ from: "/authed/boss/$trailId" })
  const trailIdNum  = Number(trailId)
  const navigate    = useNavigate()
  const qc          = useQueryClient()

  const [stage,       setStage]      = useState<Stage>("intro")
  const [answers,     setAnswers]    = useState<Map<number, number>>(new Map())
  const [currentIdx,  setCurrentIdx] = useState(0)
  const [result,      setResult]     = useState<BossFightResultResponse | null>(null)

  const sessionQuery = useQuery({
    queryKey: ["boss-fight", trailIdNum],
    queryFn:  () => journeyApi.bossFightStart(trailIdNum),
    enabled:  stage === "intro" || stage === "quiz",
    staleTime: 0,
  })

  const submitMutation = useMutation({
    mutationFn: () => {
      const body: { answers: BossFightAnswer[] } = {
        answers: Array.from(answers.entries()).map(([challengeId, idx]) => ({
          challengeId, selectedOptionIndex: idx,
        })),
      }
      return journeyApi.bossFightSubmit(trailIdNum, body)
    },
    onSuccess: (r) => {
      setResult(r)
      setStage("result")
      qc.invalidateQueries({ queryKey: ["profile", "me"] })
      qc.invalidateQueries({ queryKey: ["trail-map", trailIdNum] })
      qc.invalidateQueries({ queryKey: ["mastery", trailIdNum] })
      if (r.passed) toast.success(`Boss derrotado! ${r.score}/${r.totalQuestions}`)
      else          toast.error(`${r.score}/${r.totalQuestions} — tente novamente`)
    },
    onError: () => toast.error("Falha ao submeter sessão."),
  })

  const session    = sessionQuery.data
  const questions  = session?.questions ?? []
  const current    = questions[currentIdx]
  const allAnswered = questions.length > 0 && answers.size === questions.length

  if (sessionQuery.isLoading) return <LoadingView />

  if (sessionQuery.isError || !session) {
    return (
      <Container>
        <Card className="border-destructive/40 bg-destructive/5">
          <CardContent className="pt-6 text-sm text-destructive">
            Falha ao carregar Boss Fight.
          </CardContent>
        </Card>
      </Container>
    )
  }

  // Não desbloqueado: mostra lock screen
  if (!session.unlocked) {
    return (
      <Container>
        <Header trailName={session.trailName} />
        <Card className="border-warning/30 bg-warning/5">
          <CardContent className="pt-8 pb-8 text-center space-y-3">
            <Lock className="h-10 w-10 text-warning mx-auto" />
            <p className="font-display text-xl font-bold">Boss Fight bloqueado</p>
            <p className="text-sm text-muted-foreground max-w-md mx-auto">
              {session.lockReason ?? "Conclua os requisitos pra liberar."}
            </p>
            <Button asChild className="mt-4">
              <Link to="/jornada/$trailId" params={{ trailId }}>Voltar ao mapa</Link>
            </Button>
          </CardContent>
        </Card>
      </Container>
    )
  }

  // Intro: explica o boss e botão "Começar"
  if (stage === "intro") {
    return (
      <Container>
        <Header trailName={session.trailName} />
        <Card className="bg-gradient-to-br from-warning/10 via-card to-card border-warning/40">
          <CardHeader className="text-center space-y-2">
            <Crown className="h-12 w-12 text-warning mx-auto" />
            <CardTitle className="text-2xl">Desafio final</CardTitle>
            <CardDescription className="max-w-md mx-auto">
              {session.totalQuestions} perguntas cruzando todos os tópicos da trilha,
              balanceadas por dificuldade. Acerte <strong>{session.passThreshold}</strong> ou
              mais pra conquistar o título de Mestre.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid grid-cols-2 md:grid-cols-4 gap-3 max-w-2xl mx-auto pt-2">
            <BossStat label="Perguntas"     value={session.totalQuestions}      />
            <BossStat label="Passar com"    value={`≥ ${session.passThreshold}`} />
            <BossStat label="Tentativas"    value={session.attemptCount}        />
            <BossStat label="Melhor score"  value={session.attemptCount > 0 ? `${session.bestScore}/${session.totalQuestions}` : "—"} />
          </CardContent>
          {session.firstWonAt && (
            <CardContent className="text-center pt-0">
              <Badge variant="default" className="gap-1 bg-success">
                <Award className="h-3 w-3" /> Já conquistado
              </Badge>
            </CardContent>
          )}
          <CardContent className="text-center pt-4 pb-8 space-y-2">
            <Button size="lg" onClick={() => setStage("quiz")} disabled={questions.length === 0}>
              <Swords className="h-5 w-5 mr-2" />
              {session.attemptCount > 0 ? "Tentar novamente" : "Enfrentar o Boss"}
            </Button>
            <p className="text-[11px] text-muted-foreground">
              Você pode responder as 10 perguntas em qualquer ordem antes de submeter.
            </p>
          </CardContent>
        </Card>
      </Container>
    )
  }

  // Quiz: navega entre perguntas
  if (stage === "quiz" && current) {
    const selected = answers.get(current.id)
    return (
      <Container>
        <header className="flex items-center justify-between">
          <h1 className="text-xl font-display font-bold flex items-center gap-2">
            <Crown className="h-5 w-5 text-warning" />
            Boss Fight · {session.trailName}
          </h1>
          <Badge variant="outline" className="font-bold text-sm py-1 px-3">
            {currentIdx + 1} / {questions.length}
          </Badge>
        </header>

        <ProgressBar answered={answers.size} total={questions.length} />

        <Card>
          <CardContent className="pt-6 space-y-4">
            <div className="flex gap-2 flex-wrap">
              <Badge variant="outline" className="text-[10px]">
                Dificuldade: {Math.round(current.estimatedDifficulty * 100)}%
              </Badge>
              <Badge variant="secondary" className="text-[10px]">{current.strategy}</Badge>
            </div>

            <pre className="whitespace-pre-wrap font-sans text-base leading-relaxed bg-popover/40 p-4 rounded-md border border-border">
              {current.prompt}
            </pre>

            <ul className="space-y-2">
              {current.options.map((opt, i) => (
                <li key={i}>
                  <button
                    type="button"
                    onClick={() => {
                      const next = new Map(answers)
                      next.set(current.id, i)
                      setAnswers(next)
                    }}
                    className={cn(
                      "w-full flex items-center gap-3 rounded-md border px-3 py-3 text-sm text-left transition-all hover:border-primary",
                      selected === i ? "border-warning bg-warning/10" : "border-border bg-popover/40",
                    )}
                  >
                    <span className={cn(
                      "flex-shrink-0 h-7 w-7 rounded-md text-xs font-bold flex items-center justify-center",
                      selected === i ? "bg-warning text-warning-foreground" : "bg-background text-primary",
                    )}>
                      {["A","B","C","D","E","F"][i]}
                    </span>
                    <span>{opt}</span>
                  </button>
                </li>
              ))}
            </ul>

            <nav className="flex items-center justify-between pt-2">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setCurrentIdx((i) => Math.max(0, i - 1))}
                disabled={currentIdx === 0}
              >
                <ChevronLeft className="h-4 w-4 mr-1" />Anterior
              </Button>
              {currentIdx < questions.length - 1 ? (
                <Button size="sm" onClick={() => setCurrentIdx((i) => i + 1)}>
                  Próxima<ChevronRight className="h-4 w-4 ml-1" />
                </Button>
              ) : (
                <Button
                  size="sm"
                  onClick={() => submitMutation.mutate()}
                  disabled={!allAnswered || submitMutation.isPending}
                  className="bg-warning hover:bg-warning/90 text-warning-foreground"
                >
                  {submitMutation.isPending
                    ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Enviando…</>
                    : <><Swords className="h-4 w-4 mr-1" />Finalizar Boss Fight</>}
                </Button>
              )}
            </nav>
            {!allAnswered && currentIdx === questions.length - 1 && (
              <p className="text-xs text-warning text-center">
                Responda todas as {questions.length} perguntas antes de finalizar
                ({answers.size} respondidas, {questions.length - answers.size} restantes).
              </p>
            )}
          </CardContent>
        </Card>

        {/* Mini-mapa visual de perguntas respondidas */}
        <Card>
          <CardContent className="pt-6">
            <p className="text-xs uppercase tracking-wider text-muted-foreground mb-2">Navegação</p>
            <div className="flex gap-1.5 flex-wrap">
              {questions.map((q, i) => (
                <button
                  key={q.id}
                  type="button"
                  onClick={() => setCurrentIdx(i)}
                  className={cn(
                    "h-8 w-8 rounded-md text-xs font-bold border transition-colors",
                    i === currentIdx
                      ? "border-warning bg-warning/20 text-warning"
                      : answers.has(q.id)
                        ? "border-success bg-success/15 text-success"
                        : "border-border hover:border-primary text-muted-foreground",
                  )}
                >
                  {i + 1}
                </button>
              ))}
            </div>
          </CardContent>
        </Card>
      </Container>
    )
  }

  // Result
  if (stage === "result" && result) {
    return (
      <Container>
        <ResultView result={result} trailName={session.trailName} onBack={() => navigate({ to: "/jornada/$trailId", params: { trailId } })} />
      </Container>
    )
  }

  return <LoadingView />
}

// ── Subcomponentes ─────────────────────────────────────────────────

function Container({ children }: { children: React.ReactNode }) {
  return (
    <div className="p-6 lg:p-10 max-w-3xl mx-auto space-y-5">
      {children}
    </div>
  )
}

function Header({ trailName }: { trailName: string }) {
  const { trailId } = useParams({ from: "/authed/boss/$trailId" })
  return (
    <>
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/jornada/$trailId" params={{ trailId }}>
          <ChevronLeft className="h-4 w-4 mr-1" />
          Voltar ao mapa
        </Link>
      </Button>
      <header className="space-y-1">
        <h1 className="text-2xl font-display font-extrabold tracking-tight flex items-center gap-2">
          <Crown className="h-7 w-7 text-warning" />
          Boss Fight
        </h1>
        <p className="text-sm text-muted-foreground">{trailName}</p>
      </header>
    </>
  )
}

function ProgressBar({ answered, total }: { answered: number; total: number }) {
  const pct = Math.round((answered / total) * 100)
  return (
    <div className="space-y-1">
      <div className="h-2 rounded-full bg-muted/40 overflow-hidden">
        <div className="h-full bg-warning transition-all" style={{ width: `${pct}%` }} />
      </div>
      <div className="flex justify-between text-[11px] text-muted-foreground">
        <span>{answered} respondidas</span>
        <span>{total - answered} restantes</span>
      </div>
    </div>
  )
}

function BossStat({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-md border border-border bg-popover/40 p-3 text-center">
      <p className="font-display text-xl font-extrabold text-warning">{value}</p>
      <p className="text-[10px] uppercase tracking-wider text-muted-foreground">{label}</p>
    </div>
  )
}

function ResultView({
  result, trailName, onBack,
}: { result: BossFightResultResponse; trailName: string; onBack: () => void }) {
  const passed   = result.passed
  const Icon     = passed ? (result.isFirstWin ? Crown : Sparkles) : Skull
  const tone     = passed ? "warning" : "destructive"

  return (
    <Card className={cn(
      "bg-gradient-to-br via-card to-card text-center",
      passed ? "from-warning/20 border-warning/50" : "from-destructive/15 border-destructive/40",
    )}>
      <CardHeader className="pt-10">
        <Icon className={cn("h-16 w-16 mx-auto", tone === "warning" ? "text-warning" : "text-destructive")} />
        <CardTitle className="text-3xl mt-4">
          {result.isFirstWin ? "🏆 Vitória histórica!" : passed ? "Vitória!" : "Boss venceu desta vez"}
        </CardTitle>
        <CardDescription>
          {result.isFirstWin
            ? `Você dominou ${trailName}.`
            : passed
              ? "Sua mestria sobre a trilha está consolidada."
              : "Volte ao estudo e tente novamente quando se sentir pronto."}
        </CardDescription>
      </CardHeader>
      <CardContent className="pb-8 space-y-3">
        <p className={cn("font-display text-7xl font-extrabold", tone === "warning" ? "text-warning" : "text-destructive")}>
          {result.score}<span className="text-3xl text-muted-foreground"> / {result.totalQuestions}</span>
        </p>
        <p className="text-sm text-muted-foreground">
          Passou com ≥ {result.passThreshold}
        </p>
        <div className="flex gap-2 flex-wrap justify-center mt-4">
          <Badge variant="outline" className="text-xs gap-1 px-3 py-1">
            <Sparkles className="h-3 w-3" />
            +{result.xpEarned} XP
          </Badge>
          {result.badgeAwarded && (
            <Badge className="text-xs gap-1 px-3 py-1 bg-warning text-warning-foreground">
              <Award className="h-3 w-3" />
              {result.badgeAwarded}
            </Badge>
          )}
        </div>

        {/* Breakdown das respostas */}
        <details className="mt-6 text-left">
          <summary className="text-xs text-muted-foreground cursor-pointer hover:text-foreground">
            ▸ Ver detalhes ({result.outcomes.filter((o) => o.isCorrect).length} acertos, {result.outcomes.filter((o) => !o.isCorrect).length} erros)
          </summary>
          <ul className="mt-2 space-y-1">
            {result.outcomes.map((o, i) => (
              <li key={o.challengeId} className={cn(
                "flex items-center gap-2 text-xs rounded-md border px-2 py-1",
                o.isCorrect ? "border-success/30 bg-success/5" : "border-destructive/30 bg-destructive/5",
              )}>
                <span className="w-6 text-center font-bold">{i + 1}</span>
                {o.isCorrect
                  ? <Check className="h-3.5 w-3.5 text-success" />
                  : <X     className="h-3.5 w-3.5 text-destructive" />}
                <span className="flex-1 truncate text-muted-foreground">
                  Gabarito: opção {["A","B","C","D","E","F"][o.correctOptionIndex]}
                </span>
              </li>
            ))}
          </ul>
        </details>

        <div className="flex gap-2 justify-center pt-4">
          <Button onClick={onBack}>Voltar ao mapa</Button>
          {!passed && (
            <Button variant="outline" onClick={() => window.location.reload()}>
              Tentar novamente
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  )
}

function LoadingView() {
  return (
    <Container>
      <Skeleton className="h-10 w-48" />
      <Skeleton className="h-64" />
      {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-12" />)}
    </Container>
  )
}
