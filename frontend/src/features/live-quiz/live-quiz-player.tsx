import { useEffect, useRef, useState } from "react"
import { Link } from "@tanstack/react-router"
import { HubConnectionState } from "@microsoft/signalr"
import { Check, Crown, Loader2, Radio, Trophy, X } from "lucide-react"
import { useAuth } from "@/stores/auth"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"
import { useLiveQuizPlayer } from "./use-live-quiz-player"
import type { LiveLeaderboardRow, LiveQuestion, LiveQuestionResult, LiveSession } from "@/api/live-quiz"

const OPTION_TONE = [
  "border-rose-400/60 bg-rose-400/15 hover:bg-rose-400/25 text-rose-200",
  "border-sky-400/60 bg-sky-400/15 hover:bg-sky-400/25 text-sky-200",
  "border-amber-400/60 bg-amber-400/15 hover:bg-amber-400/25 text-amber-200",
  "border-emerald-400/60 bg-emerald-400/15 hover:bg-emerald-400/25 text-emerald-200",
  "border-violet-400/60 bg-violet-400/15 hover:bg-violet-400/25 text-violet-200",
]

type Phase = "enter" | "lobby" | "question" | "answered" | "reveal" | "podium"

/**
 * Tela do ALUNO no Quiz ao Vivo. Entra por código (turma valida whitelist;
 * livre é aberto), responde ao vivo com timer e vê sua posição + pódio.
 * Acessível por /ao-vivo (opcionalmente com ?code=).
 */
