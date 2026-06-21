import { useEffect, useRef, useState } from "react"
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr"
import { useAuth } from "@/stores/auth"
import { env } from "@/lib/env"
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

  useEffect(() => {
    const token = useAuth.getState().accessToken
    if (!token) return

    const conn = new HubConnectionBuilder()
      .withUrl(`${env.apiUrl}/hubs/live-quiz`, {
        accessTokenFactory: () => useAuth.getState().accessToken ?? "",
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    connRef.current = conn

    const h = handlersRef
    conn.on("Joined",         (e) => h.current.onJoined?.(e))
    conn.on("JoinError",      (r) => h.current.onJoinError?.(r))
    conn.on("QuestionStarted",(q) => h.current.onQuestionStarted?.(q))
    conn.on("AnswerResult",   (r) => h.current.onAnswerResult?.(r))
    conn.on("QuestionEnded",  (e) => h.current.onQuestionEnded?.(e))
    conn.on("SessionEnded",   (r) => h.current.onSessionEnded?.(r))

    conn.onreconnecting(() => setState(HubConnectionState.Reconnecting))
    conn.onreconnected(() => setState(HubConnectionState.Connected))
    conn.onclose(() => setState(HubConnectionState.Disconnected))

    setState(HubConnectionState.Connecting)
    conn.start()
      .then(() => setState(HubConnectionState.Connected))
      .catch(() => setState(HubConnectionState.Disconnected))

    return () => { connRef.current = null; void conn.stop() }
  }, [])

  const invoke = (method: string, ...args: unknown[]) =>
    connRef.current?.state === HubConnectionState.Connected
      ? connRef.current.invoke(method, ...args)
      : Promise.resolve()

  return {
    state,
    join:   (code: string) => invoke("JoinSession", code.trim().toUpperCase()),
    submit: (sessionId: number, orderIndex: number, optionIndex: number) =>
      invoke("SubmitAnswer", sessionId, orderIndex, optionIndex),
  }
}
