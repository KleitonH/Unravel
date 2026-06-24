import { useEffect, useRef, useState } from "react"
import { Swords, Trophy, Loader2, Check, X, Heart, Timer } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { useAuth } from "@/stores/auth"
import { cn } from "@/lib/utils"
import { Navi } from "@/components/navi/navi"
import { appearanceFromEquipped, type NaviAppearance } from "@/components/navi/appearance"
import type { ArenaMatch, ArenaRound, ArenaRoundResult, ArenaCosmetic } from "@/api/arena"
import { useArenaMatch } from "./use-arena-match"

type Phase = "connecting" | "intro" | "answering" | "waiting" | "reveal" | "finished"

const MAX_PER_ROUND = 1000 // Base 500 + bônus de velocidade 500 (LiveQuizScoring)

/**
 * Duelo da Arena em formato "VS": NAVI do usuário à esquerda, do adversário à
 * direita (espelhado, encarando o centro), com barra de VIDA acima de cada um.
 * Cada ponto que você ganha vira dano no inimigo (e vice-versa). A pergunta e
 * as alternativas ficam no meio. Sem host: a rodada apura quando os dois
 * respondem (push do ArenaHub).
 */
export function ArenaDuel({ matchId, onExit }: { matchId: number; onExit: () => void }) {
  const me = useAuth((s) => s.user)?.id
  const [match, setMatch] = useState<ArenaMatch | null>(null)
  const [round, setRound] = useState<ArenaRound | null>(null)
  const [reveal, setReveal] = useState<ArenaRoundResult | null>(null)
  const [selected, setSelected] = useState<number | null>(null)
  const [phase, setPhase] = useState<Phase>("connecting")
  const [dmg, setDmg] = useState<{ me: number; opp: number } | null>(null)

  const [secsLeft, setSecsLeft] = useState<number | null>(null)

  const introDoneRef = useRef(false)
  const nextRoundRef = useRef<ArenaRound | null>(null)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const phaseRef = useRef<Phase>(phase)
  phaseRef.current = phase
  const selectedRef = useRef<number | null>(selected)
  selectedRef.current = selected

  function startRound(r: ArenaRound) {
    setRound(r); setSelected(null); setReveal(null); setDmg(null); setPhase("answering")
  }

  const { submit, timeUp } = useArenaMatch(matchId, {
    onMatch: (m) => setMatch(m),
    onRoundStarted: (r) => {
      if (!introDoneRef.current) {
        // Pareamento concluído → tela "VS" antes da 1ª pergunta.
        introDoneRef.current = true
        setRound(r); setSelected(null); setPhase("intro")
        if (timerRef.current) clearTimeout(timerRef.current)
        timerRef.current = setTimeout(() => setPhase("answering"), 2000)
        return
      }
      if (phaseRef.current === "reveal") nextRoundRef.current = r
      else startRound(r)
    },
    onAnswerResult: (r) => { if (r.accepted && !r.roundResolved) setPhase("waiting") },
    onRoundResult: (r) => {
      setMatch((m) => {
        if (m) {
          const iAmP1 = me === m.player1Id
          const oldMy = iAmP1 ? m.score1 : m.score2
          const oldOpp = iAmP1 ? m.score2 : m.score1
          const newMy = iAmP1 ? r.score1 : r.score2
          const newOpp = iAmP1 ? r.score2 : r.score1
          setDmg({ opp: Math.max(0, newMy - oldMy), me: Math.max(0, newOpp - oldOpp) })
          return { ...m, score1: r.score1, score2: r.score2 }
        }
        return m
      })
      setReveal(r); setPhase("reveal")
      if (timerRef.current) clearTimeout(timerRef.current)
      timerRef.current = setTimeout(() => {
        if (r.finished) return
        const nr = nextRoundRef.current; nextRoundRef.current = null
        if (nr) startRound(nr)
      }, 2100)
    },
    onMatchFinished: (m) => { setMatch(m); setPhase("finished") },
  })

  useEffect(() => () => { if (timerRef.current) clearTimeout(timerRef.current) }, [])

  // Cronômetro por rodada: ao zerar, auto-pula (0 pts) se ainda não respondi e,
  // como rede de segurança, avisa o servidor pra resolver mesmo se o oponente sumiu.
  useEffect(() => {
    if (phase !== "answering" || !round) { setSecsLeft(null); return }
    const total = round.secondsPerQuestion || match?.secondsPerQuestion || 25
    const startedAt = Date.now()
    setSecsLeft(total)
    let skipTimer: ReturnType<typeof setTimeout> | null = null

    const tick = setInterval(() => {
      const left = Math.max(0, total - Math.round((Date.now() - startedAt) / 1000))
      setSecsLeft(left)
      if (left <= 0) {
        clearInterval(tick)
        if (selectedRef.current === null) { setSelected(-1); void submit(round.orderIndex, -1) }
        skipTimer = setTimeout(() => void timeUp(round.orderIndex), 2200) // folga p/ resolver no servidor
      }
    }, 250)

    return () => { clearInterval(tick); if (skipTimer) clearTimeout(skipTimer) }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [phase, round?.orderIndex])

  if (!match || phase === "connecting") {
    return (
      <div className="flex flex-col items-center justify-center gap-3 py-20 text-muted-foreground">
        <Loader2 className="h-8 w-8 animate-spin" />
        <p>Conectando ao duelo…</p>
      </div>
    )
  }

  const iAmP1 = me === match.player1Id
  const myName  = iAmP1 ? match.player1Name : (match.player2Name ?? "Você")
  const oppName = iAmP1 ? (match.player2Name ?? "Oponente") : match.player1Name
  const myScore  = iAmP1 ? match.score1 : match.score2
  const oppScore = iAmP1 ? match.score2 : match.score1
  const myApp  = appearanceFromEquipped(toEquipped(iAmP1 ? match.player1Cosmetics : match.player2Cosmetics))
  const oppApp = appearanceFromEquipped(toEquipped(iAmP1 ? match.player2Cosmetics : match.player1Cosmetics))

  const maxHp = Math.max(1, match.totalRounds * MAX_PER_ROUND)
  const myHp  = Math.round(100 * Math.max(0, 1 - oppScore / maxHp)) // inimigo me machuca
  const oppHp = Math.round(100 * Math.max(0, 1 - myScore / maxHp))  // eu machuco o inimigo

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      {/* Palco: NAVI ⟷ NAVI */}
      <div className="grid grid-cols-2 items-end gap-3 sm:grid-cols-[1fr_auto_1fr] sm:items-center">
        <Fighter name={myName} you appearance={myApp} hpPct={myHp} score={myScore} dmg={dmg?.me} />
        <div className="hidden flex-col items-center sm:flex">
          <Swords className="h-7 w-7 text-primary" />
          <span className="text-xs font-bold text-muted-foreground">
            {phase === "finished" ? "FIM" : phase === "intro" ? "VS" : `${Math.min(match.currentRoundIndex + 1, match.totalRounds)}/${match.totalRounds}`}
          </span>
        </div>
        <Fighter name={oppName} appearance={oppApp} hpPct={oppHp} score={oppScore} dmg={dmg?.opp} mirror align="right" />
      </div>

      {/* Centro: pergunta / estado */}
      {phase === "finished" ? (
        <FinishedCard match={match} me={me} onExit={onExit} />
      ) : phase === "intro" ? (
        <Card className="border-primary/30 bg-primary/5 text-center">
          <CardContent className="py-8">
            <p className="font-display text-3xl font-extrabold tracking-tight">VS</p>
            <p className="mt-1 text-sm text-muted-foreground">{myName} <span className="text-primary">×</span> {oppName} — preparando o duelo…</p>
          </CardContent>
        </Card>
      ) : phase === "waiting" ? (
        <Card><CardContent className="flex flex-col items-center gap-3 py-10 text-muted-foreground">
          <Loader2 className="h-7 w-7 animate-spin" />
          <p>Resposta enviada! Esperando o oponente…</p>
        </CardContent></Card>
      ) : round ? (
        <Card>
          <CardContent className="space-y-4 pt-6">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold uppercase text-muted-foreground">
                Rodada {Math.min(match.currentRoundIndex + 1, match.totalRounds)}/{match.totalRounds}
              </span>
              {secsLeft !== null && (
                <span className={cn(
                  "inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-bold tabular-nums",
                  secsLeft <= 5 ? "border-destructive/50 bg-destructive/10 text-destructive animate-pulse" : "border-border text-muted-foreground",
                )}>
                  <Timer className="h-3.5 w-3.5" /> {secsLeft}s
                </span>
              )}
            </div>
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
                    )}
                  >
                    <span>{opt}</span>
                    {showReveal && isCorrect && <Check className="h-4 w-4" />}
                    {showReveal && isMine && !isCorrect && <X className="h-4 w-4" />}
                  </button>
                )
              })}
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-xs text-muted-foreground">Acerte rápido — velocidade vira dano!</span>
              {phase === "answering" && (
                <Button size="sm" variant="ghost" className="text-muted-foreground"
                  onClick={() => { setSelected(-1); void submit(round.orderIndex, -1) }}>
                  Não sei — pular
                </Button>
              )}
            </div>
          </CardContent>
        </Card>
      ) : null}
    </div>
  )
}

