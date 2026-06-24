import { useEffect, useRef, useState } from "react"
import { HubConnection, HubConnectionState } from "@microsoft/signalr"
import { useAuth } from "@/stores/auth"
import { buildHubConnection } from "@/lib/signalr"
import type { LiveLeaderboardRow, LiveQuestion, LiveQuestionResult, LiveSession } from "@/api/live-quiz"

type SubmitResult = {
  accepted: boolean; isCorrect: boolean; points: number; totalScore: number; correctIndex: number
}

type PlayerHandlers = {
  onJoined?:         (e: { session: LiveSession }) => void
  onJoinError?:      (reason: string) => void
  onQuestionStarted?:(q: LiveQuestion) => void
  onAnswerResult?:   (r: SubmitResult) => void
  onQuestionEnded?:  (e: { result: LiveQuestionResult; leaderboard: LiveLeaderboardRow[] }) => void
  onSessionEnded?:   (rows: LiveLeaderboardRow[]) => void
}

/**
 * Conexão SignalR do PARTICIPANTE (/hubs/live-quiz). Conecta ao montar;
 * `join(code)` entra na sala; `submit(...)` envia resposta. Os eventos
 * (pergunta iniciada, resultado, fim de rodada, pódio) chegam pelos handlers.
 */
export function useLiveQuizPlayer(handlers: PlayerHandlers) {
  const handlersRef = useRef(handlers)
  handlersRef.current = handlers
  const connRef = useRef<HubConnection | null>(null)
  const [state, setState] = useState<HubConnectionState>(HubConnectionState.Disconnected)
  const authed = useAuth((s) => !!s.accessToken)
  const lastCodeRef = useRef<string | null>(null) // pra re-entrar na sala ao reconectar

  useEffect(() => {
    if (!authed) return

    const conn = buildHubConnection("/hubs/live-quiz")
    connRef.current = conn

    const h = handlersRef
    conn.on("Joined",         (e) => h.current.onJoined?.(e))
    conn.on("JoinError",      (r) => h.current.onJoinError?.(r))
    conn.on("QuestionStarted",(q) => h.current.onQuestionStarted?.(q))
    conn.on("AnswerResult",   (r) => h.current.onAnswerResult?.(r))
    conn.on("QuestionEnded",  (e) => h.current.onQuestionEnded?.(e))
    conn.on("SessionEnded",   (r) => h.current.onSessionEnded?.(r))

    conn.onreconnecting(() => setState(HubConnectionState.Reconnecting))
    conn.onreconnected(() => {
      setState(HubConnectionState.Connected)
      // Reconectou → entra de novo na sala (o grupo SignalR se perde na queda).
      if (lastCodeRef.current) void conn.invoke("JoinSession", lastCodeRef.current)
    })
    conn.onclose(() => setState(HubConnectionState.Disconnected))

    setState(HubConnectionState.Connecting)
    conn.start()
      .then(() => setState(HubConnectionState.Connected))
      .catch(() => setState(HubConnectionState.Disconnected))

    return () => { connRef.current = null; void conn.stop() }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authed])

  const invoke = (method: string, ...args: unknown[]) =>
    connRef.current?.state === HubConnectionState.Connected
      ? connRef.current.invoke(method, ...args)
      : Promise.resolve()

  return {
    state,
    join:   (code: string) => {
      const c = code.trim().toUpperCase()
      lastCodeRef.current = c
      return invoke("JoinSession", c)
    },
    submit: (sessionId: number, orderIndex: number, optionIndex: number) =>
      invoke("SubmitAnswer", sessionId, orderIndex, optionIndex),
  }
}
