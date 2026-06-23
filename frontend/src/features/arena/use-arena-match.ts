import { useEffect, useRef, useState } from "react"
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr"
import { useAuth } from "@/stores/auth"
import { env } from "@/lib/env"
import type { ArenaMatch, ArenaRound, ArenaRoundResult } from "@/api/arena"

type ArenaAnswerResult = {
  accepted: boolean
  isCorrect: boolean
  points: number
  roundResolved: boolean
  matchFinished: boolean
  correctIndex: number
}

type Handlers = {
  onMatch?:          (m: ArenaMatch) => void
  onRoundStarted?:   (r: ArenaRound) => void
  onAnswerResult?:   (r: ArenaAnswerResult) => void
  onOpponentAnswered?: (e: { roundIndex: number }) => void
  onRoundResult?:    (r: ArenaRoundResult) => void
  onMatchFinished?:  (m: ArenaMatch) => void
}

/**
 * Conexão SignalR do duelo da Arena (/hubs/arena). Conecta ao montar e entra
 * na sala da partida; `submit(...)` envia a resposta. Sem host: a rodada
 * avança sozinha quando os dois respondem (eventos RoundResult/RoundStarted/
 * MatchFinished chegam pelos handlers).
 */
export function useArenaMatch(matchId: number, handlers: Handlers) {
  const handlersRef = useRef(handlers)
  handlersRef.current = handlers
  const connRef = useRef<HubConnection | null>(null)
  const [state, setState] = useState<HubConnectionState>(HubConnectionState.Disconnected)

  useEffect(() => {
    const token = useAuth.getState().accessToken
    if (!token || !matchId) return

    const conn = new HubConnectionBuilder()
      .withUrl(`${env.apiUrl}/hubs/arena`, {
        accessTokenFactory: () => useAuth.getState().accessToken ?? "",
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    connRef.current = conn

    const h = handlersRef
    conn.on("Match",           (m) => h.current.onMatch?.(m))
    conn.on("RoundStarted",    (r) => h.current.onRoundStarted?.(r))
    conn.on("AnswerResult",    (r) => h.current.onAnswerResult?.(r))
    conn.on("OpponentAnswered",(e) => h.current.onOpponentAnswered?.(e))
    conn.on("RoundResult",     (r) => h.current.onRoundResult?.(r))
    conn.on("MatchFinished",   (m) => h.current.onMatchFinished?.(m))

    conn.onreconnecting(() => setState(HubConnectionState.Reconnecting))
    conn.onreconnected(async () => {
      setState(HubConnectionState.Connected)
      await conn.invoke("JoinMatch", matchId).catch(() => {})
    })
    conn.onclose(() => setState(HubConnectionState.Disconnected))

    setState(HubConnectionState.Connecting)
    conn.start()
      .then(async () => {
        setState(HubConnectionState.Connected)
        await conn.invoke("JoinMatch", matchId)
      })
      .catch(() => setState(HubConnectionState.Disconnected))

    return () => { connRef.current = null; void conn.stop() }
  }, [matchId])

  const submit = (roundIndex: number, selectedIndex: number) =>
    connRef.current?.state === HubConnectionState.Connected
      ? connRef.current.invoke("SubmitAnswer", matchId, roundIndex, selectedIndex)
      : Promise.resolve()

  return { state, submit }
}
