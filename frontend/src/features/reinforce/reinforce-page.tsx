import { useMemo, useState } from "react"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { toast } from "sonner"
import {
  Brain, Check, ChevronLeft, ChevronRight, Clock, Coins, Heart, Sparkles, Star, Target, X,
} from "lucide-react"
import { challengePoolApi } from "@/api/challenge-pool"
import { journeyApi } from "@/api/journey"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { PoolChallenge, SubmitPoolChallengeResponse } from "@/types/api"

type AnswerState = {
  selectedIndex:   number
  isCorrect:       boolean
  correctIndex:    number
  explanation:     string | null
  newMasteryScore: number
  xpEarned:        number
  coinsEarned:     number
  starsEarned:     number
  lifeDelta:       number
}

/**
 * PR 37 — página "Treinar fraquezas". Chama o endpoint reinforce com
 * o trailId, mostra os tópicos fracos identificados e renderiza um
 * quiz focado neles. Submit reusa o pool challenge endpoint (cada
 * pergunta carrega seu próprio contentId).
 *
 * <para>Stateless do lado do front: cada visita gera nova seleção
 * baseada na mastery atual + perguntas já vistas. Refresh da página
 * pode dar outras perguntas (perfeitamente OK — é o esperado).</para>
 */
export function ReinforcePage() {
  const { trailId } = useParams({ from: "/authed/reinforce/$trailId" })
  const trailIdNum  = Number(trailId)
  const navigate    = useNavigate()
  const qc          = useQueryClient()

  const quizQuery = useQuery({
    queryKey: ["reinforce", trailIdNum],
    queryFn:  () => journeyApi.reinforce(trailIdNum, 5),
    retry:    false,
    // Não cacheia por mais que alguns segundos — cada visita deve
    // reavaliar fraquezas (mastery mudou após cada quiz anterior).
    staleTime: 0,
  })

  const [currentIdx, setCurrentIdx] = useState(0)
  const [answers, setAnswers]       = useState<Map<number, AnswerState>>(new Map())

  const data       = quizQuery.data
  const challenges = data?.challenges ?? []
  const total      = challenges.length
  const current    = challenges[currentIdx] ?? null
  const answered   = current ? answers.has(current.id) : false
  const score      = useMemo(() => Array.from(answers.values()).filter((a) => a.isCorrect).length, [answers])
  const isFinished = total > 0 && answers.size === total

  const submitMutation = useMutation({
    mutationFn: async ({ challenge, selectedIndex }: { challenge: PoolChallenge; selectedIndex: number }) => {
      const r: SubmitPoolChallengeResponse = await challengePoolApi.submit(challenge.contentId, {
        generatedChallengeId: challenge.id,
        selectedOptionIndex:  selectedIndex,
      })
      return { challenge, selectedIndex, r }
    },
    onSuccess: ({ challenge, selectedIndex, r }) => {
      const state: AnswerState = {
        selectedIndex,
        isCorrect:       r.isCorrect,
        correctIndex:    r.correctOptionIndex,
        explanation:     r.explanation,
        newMasteryScore: r.newMasteryScore,
        xpEarned:        r.xpEarned,
        coinsEarned:     r.coinsEarned,
        starsEarned:     r.starsEarned,
        lifeDelta:       r.lifeDelta,
      }
      setAnswers((prev) => new Map(prev).set(challenge.id, state))
      qc.invalidateQueries({ queryKey: ["profile", "me"] })

      if (r.isCorrect && (r.xpEarned > 0 || r.coinsEarned > 0)) {
        toast.success(`+${r.xpEarned} XP · +${r.coinsEarned} 🪙${r.starsEarned > 0 ? ` · +${r.starsEarned} ⭐` : ""}`)
      } else if (!r.isCorrect && r.lifeDelta < 0) {
        toast.error(`${r.lifeDelta} ❤️  (restam ${r.totalLives})`)
      }
    },
    onError: (_, { challenge, selectedIndex }) => {
      // Fallback offline: usa gabarito local pra não travar a sessão
      setAnswers((prev) => new Map(prev).set(challenge.id, {
        selectedIndex,
        isCorrect:       selectedIndex === challenge.correctIndex,
        correctIndex:    challenge.correctIndex,
        explanation:     challenge.explanation,
        newMasteryScore: 0,
        xpEarned:        0,
        coinsEarned:     0,
        starsEarned:     0,
        lifeDelta:       0,
      }))
      toast.warning("Resposta validada offline — sinal não foi sincronizado")
    },
  })

  function optionClass(c: PoolChallenge, i: number): string {
    const ans = answers.get(c.id)
    if (!ans) return ""
    if (i === ans.correctIndex)        return "border-success bg-success/15"
    if (ans.selectedIndex === i)       return "border-destructive bg-destructive/15"
    return "opacity-40"
  }

  return (
    <div className="p-6 lg:p-10 max-w-3xl space-y-5">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/dashboard">
          <ChevronLeft className="h-4 w-4 mr-1" />
          Voltar
        </Link>
      </Button>

      <header className="space-y-1">
        <h1 className="text-2xl font-display font-extrabold tracking-tight flex items-center gap-2">
          <Target className="h-6 w-6 text-accent" />
          Treinar fraquezas
        </h1>
        <p className="text-sm text-muted-foreground">
          Perguntas focadas nos tópicos onde você ainda não consolidou o domínio.
          Cada acerto sobe sua mastery; cada erro indica onde voltar e estudar.
        </p>
      </header>

      {quizQuery.isLoading && (
        <div className="space-y-3">
          <Skeleton className="h-20 w-full" />
          <Skeleton className="h-48 w-full" />
        </div>
      )}

      {quizQuery.isError && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardContent className="pt-6 text-sm text-destructive">
            Falha ao carregar reforço. Tente novamente em alguns segundos.
          </CardContent>
        </Card>
      )}

      {data && data.weakTopics.length > 0 && (
        <Card className="border-accent/30 bg-accent/5">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Brain className="h-4 w-4 text-accent" />
              Fraquezas detectadas
            </CardTitle>
            <CardDescription className="text-xs">
              Tópicos com domínio abaixo de 60% (efetivo, com decaimento por tempo)
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2 pt-2">
            {data.weakTopics.map((t) => (
              <Badge
                key={t.topicId}
                variant="outline"
                className={cn(
                  "text-[11px] gap-1",
                  t.effectiveMastery < 0.3 && "border-destructive/40 text-destructive",
                  t.effectiveMastery >= 0.3 && t.effectiveMastery < 0.5 && "border-warning/40 text-warning",
                )}
              >
                {t.topicSlug} · <strong>{Math.round(t.effectiveMastery * 100)}%</strong>
              </Badge>
            ))}
          </CardContent>
          {data.moreComing && (
            <CardContent className="pt-0">
              <div className="flex items-center gap-2 text-xs text-muted-foreground rounded-md border border-warning/30 bg-warning/5 p-2">
                <Clock className="h-3.5 w-3.5 text-warning shrink-0" />
                <span>
                  Pool curto pra algumas fraquezas — geramos <strong>{data.jobsEnqueued}</strong> perguntas novas em background.
                  Volte em ~1 minuto pra ver mais opções.
                </span>
              </div>
            </CardContent>
          )}
        </Card>
      )}

      {data?.reason === "no_weaknesses" && (
        <Card className="border-success/30 bg-success/5">
          <CardContent className="pt-8 pb-8 text-center space-y-2">
            <Sparkles className="h-10 w-10 text-success mx-auto" />
            <p className="font-display text-xl font-bold">Sem fraquezas detectadas!</p>
            <p className="text-sm text-muted-foreground">
              Todos os tópicos que você praticou estão acima de 60% de domínio.
              Continue com a jornada do dia pra explorar conteúdo novo.
            </p>
            <Button asChild className="mt-4">
              <Link to="/dashboard">Ir pra jornada</Link>
            </Button>
          </CardContent>
        </Card>
      )}

      {data?.reason === "pool_exhausted" && (
        <Card className="border-warning/30 bg-warning/5">
          <CardContent className="pt-6 pb-6 text-center space-y-2">
            <Clock className="h-8 w-8 text-warning mx-auto" />
            <p className="font-bold">Pool esgotado</p>
            <p className="text-sm text-muted-foreground">
              Você já viu todas as perguntas disponíveis pras suas fraquezas atuais.
              Geramos {data.jobsEnqueued} novas em background — volte em alguns minutos.
            </p>
          </CardContent>
        </Card>
      )}

      {data && current && (
        <Card>
          <CardContent className="pt-6 space-y-4">
            <div className="flex items-center justify-between">
              <Badge variant="outline" className="text-[10px]">
                Dificuldade: {Math.round(current.estimatedDifficulty * 100)}%
              </Badge>
              <Badge variant="outline" className="font-bold text-sm py-1 px-3">
                {currentIdx + 1} / {total}
              </Badge>
            </div>

            <pre className="whitespace-pre-wrap font-sans text-base leading-relaxed bg-popover/40 p-4 rounded-md border border-border">
              {current.prompt}
            </pre>

            <ul className="space-y-2">
              {current.options.map((opt, i) => (
                <li key={i}>
                  <button
                    type="button"
                    onClick={() => submitMutation.mutate({ challenge: current, selectedIndex: i })}
                    disabled={answered || submitMutation.isPending}
                    className={cn(
                      "w-full flex items-center gap-3 rounded-md border px-3 py-3 text-sm text-left transition-all",
                      !answered && !submitMutation.isPending && "hover:border-primary",
                      (answered || submitMutation.isPending) && "cursor-default",
                      optionClass(current, i),
                      !answered && "border-border bg-popover/40",
                    )}
                  >
                    <span className="flex-shrink-0 h-7 w-7 rounded-md text-xs font-bold flex items-center justify-center bg-background text-primary">
                      {["A","B","C","D","E","F"][i]}
                    </span>
                    <span>{opt}</span>
                  </button>
                </li>
              ))}
            </ul>

            {answered && current && <FeedbackBlock answer={answers.get(current.id)!} />}

            <nav className="flex items-center justify-between pt-2">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setCurrentIdx((i) => Math.max(0, i - 1))}
                disabled={currentIdx === 0}
              >
                <ChevronLeft className="h-4 w-4 mr-1" />Anterior
              </Button>
              {currentIdx < total - 1 ? (
                <Button
                  size="sm"
                  onClick={() => setCurrentIdx((i) => Math.min(total - 1, i + 1))}
                  disabled={!answered}
                >
                  Próxima<ChevronRight className="h-4 w-4 ml-1" />
                </Button>
              ) : isFinished ? (
                <Button size="sm" onClick={() => navigate({ to: "/dashboard" })}>
                  Finalizar
                </Button>
              ) : null}
            </nav>
          </CardContent>
        </Card>
      )}

      {isFinished && (
        <Card className="bg-gradient-to-br from-accent/15 via-card to-card border-accent/20 text-center">
          <CardContent className="pt-8 space-y-2">
            <p className="text-xs uppercase tracking-wider text-muted-foreground">Reforço concluído</p>
            <p className="font-display text-5xl font-extrabold text-accent">
              {score} / {total}
            </p>
            <p className="text-sm text-muted-foreground">
              {score === total
                ? "Domínio reforçado nas fraquezas. Suas masteries acabam de subir!"
                : `Reveja os conteúdos das perguntas que você errou; sua mastery vai consolidar com o próximo reforço.`}
            </p>
            <div className="flex gap-2 justify-center pt-2">
              <Button variant="outline" onClick={() => quizQuery.refetch()}>
                Novo reforço
              </Button>
              <Button asChild>
                <Link to="/dashboard">Voltar ao dashboard</Link>
              </Button>
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
        {answer.isCorrect ? (
          <><Check className="h-4 w-4 text-success" />Resposta correta!</>
        ) : (
          <><X className="h-4 w-4 text-destructive" />Não foi dessa vez.</>
        )}
      </div>
      {answer.explanation && <p className="text-muted-foreground text-xs">{answer.explanation}</p>}
      <p className="text-xs">
        📈 Domínio atualizado: <strong>{Math.round(answer.newMasteryScore * 100)}%</strong>
      </p>
      {(answer.xpEarned > 0 || answer.coinsEarned > 0 || answer.starsEarned > 0 || answer.lifeDelta < 0) && (
        <div className="flex flex-wrap gap-1.5 pt-1">
          {answer.xpEarned > 0    && <Reward icon={<Star  className="h-3 w-3" />} label={`+${answer.xpEarned} XP`} />}
          {answer.coinsEarned > 0 && <Reward icon={<Coins className="h-3 w-3" />} label={`+${answer.coinsEarned} moedas`} />}
          {answer.starsEarned > 0 && <Reward icon={<Star  className="h-3 w-3" />} label={`+${answer.starsEarned} ⭐`} />}
          {answer.lifeDelta < 0   && <Reward icon={<Heart className="h-3 w-3" />} label={`${answer.lifeDelta} vida`} bad />}
        </div>
      )}
    </div>
  )
}

function Reward({ icon, label, bad = false }: { icon: React.ReactNode; label: string; bad?: boolean }) {
  return (
    <span className={cn(
      "inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-[11px] font-bold border",
      bad
        ? "border-destructive bg-destructive/10 text-destructive"
        : "border-primary bg-primary/10 text-primary",
    )}>
      {icon}{label}
    </span>
  )
}
