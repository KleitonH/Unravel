import { useEffect, useRef, useState } from "react"
import { HubConnectionState } from "@microsoft/signalr"
import { Check, Crown, Loader2, Play, SkipForward, Trophy, Users, X } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { cn } from "@/lib/utils"
import { useLiveQuizHost } from "./use-live-quiz-host"
import type { LiveLeaderboardRow, LiveQuestion, LiveQuestionResult } from "@/api/live-quiz"

const OPTION_TONE = [
  "border-rose-400/50 bg-rose-400/10 text-rose-300",
  "border-sky-400/50 bg-sky-400/10 text-sky-300",
  "border-amber-400/50 bg-amber-400/10 text-amber-300",
  "border-emerald-400/50 bg-emerald-400/10 text-emerald-300",
  "border-violet-400/50 bg-violet-400/10 text-violet-300",
]

/**
 * Tela do PROFESSOR conduzindo o Quiz ao Vivo. Lobby (código + quem entrou)
 * → rodada (pergunta + quantos responderam + revelar) → pódio. Tudo via
 * SignalR; o host só controla (não responde).
 */
export function LiveQuizHost({ sessionId, joinCode, onExit }: { sessionId: number; joinCode: string; onExit: () => void }) {
  const [participants, setParticipants] = useState<string[]>([])
  const [count, setCount]               = useState(0)
  const [question, setQuestion]         = useState<LiveQuestion | null>(null)
  const [answered, setAnswered]         = useState(0)
  const [result, setResult]             = useState<LiveQuestionResult | null>(null)
  const [board, setBoard]               = useState<LiveLeaderboardRow[]>([])
  const [finished, setFinished]         = useState(false)
  const [secondsLeft, setSecondsLeft]   = useState(0)
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const { state, start, reveal, next, end } = useLiveQuizHost(sessionId, {
    onParticipant:     (e) => { setParticipants((p) => [...p, e.name]); setCount(e.count) },
    onLeaderboard:     (rows) => setBoard(rows),
    onQuestionStarted: (q) => { setQuestion(q); setResult(null); setAnswered(0); startTimer(q.secondsPerQuestion) },
    onAnswerTally:     (e) => setAnswered(e.count),
    onQuestionEnded:   (e) => { setResult(e.result); setBoard(e.leaderboard); stopTimer() },
    onSessionEnded:    (rows) => { setBoard(rows); setFinished(true); setQuestion(null); stopTimer() },
  })

  function startTimer(seconds: number) {
    stopTimer()
    setSecondsLeft(seconds)
    timerRef.current = setInterval(() => setSecondsLeft((s) => (s <= 1 ? 0 : s - 1)), 1000)
  }
  function stopTimer() { if (timerRef.current) { clearInterval(timerRef.current); timerRef.current = null } }
  useEffect(() => () => stopTimer(), [])

  const connecting = state !== HubConnectionState.Connected

  // ── PÓDIO ───────────────────────────────────────────────────────────
  if (finished) {
    return (
      <div className="space-y-5 max-w-2xl">
        <div className="text-center space-y-1">
          <Trophy className="h-12 w-12 text-warning mx-auto fill-warning/30" />
          <h2 className="font-display text-2xl font-extrabold">Pódio final</h2>
        </div>
        <Leaderboard rows={board} podium />
        <div className="flex justify-center">
          <Button onClick={onExit}>Encerrar</Button>
        </div>
      </div>
    )
  }

  // ── LOBBY ───────────────────────────────────────────────────────────
  if (!question) {
    return (
      <div className="space-y-5 max-w-2xl">
        <Card className="border-primary/30 bg-primary/5 text-center">
          <CardContent className="py-8 space-y-3">
            <p className="text-xs uppercase tracking-wider text-muted-foreground">Código da sala</p>
            <p className="font-display text-5xl font-extrabold tracking-[0.3em] text-primary">{joinCode}</p>
            <p className="text-sm text-muted-foreground">
              Os participantes entram com este código. {connecting && "Conectando…"}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="py-4">
            <div className="flex items-center justify-between mb-2">
              <p className="text-sm font-display font-bold flex items-center gap-1.5">
                <Users className="h-4 w-4" />Participantes ({count})
              </p>
            </div>
            {participants.length === 0 ? (
              <p className="text-sm text-muted-foreground text-center py-4">Aguardando alunos entrarem…</p>
            ) : (
              <div className="flex flex-wrap gap-2">
                {participants.map((n, i) => (
                  <Badge key={i} variant="outline" className="animate-pop-in">{n}</Badge>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onExit}>Cancelar</Button>
          <Button onClick={() => start()} disabled={connecting || count === 0}>
            <Play className="h-4 w-4 mr-1" />Iniciar quiz
          </Button>
        </div>
      </div>
    )
  }

  // ── RODADA (pergunta) ───────────────────────────────────────────────
  const isLast = question.orderIndex >= question.total - 1
  return (
    <div className="space-y-4 max-w-2xl">
      <div className="flex items-center justify-between">
        <Badge variant="outline" className="font-bold">{question.orderIndex + 1} / {question.total}</Badge>
        <div className="flex items-center gap-3">
          <Badge variant="outline" className="gap-1"><Users className="h-3.5 w-3.5" />{answered}/{count} responderam</Badge>
          {!result && <span className={cn("font-display text-xl tabular-nums", secondsLeft <= 5 ? "text-destructive" : "text-foreground")}>{secondsLeft}s</span>}
        </div>
      </div>

      <Card>
        <CardContent className="pt-6 space-y-4">
          <p className="text-lg font-medium">{question.prompt}</p>
          <div className="grid gap-2 sm:grid-cols-2">
            {question.options.map((opt, i) => {
              const correct = result && i === result.correctIndex
              const wrong   = result && i !== result.correctIndex
              return (
                <div key={i} className={cn(
                  "rounded-md border p-3 text-sm font-medium flex items-center gap-2 transition-colors",
                  result ? (correct ? "border-success bg-success/15 text-success" : "border-border opacity-50")
                         : OPTION_TONE[i % OPTION_TONE.length],
                )}>
                  {correct && <Check className="h-4 w-4" />}
                  {wrong && <X className="h-4 w-4 opacity-40" />}
                  {opt}
                </div>
              )
            })}
          </div>
          {result?.explanation && (
            <p className="text-xs text-muted-foreground border-l-2 border-primary/40 pl-2">{result.explanation}</p>
          )}
        </CardContent>
      </Card>

      {/* Ranking parcial após revelar */}
      {result && board.length > 0 && <Leaderboard rows={board.slice(0, 5)} />}

      <div className="flex justify-end gap-2">
        {!result ? (
          <Button onClick={() => reveal(question.orderIndex)} disabled={connecting}>
            Revelar resposta
          </Button>
        ) : isLast ? (
          <Button onClick={() => end()} disabled={connecting}>
            <Trophy className="h-4 w-4 mr-1" />Ver pódio
          </Button>
        ) : (
          <Button onClick={() => next()} disabled={connecting}>
            <SkipForward className="h-4 w-4 mr-1" />Próxima
          </Button>
        )}
      </div>
    </div>
  )
}

function Leaderboard({ rows, podium = false }: { rows: LiveLeaderboardRow[]; podium?: boolean }) {
  if (rows.length === 0) return null
  return (
    <Card>
      <CardContent className="py-3 space-y-1.5">
        {rows.map((r) => (
          <div key={r.userId} className={cn(
            "flex items-center gap-3 rounded-md px-3 py-2",
            podium && r.rank === 1 ? "bg-warning/10 border border-warning/30" : "bg-popover/40",
          )}>
            <span className={cn("font-display font-extrabold w-6 text-center",
              r.rank === 1 ? "text-warning" : "text-muted-foreground")}>
              {r.rank === 1 ? <Crown className="h-4 w-4 inline" /> : r.rank}
            </span>
            <span className="flex-1 min-w-0 truncate text-sm font-medium">{r.displayName}</span>
            <span className="font-display font-bold tabular-nums">{r.score}</span>
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

// Suppress unused import when connecting spinner not used inline
void Loader2
