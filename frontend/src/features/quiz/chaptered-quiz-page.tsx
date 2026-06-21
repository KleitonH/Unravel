import { useEffect, useMemo, useReducer } from "react"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import MarkdownPreview from "@uiw/react-markdown-preview"
import {
  ArrowRight, BookOpen, Check, ChevronLeft, Coins, Heart,
  Sparkles, Star, Trophy, X,
} from "lucide-react"
import { toast } from "sonner"
import { chaptersApi } from "@/api/chapters"
import { challengePoolApi } from "@/api/challenge-pool"
import { QuestionRenderer } from "@/components/quiz/question-renderer"
import { OutOfLivesCard } from "@/components/gamification/out-of-lives"
import { useQuizLives } from "@/components/gamification/use-quiz-lives"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { ChapteredContent } from "@/types/chapters"
import type { PoolChallenge, SubmitPoolChallengeResponse } from "@/types/api"

/**
 * PR 60-b — "Estudo guiado" estilo Duolingo.
 *
 * Fluxo por capítulo:
 * 1. **study** — mostra texto do chunk H2 + botão "Praticar"
 * 2. **questions** — loop de N perguntas (4-7 adaptativo) com feedback inline
 * 3. **recap** — placar do capítulo + botão "Próximo"
 *
 * Após todos os capítulos:
 * 4. **final-review** — 3 perguntas misturadas (1 de cada chunk aleatório)
 *    pra forçar discriminação entre conceitos
 * 5. **complete** — placar final + voltar pra trilha
 *
 * Cada resposta é submetida via `challengePoolApi.submit` (mesma rota do
 * quiz tradicional) — preserva mastery, XP/coins/stars, lifeDelta, streak,
 * trail progression. Só a UX muda.
 */

type Phase = "study" | "questions" | "recap" | "final-review" | "complete"

type AnswerState = {
  selectedIndex:   number
  isCorrect:       boolean
  correctIndex:    number
  explanation:     string | null
  xpEarned:        number
  coinsEarned:     number
  starsEarned:     number
  lifeDelta:       number
}

type WizardState = {
  phase:           Phase
  chapterIndex:    number
  questionIndex:   number
  /** Map de generatedChallengeId → estado da resposta. */
  answers:         Map<number, AnswerState>
  /** Submitting global pra evitar duplo-click; UI desabilita interações. */
  submitting:      boolean
  /** Perguntas sorteadas pra revisão final (calculadas uma vez quando
   *  transicionamos pra final-review). */
  finalReviewIds:  number[]
  finalReviewIdx:  number
}

type Action =
  | { type: "START_QUESTIONS" }
  | { type: "ANSWER_LOADING" }
  | { type: "ANSWER_DONE";   challengeId: number; answer: AnswerState }
  | { type: "ANSWER_FAILED" }
  | { type: "NEXT_QUESTION" }
  | { type: "GO_TO_RECAP" }
  | { type: "NEXT_CHAPTER" }
  | { type: "START_FINAL_REVIEW"; ids: number[] }
  | { type: "NEXT_FINAL_QUESTION" }
  | { type: "FINISH" }

function reducer(state: WizardState, action: Action): WizardState {
  switch (action.type) {
    case "START_QUESTIONS":
      return { ...state, phase: "questions", questionIndex: 0 }
    case "ANSWER_LOADING":
      return { ...state, submitting: true }
    case "ANSWER_DONE": {
      const next = new Map(state.answers)
      next.set(action.challengeId, action.answer)
      return { ...state, submitting: false, answers: next }
    }
    case "ANSWER_FAILED":
      return { ...state, submitting: false }
    case "NEXT_QUESTION":
      return { ...state, questionIndex: state.questionIndex + 1 }
    case "GO_TO_RECAP":
      return { ...state, phase: "recap" }
    case "NEXT_CHAPTER":
      return {
        ...state,
        phase: "study",
        chapterIndex: state.chapterIndex + 1,
        questionIndex: 0,
      }
    case "START_FINAL_REVIEW":
      return {
        ...state,
        phase: "final-review",
        finalReviewIds: action.ids,
        finalReviewIdx: 0,
      }
    case "NEXT_FINAL_QUESTION":
      return { ...state, finalReviewIdx: state.finalReviewIdx + 1 }
    case "FINISH":
      return { ...state, phase: "complete" }
  }
}

