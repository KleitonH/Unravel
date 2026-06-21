import { useEffect, useState } from "react"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import { useMutation, useQueryClient } from "@tanstack/react-query"
import { toast } from "sonner"
import {
  Activity, Check, ChevronLeft, Loader2, Sparkles, Target, X,
} from "lucide-react"
import { challengePoolApi } from "@/api/challenge-pool"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import { QuestionRenderer } from "@/components/quiz/question-renderer"
import { OutOfLivesCard } from "@/components/gamification/out-of-lives"
import { useQuizLives } from "@/components/gamification/use-quiz-lives"
import type {
  AdaptiveHistoryItem,
  AdaptiveNextResponse,
  AdaptiveStopReason,
  PoolChallenge,
  SubmitPoolChallengeResponse,
} from "@/types/api"

type AnswerState = {
  selectedIndex: number
  isCorrect:     boolean
  correctIndex:  number
  explanation:   string | null
  xpEarned:      number
  coinsEarned:   number
  lifeDelta:     number
}

const STOP_REASON_LABEL: Record<AdaptiveStopReason, string> = {
  MaxReached:    "Limite de perguntas atingido",
  Converged:     "Sua estimativa de domínio estabilizou",
  PoolExhausted: "Não há mais perguntas disponíveis",
}

/**
 * PR 42 — Quiz Adaptativo (CAT-lite). Renderiza uma pergunta de cada vez;
 * cada submit dispara fetch da próxima via algoritmo de seleção que
 * leva em conta o histórico curto da sessão.
 *
 * <para>Indicador visual da <c>abilityEstimate</c> sobe/desce conforme
 * o aluno responde — expõe o algoritmo de forma transparente.</para>
 */
