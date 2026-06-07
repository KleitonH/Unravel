import { useMemo, useState } from "react"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { useNavigate, useParams } from "@tanstack/react-router"
import { toast } from "sonner"
import { Check, ChevronLeft, ChevronRight, Coins, Heart, Star, X } from "lucide-react"
import { challengePoolApi } from "@/api/challenge-pool"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import { QuestionRenderer } from "@/components/quiz/question-renderer"
import type { PoolChallenge, SubmitPoolChallengeResponse } from "@/types/api"

type AnswerState = {
  selectedIndex: number
  isCorrect: boolean
  correctIndex: number
  explanation: string | null
  newMasteryScore: number
  xpEarned: number
  coinsEarned: number
  starsEarned: number
  lifeDelta: number
}

export function QuizPage() {
  const { contentId } = useParams({ from: "/authed/quiz/$contentId" })
  const contentIdNum = Number(contentId)
  const navigate = useNavigate()
  const qc = useQueryClient()

  const poolQuery = useQuery({
    queryKey: ["challenge-pool", contentIdNum],
    queryFn:  () => challengePoolApi.get(contentIdNum, 5),
    retry: false,
  })

  const [currentIdx, setCurrentIdx] = useState(0)
  const [answers, setAnswers] = useState<Map<number, AnswerState>>(new Map())
  const [submitting, setSubmitting] = useState(false)

  const data = poolQuery.data
  const current: PoolChallenge | null = data?.challenges[currentIdx] ?? null
  const currentAnswer = current ? answers.get(current.id) ?? null : null
  const answered = currentAnswer !== null

  const total = data?.challenges.length ?? 0
  const score = useMemo(() => Array.from(answers.values()).filter((a) => a.isCorrect).length, [answers])
  const isFinished = total > 0 && answers.size === total

  async function choose(index: number) {
    if (!current || answered || submitting) return
    setSubmitting(true)
    try {
      const r: SubmitPoolChallengeResponse = await challengePoolApi.submit(contentIdNum, {
        generatedChallengeId: current.id,
        selectedOptionIndex: index,
      })
      const state: AnswerState = {
        selectedIndex: index,
        isCorrect: r.isCorrect,
        correctIndex: r.correctOptionIndex,
        explanation: r.explanation,
        newMasteryScore: r.newMasteryScore,
        xpEarned: r.xpEarned,
        coinsEarned: r.coinsEarned,
        starsEarned: r.starsEarned,
        lifeDelta: r.lifeDelta,
      }
      setAnswers((prev) => new Map(prev).set(current.id, state))

      // PR 26: invalida o profile pro Hero do dashboard atualizar XP/streak/vidas
      // no próximo render. Não esperamos o refetch (não é UI crítica do quiz).
      qc.invalidateQueries({ queryKey: ["profile", "me"] })

      // Toast com ganhos
      if (r.isCorrect && (r.xpEarned > 0 || r.coinsEarned > 0)) {
        toast.success(`+${r.xpEarned} XP · +${r.coinsEarned} 🪙${r.starsEarned > 0 ? ` · +${r.starsEarned} ⭐` : ""}`)
      } else if (!r.isCorrect && r.lifeDelta < 0) {
        toast.error(`${r.lifeDelta} ❤️  (restam ${r.totalLives})`)
      }
    } catch {
      // Fallback offline: usa gabarito local pra não travar quiz
      if (current) {
        setAnswers((prev) => new Map(prev).set(current.id, {
          selectedIndex: index,
          isCorrect: index === current.correctIndex,
          correctIndex: current.correctIndex,
          explanation: current.explanation,
          newMasteryScore: data?.targetUserMastery ?? 0,
          xpEarned: 0, coinsEarned: 0, starsEarned: 0, lifeDelta: 0,
        }))
        toast.warning("Resposta validada offline — sinal não foi sincronizado")
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="p-6 lg:p-10 max-w-3xl space-y-5">
      {poolQuery.isLoading && <SkeletonView />}

      {poolQuery.isError && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardHeader>
            <CardTitle className="text-base text-destructive">Falha ao carregar</CardTitle>
            <CardDescription>Conteúdo não encontrado ou sem perguntas geradas.</CardDescription>
          </CardHeader>
        </Card>
      )}

      {data && (
        <>
          <header className="flex items-start justify-between gap-4">
            <div>
              <h1 className="text-2xl font-display font-extrabold tracking-tight">{data.contentTitle}</h1>
              <p className="text-xs text-muted-foreground mt-1">
                Domínio atual: <strong>{Math.round(data.targetUserMastery * 100)}%</strong>
              </p>
            </div>
            <Badge variant="outline" className="font-bold text-sm py-1 px-3">
              {currentIdx + 1} / {total}
            </Badge>
          </header>

          {current && (
            <Card>
              <CardContent className="pt-6 space-y-4">
                <div className="flex gap-1.5 flex-wrap">
                  <StrategyBadge strategy={current.strategy} />
                  <Badge variant="outline" className="text-[10px]">
                    Dificuldade: {Math.round(current.estimatedDifficulty * 100)}%
                  </Badge>
                </div>

                <QuestionRenderer
                  shape={current.shape}
                  prompt={current.prompt}
                  options={current.options}
                  selectedIndex={currentAnswer?.selectedIndex ?? null}
                  correctIndex={currentAnswer?.correctIndex ?? current.correctIndex}
                  disabled={submitting}
                  onSelect={choose}
                />

                {currentAnswer && (
                  <FeedbackBlock answer={currentAnswer} />
                )}

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
            <Card className="bg-gradient-to-br from-primary/15 via-card to-card border-primary/20 text-center">
              <CardContent className="pt-8 space-y-2">
                <p className="text-xs uppercase tracking-wider text-muted-foreground">Resultado</p>
                <p className="font-display text-5xl font-extrabold text-primary">
                  {score} / {total}
                </p>
                <p className="text-sm text-muted-foreground">acertos</p>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  )
}

function FeedbackBlock({ answer }: { answer: AnswerState }) {
  // PR 27: `animate-pop-in` no bloco inteiro — feedback aparece com
  // pequena escala in pra confirmar a interação. Sem isso o usuário
  // pode não notar que o bloco surgiu (especialmente em viewport longa).
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

function SkeletonView() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-6 w-1/2" />
      <Skeleton className="h-32 w-full" />
      {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-12 w-full" />)}
    </div>
  )
}

/** Badge da strategy com tratamento especial pra LlmGrounded.
 *  - LlmGrounded → ícone 🤖 + cor accent (verde), pra destacar
 *    visualmente perguntas geradas por IA local (PR 31/32)
 *  - Demais (Cloze/Definition/TrueFalse/Ordering/Match/Code) → label
 *    plana variant=secondary */
function StrategyBadge({ strategy }: { strategy: import("@/types/api").ChallengeStrategy }) {
  if (strategy === "LlmGrounded") {
    return (
      <Badge
        className={cn(
          "text-[10px] bg-accent/15 text-accent border border-accent/40",
          "inline-flex items-center gap-1",
        )}
      >
        <span aria-hidden>🤖</span>
        LLM grounded
      </Badge>
    )
  }
  return <Badge variant="secondary" className="text-[10px]">{strategy}</Badge>
}