export function LiveQuizPlayer({ initialCode = "" }: { initialCode?: string }) {
  const myId = useAuth((s) => s.user?.id)

  const [phase, setPhase]       = useState<Phase>("enter")
  const [code, setCode]         = useState(initialCode)
  const [error, setError]       = useState<string | null>(null)
  const [session, setSession]   = useState<LiveSession | null>(null)
  const [question, setQuestion] = useState<LiveQuestion | null>(null)
  const [selected, setSelected] = useState<number | null>(null)
  const [myResult, setMyResult] = useState<{ isCorrect: boolean; points: number; totalScore: number } | null>(null)
  const [reveal, setReveal]     = useState<LiveQuestionResult | null>(null)
  const [board, setBoard]       = useState<LiveLeaderboardRow[]>([])
  const [secondsLeft, setSecondsLeft] = useState(0)
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  function startTimer(seconds: number) {
    stopTimer(); setSecondsLeft(seconds)
    timerRef.current = setInterval(() => setSecondsLeft((s) => (s <= 1 ? 0 : s - 1)), 1000)
  }
  function stopTimer() { if (timerRef.current) { clearInterval(timerRef.current); timerRef.current = null } }
  useEffect(() => () => stopTimer(), [])

  const { state, join, submit } = useLiveQuizPlayer({
    onJoined:          (e) => { setSession(e.session); setError(null); setPhase("lobby") },
    onJoinError:       (r) => setError(joinErrorMessage(r)),
    onQuestionStarted: (q) => { setQuestion(q); setSelected(null); setMyResult(null); setReveal(null); setPhase("question"); startTimer(q.secondsPerQuestion) },
    onAnswerResult:    (r) => { if (r.accepted) setMyResult({ isCorrect: r.isCorrect, points: r.points, totalScore: r.totalScore }) },
    onQuestionEnded:   (e) => { setReveal(e.result); setBoard(e.leaderboard); setPhase("reveal"); stopTimer() },
    onSessionEnded:    (rows) => { setBoard(rows); setPhase("podium"); stopTimer() },
  })

  const connecting = state !== HubConnectionState.Connected
  const myRow = board.find((r) => r.userId === myId)

  function choose(i: number) {
    if (!question || selected !== null || !session) return
    setSelected(i); setPhase("answered")
    void submit(session.id, question.orderIndex, i)
  }

  return (
    <div className="p-6 lg:p-10 max-w-xl mx-auto space-y-5">
      <header className="text-center space-y-1">
        <h1 className="text-2xl font-display font-extrabold tracking-tight flex items-center justify-center gap-2">
          <Radio className="h-6 w-6 text-primary" />Quiz ao Vivo
        </h1>
      </header>

      {/* ENTRAR */}
      {phase === "enter" && (
        <Card>
          <CardContent className="py-8 space-y-4 text-center">
            <p className="text-sm text-muted-foreground">Digite o código da sala que o professor compartilhou.</p>
            <Input
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              onKeyDown={(e) => { if (e.key === "Enter" && code.trim().length >= 4) join(code) }}
              placeholder="ABC123"
              maxLength={10}
              className="text-center text-2xl font-display tracking-[0.3em] h-14"
            />
            {error && <p className="text-sm text-destructive">{error}</p>}
            <Button className="w-full" disabled={connecting || code.trim().length < 4} onClick={() => join(code)}>
              {connecting ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Conectando…</> : "Entrar"}
            </Button>
          </CardContent>
        </Card>
      )}

      {/* LOBBY */}
      {phase === "lobby" && (
        <Card className="border-primary/30 bg-primary/5">
          <CardContent className="py-10 text-center space-y-2">
            <Loader2 className="h-8 w-8 text-primary mx-auto animate-spin" />
            <p className="font-display font-bold">Você entrou! 🎉</p>
            <p className="text-sm text-muted-foreground">Aguardando o professor iniciar o quiz…</p>
          </CardContent>
        </Card>
      )}

      {/* PERGUNTA */}
      {(phase === "question" || phase === "answered") && question && (
        <div className="space-y-4">
          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground">{question.orderIndex + 1} / {question.total}</span>
            {phase === "question" && (
              <span className={cn("font-display text-xl tabular-nums", secondsLeft <= 5 ? "text-destructive" : "")}>{secondsLeft}s</span>
            )}
          </div>
          <Card><CardContent className="pt-6"><p className="text-lg font-medium text-center">{question.prompt}</p></CardContent></Card>

          {phase === "answered" ? (
            <Card className="border-primary/30 bg-primary/5">
              <CardContent className="py-8 text-center space-y-1">
                <Check className="h-8 w-8 text-primary mx-auto" />
                <p className="font-display font-bold">Resposta enviada!</p>
                <p className="text-sm text-muted-foreground">Aguarde o professor revelar a resposta.</p>
              </CardContent>
            </Card>
          ) : (
            <div className="grid gap-2 sm:grid-cols-2">
              {question.options.map((opt, i) => (
                <button key={i} type="button" onClick={() => choose(i)}
                  className={cn("rounded-lg border p-4 text-left text-sm font-medium transition-colors", OPTION_TONE[i % OPTION_TONE.length])}>
                  {opt}
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {/* REVELAÇÃO */}
      {phase === "reveal" && question && reveal && (
        <div className="space-y-4">
          <Card className={cn("text-center", myResult?.isCorrect ? "border-success/50 bg-success/10" : "border-destructive/40 bg-destructive/5")}>
            <CardContent className="py-6 space-y-1">
              {myResult?.isCorrect
                ? <><Check className="h-10 w-10 text-success mx-auto" /><p className="font-display font-extrabold text-xl text-success">Acertou! +{myResult.points}</p></>
                : <><X className="h-10 w-10 text-destructive mx-auto" /><p className="font-display font-extrabold text-xl text-destructive">{selected === null ? "Sem resposta" : "Não foi dessa vez"}</p></>}
              {myRow && <p className="text-sm text-muted-foreground">Você está em {myRow.rank}º · {myRow.score} pts</p>}
            </CardContent>
          </Card>
          <div className="grid gap-2 sm:grid-cols-2">
            {question.options.map((opt, i) => (
              <div key={i} className={cn("rounded-lg border p-3 text-sm flex items-center gap-2",
                i === reveal.correctIndex ? "border-success bg-success/15 text-success" : "border-border opacity-50")}>
                {i === reveal.correctIndex && <Check className="h-4 w-4" />}{opt}
              </div>
            ))}
          </div>
          <Leaderboard rows={board.slice(0, 5)} myId={myId} />
          <p className="text-center text-sm text-muted-foreground">Aguardando a próxima pergunta…</p>
        </div>
      )}

      {/* PÓDIO */}
      {phase === "podium" && (
        <div className="space-y-4 text-center">
          <Trophy className="h-12 w-12 text-warning mx-auto fill-warning/30" />
          <h2 className="font-display text-2xl font-extrabold">Fim!</h2>
          {myRow && <p className="text-muted-foreground">Você terminou em <strong className="text-foreground">{myRow.rank}º</strong> com {myRow.score} pts</p>}
          <Leaderboard rows={board} myId={myId} podium />
          <Button asChild><Link to="/dashboard">Voltar ao início</Link></Button>
        </div>
      )}
    </div>
  )
}

function Leaderboard({ rows, myId, podium = false }: { rows: LiveLeaderboardRow[]; myId?: string; podium?: boolean }) {
  if (rows.length === 0) return null
  return (
    <Card><CardContent className="py-3 space-y-1.5">
      {rows.map((r) => (
        <div key={r.userId} className={cn("flex items-center gap-3 rounded-md px-3 py-2",
          r.userId === myId ? "bg-primary/10 border border-primary/30" : podium && r.rank === 1 ? "bg-warning/10 border border-warning/30" : "bg-popover/40")}>
          <span className={cn("font-display font-extrabold w-6 text-center", r.rank === 1 ? "text-warning" : "text-muted-foreground")}>
            {r.rank === 1 ? <Crown className="h-4 w-4 inline" /> : r.rank}
          </span>
          <span className="flex-1 min-w-0 truncate text-sm font-medium">{r.displayName}</span>
          <span className="font-display font-bold tabular-nums">{r.score}</span>
        </div>
      ))}
    </CardContent></Card>
  )
}

function joinErrorMessage(reason: string): string {
  switch (reason) {
    case "NotFound":   return "Sala não encontrada. Confira o código."
    case "NotAllowed": return "Você não está na lista de participantes desta turma."
    case "Finished":   return "Esta sessão já foi encerrada."
    default:           return "Não foi possível entrar. Tente de novo."
  }
}