export function AdaptiveQuizPage() {
  const { contentId } = useParams({ from: "/authed/quiz/$contentId/adaptive" })
  const contentIdNum  = Number(contentId)
  const navigate      = useNavigate()
  const qc            = useQueryClient()

  const [history, setHistory] = useState<AdaptiveHistoryItem[]>([])
  const [current, setCurrent] = useState<PoolChallenge | null>(null)
  const [answer, setAnswer]   = useState<AnswerState | null>(null)
  const [ability, setAbility] = useState<number>(0.3)
  const [done, setDone]       = useState<{ reason: AdaptiveStopReason } | null>(null)
  const [loading, setLoading] = useState(true)
  const { gameOver, applyTotalLives } = useQuizLives()

  // Track simples de acertos pra resultado final
  const correctCount = history.filter((h) => h.wasCorrect).length

  /** Pede a próxima pergunta ao backend dado o histórico atual. */
  async function fetchNext(historyToSend: AdaptiveHistoryItem[]) {
    setLoading(true)
    try {
      const r: AdaptiveNextResponse = await challengePoolApi.adaptiveNext(contentIdNum, historyToSend)
      setAbility(r.abilityEstimate)
      if (r.done) {
        setDone({ reason: r.stopReason ?? "PoolExhausted" })
        setCurrent(null)
      } else {
        setCurrent(r.question)
        setAnswer(null)
      }
    } catch {
      toast.error("Falha ao buscar próxima pergunta.")
    } finally {
      setLoading(false)
    }
  }

  // Bootstrap: pede a primeira pergunta com histórico vazio.
  useEffect(() => {
    void fetchNext([])
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [contentIdNum])

  const submitMutation = useMutation({
    mutationFn: async ({ selectedIndex }: { selectedIndex: number }) => {
      if (!current) throw new Error("no current question")
      const r: SubmitPoolChallengeResponse = await challengePoolApi.submit(contentIdNum, {
        generatedChallengeId: current.id,
        selectedOptionIndex:  selectedIndex,
      })
      return { selectedIndex, r }
    },
    onSuccess: ({ selectedIndex, r }) => {
      if (!current) return
      applyTotalLives(r.totalLives)
      setAnswer({
        selectedIndex,
        isCorrect:    r.isCorrect,
        correctIndex: r.correctOptionIndex,
        explanation:  r.explanation,
        xpEarned:     r.xpEarned,
        coinsEarned:  r.coinsEarned,
        lifeDelta:    r.lifeDelta,
      })
      qc.invalidateQueries({ queryKey: ["profile", "me"] })
      qc.invalidateQueries({ queryKey: ["trail-map"] })
      if (r.isCorrect && (r.xpEarned > 0)) {
        toast.success(`+${r.xpEarned} XP · +${r.coinsEarned} 🪙`)
      } else if (!r.isCorrect && r.lifeDelta < 0) {
        toast.error(`${r.lifeDelta} ❤️`)
      }
    },
    onError: () => toast.error("Falha ao submeter resposta."),
  })

  function chooseOption(index: number) {
    if (!current || answer || submitMutation.isPending || gameOver) return
    submitMutation.mutate({ selectedIndex: index })
  }

  function nextQuestion() {
    if (!current || !answer) return
    const newHistory: AdaptiveHistoryItem[] = [
      ...history,
      { challengeId: current.id, wasCorrect: answer.isCorrect },
    ]
    setHistory(newHistory)
    void fetchNext(newHistory)
  }

  return (
    <div className="p-6 lg:p-10 max-w-3xl mx-auto space-y-5">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/contents/$contentId" params={{ contentId }}>
          <ChevronLeft className="h-4 w-4 mr-1" />
          Voltar
        </Link>
      </Button>

      <header className="space-y-1">
        <h1 className="text-2xl font-display font-extrabold tracking-tight flex items-center gap-2">
          <Activity className="h-6 w-6 text-accent" />
          Modo adaptativo
        </h1>
        <p className="text-sm text-muted-foreground">
          A próxima pergunta se ajusta à sua estimativa de domínio
          (CAT — Computerized Adaptive Testing). Acertou: nível sobe; errou: baixa.
        </p>
      </header>

      {/* Ability estimate — indicador visual */}
      <Card>
        <CardContent className="pt-6 space-y-2">
          <div className="flex items-center justify-between gap-3 text-xs">
            <span className="flex items-center gap-1 text-muted-foreground uppercase tracking-wider">
              <Target className="h-3 w-3" />
              Estimativa de domínio
            </span>
            <strong className={cn(
              "font-display text-lg",
              ability >= 0.6 ? "text-success" : ability >= 0.3 ? "text-warning" : "text-destructive",
            )}>
              {Math.round(ability * 100)}%
            </strong>
          </div>
          <div className="h-2 rounded-full bg-muted/40 overflow-hidden">
            <div
              className={cn(
                "h-full transition-all duration-500",
                ability >= 0.6 ? "bg-success" : ability >= 0.3 ? "bg-warning" : "bg-destructive",
              )}
              style={{ width: `${Math.round(ability * 100)}%` }}
            />
          </div>
          <div className="flex items-center justify-between gap-3 text-[11px] text-muted-foreground">
            <span>{history.length} respondidas · {correctCount} corretas</span>
            {current && <Badge variant="outline" className="text-[10px]">
              Dificuldade: {Math.round(current.estimatedDifficulty * 100)}%
            </Badge>}
          </div>
        </CardContent>
      </Card>

      {loading && (
        <div className="space-y-3">
          <Skeleton className="h-32 w-full" />
          {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-10 w-full" />)}
        </div>
      )}

      {!loading && gameOver && (
        <OutOfLivesCard onLeave={() => navigate({ to: "/dashboard" })} leaveLabel="Voltar ao início" />
      )}

      {!loading && !gameOver && current && (
        <Card>
          <CardContent className="pt-6 space-y-4">
            <QuestionRenderer
              shape={current.shape}
              prompt={current.prompt}
              options={current.options}
              selectedIndex={answer?.selectedIndex ?? null}
              correctIndex={answer?.correctIndex ?? current.correctIndex}
              disabled={submitMutation.isPending}
              onSelect={chooseOption}
            />

            {answer && (
              <FeedbackBlock answer={answer} />
            )}

            {answer && (
              <div className="flex justify-end">
                <Button onClick={nextQuestion} disabled={loading}>
                  {loading
                    ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Carregando…</>
                    : "Próxima →"}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {!loading && done && (
        <Card className="bg-gradient-to-br from-accent/15 via-card to-card border-accent/30 text-center">
          <CardHeader className="pt-8">
            <Sparkles className="h-10 w-10 text-accent mx-auto" />
            <CardTitle className="text-2xl mt-2">Sessão concluída</CardTitle>
            <CardDescription>{STOP_REASON_LABEL[done.reason]}</CardDescription>
          </CardHeader>
          <CardContent className="pt-2 pb-8 space-y-3">
            <p className="font-display text-5xl font-extrabold text-accent">
              {correctCount} / {history.length}
            </p>
            <p className="text-sm text-muted-foreground">
              Sua estimativa final de domínio: <strong>{Math.round(ability * 100)}%</strong>
            </p>
            <div className="flex gap-2 justify-center pt-2">
              <Button variant="outline" asChild>
                <Link to="/contents/$contentId" params={{ contentId }}>Voltar ao conteúdo</Link>
              </Button>
              <Button onClick={() => navigate({ to: "/dashboard" })}>Início</Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

function FeedbackBlock({ answer }: { answer: AnswerState }) {
  return (
    <div className={cn(
      "rounded-md border p-3 text-sm space-y-2 animate-pop-in",
      answer.isCorrect ? "border-success bg-success/10" : "border-destructive bg-destructive/10",
    )}>
      <div className="flex items-center gap-2 font-semibold">
        {answer.isCorrect
          ? <><Check className="h-4 w-4 text-success" />Resposta correta!</>
          : <><X className="h-4 w-4 text-destructive" />Não foi dessa vez.</>}
      </div>
      {answer.explanation && (
        <p className="text-muted-foreground text-xs">{answer.explanation}</p>
      )}
      {(answer.xpEarned > 0 || answer.lifeDelta < 0) && (
        <div className="flex gap-1.5 flex-wrap pt-1">
          {answer.xpEarned > 0    && <Badge variant="outline" className="text-[10px] border-primary text-primary">+{answer.xpEarned} XP</Badge>}
          {answer.coinsEarned > 0 && <Badge variant="outline" className="text-[10px] border-primary text-primary">+{answer.coinsEarned} 🪙</Badge>}
          {answer.lifeDelta < 0   && <Badge variant="outline" className="text-[10px] border-destructive text-destructive">{answer.lifeDelta} ❤️</Badge>}
        </div>
      )}
    </div>
  )
}