function toEquipped(cos: ArenaCosmetic[] | undefined) {
  return (cos ?? []).map((c) => ({ slot: c.slot, assetSlug: c.assetSlug }))
}

function Fighter({
  name, appearance, hpPct, score, you, mirror, align, dmg,
}: {
  name: string; appearance: NaviAppearance; hpPct: number; score: number
  you?: boolean; mirror?: boolean; align?: "right"; dmg?: number
}) {
  const hpColor = hpPct > 50 ? "bg-success" : hpPct > 25 ? "bg-warning" : "bg-destructive"
  return (
    <div className={cn("flex flex-col items-center gap-2")}>
      <div className="w-full max-w-[170px]">
        <div className={cn("mb-0.5 flex items-center justify-between gap-2 text-[11px]", align === "right" && "flex-row-reverse")}>
          <span className="truncate font-semibold">{name}{you && <span className="text-primary"> (você)</span>}</span>
          <span className="tabular-nums text-muted-foreground">{score}</span>
        </div>
        <div className="flex items-center gap-1">
          <Heart className={cn("h-3 w-3 shrink-0", hpPct > 25 ? "text-destructive" : "text-destructive animate-pulse")} fill="currentColor" />
          <div className="h-2.5 flex-1 overflow-hidden rounded-full bg-muted">
            <div className={cn("h-full rounded-full transition-[width] duration-700", hpColor)} style={{ width: `${hpPct}%` }} />
          </div>
        </div>
      </div>
      <div className="relative">
        {dmg ? (
          <span key={`${score}-${dmg}`} className="animate-pop-in absolute -top-1 left-1/2 z-10 -translate-x-1/2 text-lg font-extrabold text-destructive drop-shadow">
            -{dmg}
          </span>
        ) : null}
        <div style={mirror ? { transform: "scaleX(-1)" } : undefined}>
          <Navi size={128} fur={appearance.fur} hat={appearance.hat} accessory={appearance.accessory} mood={appearance.mood} />
        </div>
      </div>
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
