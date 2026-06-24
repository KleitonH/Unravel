import { useEffect, useRef } from "react"
import { HubConnection, LogLevel } from "@microsoft/signalr"
import { useAuth } from "@/stores/auth"
import { buildHubConnection } from "@/lib/signalr"

/**
 * Conexão SignalR "de lobby" da Arena. Mantém uma conexão leve a /hubs/arena
 * enquanto o aluno está na fila/aguardando, só pra receber o push `Matched`
 * (pareamento aconteceu) — sem polling. O servidor direciona via
 * Clients.User(meuId), então basta a conexão estar aberta.
 */
export function useArenaLobby(enabled: boolean, onMatched: (matchId: number) => void) {
  const cbRef = useRef(onMatched)
  cbRef.current = onMatched

  const authed = useAuth((s) => !!s.accessToken)

  useEffect(() => {
    if (!enabled || !authed) return

    // Conexão best-effort: log em Critical pra não poluir o console com o
    // abort esperado quando entramos no duelo (enabled→false aborta a negociação).
    const conn: HubConnection = buildHubConnection("/hubs/arena", LogLevel.Critical)
    conn.on("Matched", (e: { matchId: number }) => cbRef.current(e.matchId))
    conn.start().catch(() => {})

    return () => { void conn.stop() }
  }, [enabled, authed])
}
