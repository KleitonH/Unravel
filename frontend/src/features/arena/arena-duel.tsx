import { useEffect, useRef, useState } from "react"
import { Swords, Trophy, Loader2, Check, X } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { useAuth } from "@/stores/auth"
import { cn } from "@/lib/utils"
import type { ArenaMatch, ArenaRound, ArenaRoundResult } from "@/api/arena"
import { useArenaMatch } from "./use-arena-match"

type Phase = "connecting" | "answering" | "waiting" | "reveal" | "finished"

/**
 * Duelo da Arena em tempo real. Sem host: cada jogador responde e a rodada
 * apura quando os dois respondem (push do ArenaHub). Mostra placar dos dois,
 * a pergunta da rodada, o reveal do gabarito e o vencedor no fim.
 */
export function ArenaDuel({ matchId, onExit }: { matchId: number; onExit: () => void }) {
  const me = useAuth((s) => s.user)?.id
  const [match, setMatch] = useState<ArenaMatch | null>(null)
  const [round, setRound] = useState<ArenaRound | null>(null)
  const [reveal, setReveal] = useState<ArenaRoundResult | null>(null)
  const [selected, setSelected] = useState<number | null>(null)
  const [phase, setPhase] = useState<Phase>("connecting")

  const nextRoundRef = useRef<ArenaRound | null>(null)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const phaseRef = useRef<Phase>(phase)
  phaseRef.current = phase

  function startRound(r: ArenaRound) {
    setRound(r); setSelected(null); setReveal(null); setPhase("answering")
  }

  const { state, submit } = useArenaMatch(matchId, {
    onMatch: (m) => setMatch(m),
    onRoundStarted: (r) => {
      if (phaseRef.current === "reveal") nextRoundRef.current = r // espera o reveal
      else startRound(r)
    },
    onAnswerResult: (r) => {
      if (r.accepted && !r.roundResolved) setPhase("waiting")
    },
    onRoundResult: (r) => {
      setReveal(r)
      setMatch((m) => (m ? { ...m, score1: r.score1, score2: r.score2 } : m))
      setPhase("reveal")
      if (timerRef.current) clearTimeout(timerRef.current)
      timerRef.current = setTimeout(() => {
        if (r.finished) return // MatchFinished cuida do fim
        const nr = nextRoundRef.current
        nextRoundRef.current = null
        if (nr) startRound(nr)
      }, 1900)
    },
    onMatchFinished: (m) => { setMatch(m); setPhase("finished") },
  })

  useEffect(() => () => { if (timerRef.current) clearTimeout(timerRef.current) }, [])

  if (!match || phase === "connecting") {
    return (
      <div className="flex flex-col items-center justify-center gap-3 py-20 text-muted-foreground">
        <Loader2 className="h-8 w-8 animate-spin" />
        <p>Conectando ao duelo…</p>
      </div>
    )
  }

  const iAmP1 = me === match.player1Id
  const myName = iAmP1 ? match.player1Name : (match.player2Name ?? "Você")
  const oppName = iAmP1 ? (match.player2Name ?? "Oponente") : match.player1Name
  const myScore = iAmP1 ? match.score1 : match.score2
  const oppScore = iAmP1 ? match.score2 : match.score1

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      {/* Placar */}
      <div className="flex items-center justify-between gap-3 rounded-xl border border-border bg-card p-4">
        <PlayerScore name={myName} score={myScore} you />
        <div className="flex flex-col items-center text-muted-foreground">
          <Swords className="h-6 w-6 text-primary" />
          <span className="text-xs font-bold">
            {phase === "finished" ? "FIM" : `Rodada ${Math.min(match.currentRoundIndex + 1, match.totalRounds)}/${match.totalRounds}`}
          </span>
        </div>
        <PlayerScore name={oppName} score={oppScore} align="right" />
      </div>

      {phase === "finished" ? (
        <FinishedCard match={match} me={me} onExit={onExit} />
      ) : phase === "waiting" ? (
        <Card><CardContent className="flex flex-col items-center gap-3 py-12 text-muted-foreground">
          <Loader2 className="h-7 w-7 animate-spin" />
          <p>Resposta enviada! Esperando o oponente…</p>
        </CardContent></Card>
      ) : round ? (
        <Card>
          <CardContent className="space-y-4 pt-6">
            <p className="text-lg font-semibold">{round.prompt}</p>
            <div className="grid gap-2">
              {round.options.map((opt, i) => {
                const isCorrect = reveal?.correctIndex === i
                const isMine = selected === i
                const showReveal = phase === "reveal"
                return (
                  <button
                    key={i}
                    disabled={phase !== "answering"}
                    onClick={() => { setSelected(i); void submit(round.orderIndex, i) }}
                    className={cn(
                      "flex items-center justify-between rounded-lg border px-4 py-3 text-left text-sm transition-colors",
                      phase === "answering" && "hover:border-primary/60 hover:bg-primary/5",
                      isMine && !showReveal && "border-primary bg-primary/10",
                      showReveal && isCorrect && "border-success bg-success/10 text-success",
                      showReveal && isMine && !isCorrect && "border-destructive bg-destructive/10 text-destructive",
                      !isMine && !isCorrect && "border-border",
                    )}
                  >
                    <span>{opt}</span>
                    {showReveal && isCorrect && <Check className="h-4 w-4" />}
                    {showReveal && isMine && !isCorrect && <X className="h-4 w-4" />}
                  </button>
                )
              })}
            </div>
            <p className="text-center text-xs text-muted-foreground">
              {state.toString() === "Connected" ? "Responda o mais rápido que puder — velocidade vale pontos!" : "Reconectando…"}
            </p>
          </CardContent>
        </Card>
      ) : null}
    </div>
  )
}

function PlayerScore({ name, score, you, align }: { name: string; score: number; you?: boolean; align?: "right" }) {
  return (
    <div className={cn("flex min-w-0 flex-1 flex-col", align === "right" && "items-end text-right")}>
      <span className="truncate text-sm font-semibold">
        {name} {you && <span className="text-primary">(você)</span>}
      </span>
      <span className="font-display text-3xl font-extrabold tabular-nums">{score}</span>
    </div>
  )
}

function FinishedCard({ match, me, onExit }: { match: ArenaMatch; me?: string; onExit: () => void }) {
  const draw = !match.winnerId
  const iWon = match.winnerId === me
  return (
    <Card className={cn("text-center", iWon ? "border-success/40 bg-success/5" : draw ? "" : "border-destructive/30 bg-destructive/5")}>
      <CardContent className="space-y-3 py-10">
        <Trophy className={cn("mx-auto h-14 w-14", iWon ? "text-success" : "text-muted-foreground")} />
        <h2 className="font-display text-2xl font-extrabold">
          {draw ? "Empate! 🤝" : iWon ? "Você venceu! 🏆" : "Você perdeu 😿"}
        </h2>
        <p className="text-sm text-muted-foreground">Placar final: {match.score1} × {match.score2}</p>
        <div className="pt-2"><Button onClick={onExit}>Voltar à Arena</Button></div>
      </CardContent>
    </Card>
  )
}
