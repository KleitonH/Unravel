import { useEffect, useRef } from "react"
import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr"
import { useAuth } from "@/stores/auth"
import { env } from "@/lib/env"

/**
 * Conexão SignalR "de lobby" da Arena. Mantém uma conexão leve a /hubs/arena
 * enquanto o aluno está na fila/aguardando, só pra receber o push `Matched`
 * (pareamento aconteceu) — sem polling. O servidor direciona via
 * Clients.User(meuId), então basta a conexão estar aberta.
 */
export function useArenaLobby(enabled: boolean, onMatched: (matchId: number) => void) {
  const cbRef = useRef(onMatched)
  cbRef.current = onMatched

  useEffect(() => {
    if (!enabled) return
    const token = useAuth.getState().accessToken
    if (!token) return

    const conn: HubConnection = new HubConnectionBuilder()
      .withUrl(`${env.apiUrl}/hubs/arena`, {
        accessTokenFactory: () => useAuth.getState().accessToken ?? "",
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    conn.on("Matched", (e: { matchId: number }) => cbRef.current(e.matchId))
    conn.start().catch(() => {})

    return () => { void conn.stop() }
  }, [enabled])
}
