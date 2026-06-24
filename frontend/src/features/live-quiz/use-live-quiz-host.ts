import { useEffect, useRef, useState } from "react"
import { HubConnection, HubConnectionState } from "@microsoft/signalr"
import { useAuth } from "@/stores/auth"
import { buildHubConnection } from "@/lib/signalr"
import type { LiveLeaderboardRow, LiveQuestion, LiveQuestionResult, LiveSession } from "@/api/live-quiz"

type HostHandlers = {
  onSession?:          (s: LiveSession) => void
  onLeaderboard?:      (rows: LiveLeaderboardRow[]) => void
  onParticipant?:      (e: { name: string; count: number }) => void
  onQuestionStarted?:  (q: LiveQuestion) => void
  onAnswerTally?:      (e: { orderIndex: number; count: number }) => void
  onQuestionEnded?:    (e: { result: LiveQuestionResult; leaderboard: LiveLeaderboardRow[] }) => void
  onSessionEnded?:     (rows: LiveLeaderboardRow[]) => void
}

/**
 * Conexão SignalR do HOST do Quiz ao Vivo (/hubs/live-quiz). Ao conectar,
 * invoca HostJoin(sessionId) pra entrar no grupo da sala e receber o estado.
 * Retorna o estado da conexão + os comandos do host (start/reveal/next/end).
 */
export function useLiveQuizHost(sessionId: number, handlers: HostHandlers) {
  const handlersRef = useRef(handlers)
  handlersRef.current = handlers
  const connRef = useRef<HubConnection | null>(null)
  const [state, setState] = useState<HubConnectionState>(HubConnectionState.Disconnected)
  const authed = useAuth((s) => !!s.accessToken)

  useEffect(() => {
    if (!authed || !sessionId) return

    const conn = buildHubConnection("/hubs/live-quiz")
    connRef.current = conn

    const h = handlersRef
    conn.on("Session",          (s) => h.current.onSession?.(s))
    conn.on("Leaderboard",      (r) => h.current.onLeaderboard?.(r))
    conn.on("ParticipantJoined",(e) => h.current.onParticipant?.(e))
    conn.on("QuestionStarted",  (q) => h.current.onQuestionStarted?.(q))
    conn.on("AnswerTally",      (e) => h.current.onAnswerTally?.(e))
    conn.on("QuestionEnded",    (e) => h.current.onQuestionEnded?.(e))
    conn.on("SessionEnded",     (r) => h.current.onSessionEnded?.(r))

    conn.onreconnected(() => { setState(HubConnectionState.Connected); void conn.invoke("HostJoin", sessionId) })
    conn.onreconnecting(() => setState(HubConnectionState.Reconnecting))
    conn.onclose(() => setState(HubConnectionState.Disconnected))

    setState(HubConnectionState.Connecting)
    conn.start()
      .then(() => { setState(HubConnectionState.Connected); return conn.invoke("HostJoin", sessionId) })
      .catch(() => setState(HubConnectionState.Disconnected))

    return () => { connRef.current = null; void conn.stop() }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId, authed])

  const invoke = (method: string, ...args: unknown[]) =>
    connRef.current?.state === HubConnectionState.Connected
      ? connRef.current.invoke(method, ...args)
      : Promise.resolve()

  return {
    state,
    start:  () => invoke("StartSession", sessionId),
    reveal: (orderIndex: number) => invoke("RevealQuestion", sessionId, orderIndex),
    next:   () => invoke("NextQuestion", sessionId),
    end:    () => invoke("EndSession", sessionId),
  }
}