const INITIAL: WizardState = {
  phase:          "study",
  chapterIndex:   0,
  questionIndex:  0,
  answers:        new Map(),
  submitting:     false,
  finalReviewIds: [],
  finalReviewIdx: 0,
}

// ── Página ──────────────────────────────────────────────────────────

export function ChapteredQuizPage() {
  const { contentId } = useParams({ from: "/authed/study/$contentId" })
  const contentIdNum = Number(contentId)
  const navigate     = useNavigate()
  const qc           = useQueryClient()

  const dataQuery = useQuery({
    queryKey: ["chapters", contentIdNum],
    queryFn:  () => chaptersApi.get(contentIdNum),
    retry:    false,
  })

  const [state, dispatch] = useReducer(reducer, INITIAL)
  const { gameOver, applyTotalLives } = useQuizLives()
  const data = dataQuery.data

  // ── Helpers ───────────────────────────────────────────────────────

  /** Submete resposta usando mesma rota do quiz tradicional. */
  async function submit(challenge: PoolChallenge, index: number) {
    if (state.submitting || state.answers.has(challenge.id) || gameOver) return
    dispatch({ type: "ANSWER_LOADING" })
    try {
      const r: SubmitPoolChallengeResponse = await challengePoolApi.submit(
        contentIdNum,
        { generatedChallengeId: challenge.id, selectedOptionIndex: index },
      )
      applyTotalLives(r.totalLives)
      dispatch({
        type: "ANSWER_DONE",
        challengeId: challenge.id,
        answer: {
          selectedIndex: index,
          isCorrect:     r.isCorrect,
          correctIndex:  r.correctOptionIndex,
          explanation:   r.explanation,
          xpEarned:      r.xpEarned,
          coinsEarned:   r.coinsEarned,
          starsEarned:   r.starsEarned,
          lifeDelta:     r.lifeDelta,
        },
      })
      // Invalida profile pra Hero do dashboard atualizar
      qc.invalidateQueries({ queryKey: ["profile", "me"] })
      // Toast rico
      if (r.isCorrect && (r.xpEarned > 0 || r.coinsEarned > 0)) {
        toast.success(`+${r.xpEarned} XP · +${r.coinsEarned} 🪙${r.starsEarned > 0 ? ` · +${r.starsEarned} ⭐` : ""}`)
      } else if (!r.isCorrect && r.lifeDelta < 0) {
        toast.error(`${r.lifeDelta} ❤️  (restam ${r.totalLives})`)
      }
    } catch {
      // Fallback offline — usa gabarito local
      dispatch({
        type: "ANSWER_DONE",
        challengeId: challenge.id,
        answer: {
          selectedIndex: index,
          isCorrect:     index === challenge.correctIndex,
          correctIndex:  challenge.correctIndex,
          explanation:   challenge.explanation,
          xpEarned: 0, coinsEarned: 0, starsEarned: 0, lifeDelta: 0,
        },
      })
      toast.warning("Resposta validada offline — sinal não sincronizado.")
    }
  }

  /** Quando termina recap, decide próxima fase. */
  function continueAfterRecap() {
    if (!data) return
    const isLastChapter = state.chapterIndex >= data.chapters.length - 1
    if (!isLastChapter) {
      dispatch({ type: "NEXT_CHAPTER" })
    } else {
      const ids = pickFinalReviewIds(data, state.answers)
      if (ids.length === 0) dispatch({ type: "FINISH" })
      else                  dispatch({ type: "START_FINAL_REVIEW", ids })
    }
  }

  /** Avança questão dentro do capítulo OU vai pro recap se terminou. */
  function nextInChapter() {
    if (!data) return
    const chapter = data.chapters[state.chapterIndex]
    if (state.questionIndex < chapter.challenges.length - 1) {
      dispatch({ type: "NEXT_QUESTION" })
    } else {
      dispatch({ type: "GO_TO_RECAP" })
    }
  }

  function nextFinal() {
    if (state.finalReviewIdx < state.finalReviewIds.length - 1) {
      dispatch({ type: "NEXT_FINAL_QUESTION" })
    } else {
      dispatch({ type: "FINISH" })
    }
  }

  // ── Render ────────────────────────────────────────────────────────

  if (dataQuery.isLoading) return <PageShell><SkeletonView /></PageShell>
  if (dataQuery.isError || !data) {
    return (
      <PageShell>
        <Card className="border-destructive/40 bg-destructive/5">
          <CardHeader>
            <CardTitle className="text-base text-destructive">Falha ao carregar capítulos</CardTitle>
            <CardDescription>
              Conteúdo não encontrado ou capítulos ainda não foram gerados.
              {" "}<Link to="/trails" className="text-primary underline">Voltar pra trilhas</Link>
            </CardDescription>
          </CardHeader>
        </Card>
      </PageShell>
    )
  }

  const chapter      = data.chapters[state.chapterIndex]
  const currentQ     = chapter?.challenges[state.questionIndex] ?? null
  const currentAns   = currentQ ? state.answers.get(currentQ.id) ?? null : null

  return (
    <PageShell>
      <Header data={data} state={state} />

      {gameOver && (
        <OutOfLivesCard onLeave={() => navigate({ to: "/trails" })} leaveLabel="Voltar pra trilha" />
      )}

      {/* Phase: STUDY */}
      {!gameOver && state.phase === "study" && chapter && (
        <Card>
          <CardHeader>
            <div className="flex items-center gap-2 text-xs text-muted-foreground uppercase tracking-wider">
              <BookOpen className="h-3.5 w-3.5" />
              Capítulo {state.chapterIndex + 1} de {data.chapters.length}
            </div>
            <CardTitle className="text-xl font-display font-extrabold">{chapter.title}</CardTitle>
            <CardDescription>
              {chapter.challenges.length} pergunta(s) para esse capítulo.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="rounded-md border border-border bg-popover/40 p-4" data-color-mode="dark">
              <MarkdownPreview
                source={chapter.bodyMd}
                style={{ background: "transparent" }}
              />
            </div>
            {chapter.challenges.length === 0 ? (
              <div className="text-center py-2 text-sm text-muted-foreground">
                Esse capítulo ainda não tem perguntas — moderador foi avisado.
                <Button variant="link" onClick={() => dispatch({ type: "NEXT_CHAPTER" })}>
                  Pular capítulo →
                </Button>
              </div>
            ) : (
              <div className="flex justify-end">
                <Button size="lg" onClick={() => dispatch({ type: "START_QUESTIONS" })}>
                  <Sparkles className="h-4 w-4 mr-2" />
                  Praticar isso
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Phase: QUESTIONS */}
      {!gameOver && state.phase === "questions" && currentQ && chapter && (
        <Card>
          <CardContent className="pt-6 space-y-4">
            <div className="flex items-center justify-between text-xs text-muted-foreground">
              <span className="uppercase tracking-wider font-semibold">
                📖 {chapter.title}
              </span>
              <Badge variant="outline" className="text-[10px]">
                Pergunta {state.questionIndex + 1} / {chapter.challenges.length}
              </Badge>
            </div>

            <QuestionRenderer
              shape={currentQ.shape}
              prompt={currentQ.prompt}
              options={currentQ.options}
              selectedIndex={currentAns?.selectedIndex ?? null}
              correctIndex={currentAns?.correctIndex ?? currentQ.correctIndex}
              disabled={state.submitting}
              onSelect={(i) => submit(currentQ, i)}
            />

            {currentAns && <FeedbackBlock answer={currentAns} />}

            {currentAns && (
              <div className="flex justify-end pt-2">
                <Button onClick={nextInChapter}>
                  {state.questionIndex < chapter.challenges.length - 1
                    ? <>Próxima<ArrowRight className="h-4 w-4 ml-1" /></>
                    : <>Concluir capítulo<ArrowRight className="h-4 w-4 ml-1" /></>}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Phase: RECAP */}
      {!gameOver && state.phase === "recap" && chapter && (
        <ChapterRecap
          chapter={chapter}
          answers={state.answers}
          isLast={state.chapterIndex >= data.chapters.length - 1}
          onContinue={continueAfterRecap}
        />
      )}

      {/* Phase: FINAL REVIEW */}
      {!gameOver && state.phase === "final-review" && (() => {
        const id = state.finalReviewIds[state.finalReviewIdx]
        const q = allQuestions(data).find((q) => q.id === id)
        if (!q) {
          return <Card><CardContent className="pt-6">Pergunta não encontrada.</CardContent></Card>
        }
        const ans = state.answers.get(q.id) ?? null
        return (
          <Card className="border-warning/30 bg-warning/5">
            <CardContent className="pt-6 space-y-4">
              <div className="flex items-center justify-between text-xs">
                <span className="uppercase tracking-wider font-semibold text-warning flex items-center gap-1.5">
                  <Trophy className="h-3.5 w-3.5" />
                  Revisão final
                </span>
                <Badge variant="outline" className="text-[10px] border-warning/40 text-warning">
                  {state.finalReviewIdx + 1} / {state.finalReviewIds.length}
                </Badge>
              </div>
              <p className="text-xs text-muted-foreground">
                Perguntas misturadas dos capítulos pra consolidar o conhecimento.
              </p>
              <QuestionRenderer
                shape={q.shape}
                prompt={q.prompt}
                options={q.options}
                selectedIndex={ans?.selectedIndex ?? null}
                correctIndex={ans?.correctIndex ?? q.correctIndex}
                disabled={state.submitting}
                onSelect={(i) => submit(q, i)}
              />
              {ans && <FeedbackBlock answer={ans} />}
              {ans && (
                <div className="flex justify-end pt-2">
                  <Button onClick={nextFinal}>
                    {state.finalReviewIdx < state.finalReviewIds.length - 1
                      ? <>Próxima<ArrowRight className="h-4 w-4 ml-1" /></>
                      : <>Finalizar<Trophy className="h-4 w-4 ml-1" /></>}
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        )
      })()}

      {/* Phase: COMPLETE */}
      {state.phase === "complete" && (
        <CompleteCard data={data} answers={state.answers}
          onBack={() => navigate({ to: "/trails" })} />
      )}
    </PageShell>
  )
}

// ── Subcomponents ───────────────────────────────────────────────────

function PageShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="p-6 lg:p-10 max-w-3xl space-y-5">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/trails">
          <ChevronLeft className="h-4 w-4 mr-1" />
          Voltar
        </Link>
      </Button>
      {children}
    </div>
  )
}

function Header({ data, state }: { data: ChapteredContent; state: WizardState }) {
  const total       = data.chapters.length
  const currentIdx  = state.phase === "complete" || state.phase === "final-review"
    ? total
    : state.chapterIndex
  return (
    <header className="space-y-2">
      <div className="flex items-baseline justify-between gap-3">
        <h1 className="text-2xl font-display font-extrabold tracking-tight">
          {data.contentTitle}
        </h1>
        <Badge variant="outline" className="text-xs font-bold shrink-0">
          {Math.min(currentIdx + 1, total)} / {total}
        </Badge>
      </div>
      {/* Mini-progresso por capítulo */}
      <div className="flex gap-1.5">
        {data.chapters.map((_, i) => (
          <div
            key={i}
            className={cn(
              "h-1.5 flex-1 rounded-full transition-all duration-300",
              i < state.chapterIndex && "bg-success",
              i === state.chapterIndex && state.phase !== "complete" && "bg-primary",
              i > state.chapterIndex && "bg-muted/40",
              state.phase === "complete" && "bg-success",
            )}
          />
        ))}
      </div>
    </header>
  )
}

function FeedbackBlock({ answer }: { answer: AnswerState }) {
  return (
    <div className={cn(
      "rounded-md border p-3 text-sm space-y-2 animate-pop-in",
      answer.isCorrect
        ? "border-success bg-success/10"
        : "border-destructive bg-destructive/10",
    )}>
      <div className="flex items-center gap-2 font-semibold">
        {answer.isCorrect
          ? <><Check className="h-4 w-4 text-success" />Resposta correta!</>
          : <><X className="h-4 w-4 text-destructive" />Não foi dessa vez.</>}
      </div>
      {answer.explanation && (
        <p className="text-muted-foreground text-xs">{answer.explanation}</p>
      )}
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

function ChapterRecap({
  chapter, answers, isLast, onContinue,
}: {
  chapter:    { challenges: PoolChallenge[]; title: string }
  answers:    Map<number, AnswerState>
  isLast:     boolean
  onContinue: () => void
}) {
  const stats = useMemo(() => {
    let correct = 0, total = 0
    for (const c of chapter.challenges) {
      const a = answers.get(c.id)
      if (a) { total++; if (a.isCorrect) correct++ }
    }
    return { correct, total }
  }, [chapter, answers])
  const pct = stats.total === 0 ? 0 : Math.round(stats.correct / stats.total * 100)

  return (
    <Card className="border-success/30 bg-success/5">
      <CardContent className="pt-6 space-y-3 text-center">
        <Check className="h-10 w-10 text-success mx-auto" />
        <p className="text-xs uppercase tracking-wider text-muted-foreground">
          Capítulo concluído
        </p>
        <h2 className="font-display font-extrabold text-2xl">{chapter.title}</h2>
        <p className="font-display text-4xl font-extrabold text-success">
          {stats.correct} / {stats.total}
        </p>
        <p className="text-xs text-muted-foreground">{pct}% de aproveitamento</p>
        <Button size="lg" onClick={onContinue} className="mt-3">
          {isLast
            ? <><Trophy className="h-4 w-4 mr-2" />Ir pra revisão final</>
            : <>Próximo capítulo<ArrowRight className="h-4 w-4 ml-2" /></>}
        </Button>
      </CardContent>
    </Card>
  )
}

function CompleteCard({
  data, answers, onBack,
}: {
  data:    ChapteredContent
  answers: Map<number, AnswerState>
  onBack:  () => void
}) {
  const stats = useMemo(() => {
    let correct = 0, total = 0
    for (const c of data.chapters) {
      for (const q of c.challenges) {
        const a = answers.get(q.id)
        if (a) { total++; if (a.isCorrect) correct++ }
      }
    }
    return { correct, total }
  }, [data, answers])
  const pct = stats.total === 0 ? 0 : Math.round(stats.correct / stats.total * 100)

  return (
    <Card className="bg-gradient-to-br from-primary/15 via-card to-card border-primary/20 text-center">
      <CardContent className="pt-8 pb-8 space-y-2">
        <Trophy className="h-12 w-12 text-warning mx-auto fill-warning/30" />
        <p className="text-xs uppercase tracking-wider text-muted-foreground">Você concluiu</p>
        <h2 className="font-display text-2xl font-extrabold">{data.contentTitle}</h2>
        <p className="font-display text-5xl font-extrabold text-primary">
          {stats.correct} / {stats.total}
        </p>
        <p className="text-sm text-muted-foreground">acertos · {pct}% de aproveitamento</p>
        <div className="pt-3">
          <Button onClick={onBack}>
            <ChevronLeft className="h-4 w-4 mr-1" />
            Voltar pra trilha
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}

function SkeletonView() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-6 w-1/2" />
      <Skeleton className="h-2 w-full" />
      <Skeleton className="h-40 w-full" />
      <Skeleton className="h-10 w-32 ml-auto" />
    </div>
  )
}

// ── helpers fora do render ──────────────────────────────────────────

/** Junta todas perguntas de todos capítulos numa lista única. */
function allQuestions(data: ChapteredContent): PoolChallenge[] {
  return data.chapters.flatMap((c) => c.challenges)
}

/**
 * Escolhe 3 IDs pra revisão final, idealmente 1 por capítulo distinto.
 * Se o content tem <3 capítulos, complementa com mais perguntas dos
 * mesmos capítulos. Usa só perguntas que o aluno respondeu (preserva
 * cache de answer pra UI imediata).
 *
 * Determinístico baseado no conjunto de IDs respondidos — re-render
 * não embaralha de novo.
 */
function pickFinalReviewIds(
  data: ChapteredContent,
  answers: Map<number, AnswerState>,
): number[] {
  const answeredIds = new Set(answers.keys())
  const byChunk = data.chapters
    .map((c) => c.challenges.filter((q) => answeredIds.has(q.id)).map((q) => q.id))
    .filter((arr) => arr.length > 0)

  // 1 por chunk até atingir 3 — round-robin
  const picked: number[] = []
  let round = 0
  while (picked.length < 3 && byChunk.some((arr) => arr.length > round)) {
    for (const arr of byChunk) {
      if (picked.length >= 3) break
      if (arr.length > round) picked.push(arr[round])
    }
    round++
  }
  return picked.slice(0, 3)
}

// Suppress unused-effect warning for the missing useEffect import
// (kept in case future logic needs reactivity)
void useEffect
