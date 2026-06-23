import { useEffect, useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Swords, Trophy, Loader2, X, Crown } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useAuth } from "@/stores/auth"
import { profileApi } from "@/api/profile"
import { arenaApi } from "@/api/arena"
import { cn } from "@/lib/utils"
import type { ArenaMatch } from "@/api/arena"
import { ArenaDuel } from "./arena-duel"
import { useArenaLobby } from "./use-arena-lobby"

/**
 * Hub da Arena (PvP). Escolhe a trilha-tema, entra na fila (matchmaking) ou
 * aceita um desafio; quando há partida ativa, abre o duelo em tempo real.
 * Mostra também o ranking da Arena.
 */
export function ArenaPage() {
  const me = useAuth((s) => s.user)?.id
  const qc = useQueryClient()
  const [activeMatchId, setActiveMatchId] = useState<number | null>(null)
  const [trailId, setTrailId] = useState<number | null>(null)
  const [queueing, setQueueing] = useState(false)

  const profile = useQuery({ queryKey: ["profile", "me"], queryFn: profileApi.me, staleTime: 60_000 })
  const ranking = useQuery({ queryKey: ["arena", "ranking"], queryFn: () => arenaApi.ranking(20) })
  const myMatches = useQuery({
    queryKey: ["arena", "my-matches"],
    queryFn: arenaApi.myMatches,
    refetchInterval: activeMatchId ? false : 3000, // descobre pareamento/aceite
  })

  const trails =
    profile.data && profile.data.role === "Student" ? profile.data.trailProgress : []

  useEffect(() => {
    if (trailId === null && trails.length > 0) setTrailId(trails[0].trailId)
  }, [trails, trailId])

  // Push do pareamento via SignalR (instantâneo) — sem depender do polling.
  useArenaLobby(!activeMatchId, (matchId) => { setQueueing(false); setActiveMatchId(matchId) })

  // Fallback: enquanto na fila, entra quando surgir uma partida ativa no poll.
  useEffect(() => {
    if (!queueing || !myMatches.data) return
    const active = myMatches.data.find((m) => m.status === "Active")
    if (active) { setQueueing(false); setActiveMatchId(active.id) }
  }, [queueing, myMatches.data])

  const enqueueMut = useMutation({
    mutationFn: () => arenaApi.enqueue(trailId!),
    onSuccess: (r) => {
      if (r.matched && r.matchId) { setActiveMatchId(r.matchId) }
      else { setQueueing(true); toast.info("Na fila! Procurando um oponente…") }
    },
    onError: () => toast.error("Não foi possível entrar na fila."),
  })

  const leaveMut = useMutation({
    mutationFn: () => arenaApi.leaveQueue(),
    onSuccess: () => { setQueueing(false); toast.info("Você saiu da fila.") },
  })

  const acceptMut = useMutation({
    mutationFn: (id: number) => arenaApi.accept(id),
    onSuccess: (r) => setActiveMatchId(r.matchId),
    onError: () => toast.error("Não foi possível aceitar o desafio."),
  })
  const declineMut = useMutation({
    mutationFn: (id: number) => arenaApi.decline(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["arena", "my-matches"] }),
  })

  if (activeMatchId) {
    return (
      <div className="p-4 sm:p-6 lg:p-10">
        <ArenaDuel
          matchId={activeMatchId}
          onExit={() => {
            setActiveMatchId(null)
            qc.invalidateQueries({ queryKey: ["arena"] })
            qc.invalidateQueries({ queryKey: ["profile", "me"] })
          }}
        />
      </div>
    )
  }

  const pending = (myMatches.data ?? []).filter((m) => m.status === "Pending")
  const active = (myMatches.data ?? []).filter((m) => m.status === "Active")

  return (
    <div className="mx-auto max-w-3xl space-y-6 p-4 sm:p-6 lg:p-10">
      <header className="flex items-center gap-3">
        <div className="grid h-11 w-11 place-items-center rounded-xl bg-primary/10 text-primary">
          <Swords className="h-6 w-6" />
        </div>
        <div>
          <h1 className="font-display text-2xl font-extrabold">Arena</h1>
          <p className="text-sm text-muted-foreground">Duelos 1×1: acerte rápido e vença!</p>
        </div>
      </header>

      {/* Partidas ativas pendentes de retomada */}
      {active.length > 0 && (
        <Card className="border-primary/30 bg-primary/5">
          <CardHeader className="pb-2"><CardTitle className="text-base">Duelo em andamento</CardTitle></CardHeader>
          <CardContent className="space-y-2">
            {active.map((m) => (
              <div key={m.id} className="flex items-center justify-between gap-2">
                <span className="text-sm">vs <b>{opponentName(m, me)}</b></span>
                <Button size="sm" onClick={() => setActiveMatchId(m.id)}>Continuar</Button>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {/* Desafios recebidos */}
      {pending.filter((m) => m.player2Id === me).length > 0 && (
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-base">Desafios recebidos</CardTitle></CardHeader>
          <CardContent className="space-y-2">
            {pending.filter((m) => m.player2Id === me).map((m) => (
              <div key={m.id} className="flex items-center justify-between gap-2">
                <span className="text-sm"><b>{m.player1Name}</b> te desafiou</span>
                <div className="flex gap-2">
                  <Button size="sm" onClick={() => acceptMut.mutate(m.id)} disabled={acceptMut.isPending}>Aceitar</Button>
                  <Button size="sm" variant="ghost" onClick={() => declineMut.mutate(m.id)}>Recusar</Button>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {/* Matchmaking por fila */}
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">Entrar na fila</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          {profile.isLoading ? (
            <Skeleton className="h-9 w-full" />
          ) : trails.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Inscreva-se em uma trilha pra batalhar com o conteúdo dela.
            </p>
          ) : (
            <>
              <div>
                <p className="mb-2 text-xs font-semibold uppercase text-muted-foreground">Trilha-tema</p>
                <div className="flex flex-wrap gap-2">
                  {trails.map((t) => (
                    <button
                      key={t.trailId}
                      onClick={() => setTrailId(t.trailId)}
                      disabled={queueing}
                      className={cn(
                        "rounded-full border px-3 py-1.5 text-sm transition-colors",
                        trailId === t.trailId
                          ? "border-primary bg-primary/10 text-primary"
                          : "border-border hover:border-primary/50",
                      )}
                    >
                      {t.trailName}
                    </button>
                  ))}
                </div>
              </div>

              {queueing ? (
                <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-popover/50 p-3">
                  <span className="flex items-center gap-2 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" /> Procurando oponente…
                  </span>
                  <Button size="sm" variant="ghost" onClick={() => leaveMut.mutate()}>
                    <X className="mr-1 h-4 w-4" /> Sair
                  </Button>
                </div>
              ) : (
                <Button
                  className="w-full"
                  disabled={trailId === null || enqueueMut.isPending}
                  onClick={() => enqueueMut.mutate()}
                >
                  <Swords className="mr-2 h-4 w-4" />
                  {enqueueMut.isPending ? "Entrando…" : "Buscar duelo"}
                </Button>
              )}
            </>
          )}
        </CardContent>
      </Card>

      {/* Ranking */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <Trophy className="h-4 w-4 text-warning" /> Ranking da Arena
          </CardTitle>
        </CardHeader>
        <CardContent>
          {ranking.isLoading ? (
            <div className="space-y-2">{[0, 1, 2].map((i) => <Skeleton key={i} className="h-8 w-full" />)}</div>
          ) : (ranking.data ?? []).length === 0 ? (
            <p className="text-sm text-muted-foreground">Ninguém pontuou ainda. Seja o primeiro!</p>
          ) : (
            <ol className="space-y-1">
              {ranking.data!.map((row) => (
                <li
                  key={row.userId}
                  className={cn(
                    "flex items-center gap-3 rounded-lg px-3 py-2 text-sm",
                    row.userId === me ? "bg-primary/10" : "odd:bg-muted/30",
                  )}
                >
                  <span className="w-6 text-center font-bold tabular-nums">
                    {row.rank === 1 ? <Crown className="mx-auto h-4 w-4 text-warning" /> : row.rank}
                  </span>
                  <span className="min-w-0 flex-1 truncate font-medium">
                    {row.displayName} {row.userId === me && <span className="text-primary">(você)</span>}
                  </span>
                  <span className="text-xs text-muted-foreground">{row.wins}V · {row.losses}D · {row.draws}E</span>
                  <span className="w-12 text-right font-bold tabular-nums">{row.points}</span>
                </li>
              ))}
            </ol>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function opponentName(m: ArenaMatch, me?: string) {
  return m.player1Id === me ? (m.player2Name ?? "Oponente") : m.player1Name
}
