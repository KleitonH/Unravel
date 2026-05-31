import { useEffect, useRef, useState } from "react"
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr"
import { useAuth } from "@/stores/auth"
import { env } from "@/lib/env"
import type { DailyPlanGeneratedEvent, StreakResetEvent } from "@/types/api"

type Handlers = {
  onDailyPlanGenerated?: (e: DailyPlanGeneratedEvent) => void
  onStreakReset?:        (e: StreakResetEvent) => void
}

/**
 * Hook que abre uma conexão SignalR com /hubs/journey enquanto o componente
 * está montado. Single-flight: o `useRef` garante uma conexão por
 * componente. O backend coloca o user no grupo `user:{userId}` no
 * OnConnectedAsync, então cada client recebe só seus próprios eventos.
 *
 * <para>Os handlers ficam estáveis via ref pra evitar re-conectar a cada
 * re-render quando o componente passa novas callbacks inline.</para>
 */
export function useJourneyHub(handlers: Handlers) {
  const handlersRef = useRef(handlers)
  handlersRef.current = handlers

  const [state, setState] = useState<HubConnectionState>(HubConnectionState.Disconnected)

  useEffect(() => {
    const token = useAuth.getState().accessToken
    if (!token) return

    const connection: HubConnection = new HubConnectionBuilder()
      .withUrl(`${env.apiUrl}/hubs/journey`, {
        accessTokenFactory: () => useAuth.getState().accessToken ?? "",
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on("DailyPlanGenerated", (e: DailyPlanGeneratedEvent) =>
      handlersRef.current.onDailyPlanGenerated?.(e),
    )
    connection.on("StreakReset", (e: StreakResetEvent) =>
      handlersRef.current.onStreakReset?.(e),
    )

    connection.onreconnecting(() => setState(HubConnectionState.Reconnecting))
    connection.onreconnected(() => setState(HubConnectionState.Connected))
    connection.onclose(() => setState(HubConnectionState.Disconnected))

    setState(HubConnectionState.Connecting)
    connection.start()
      .then(() => setState(HubConnectionState.Connected))
      .catch(() => setState(HubConnectionState.Disconnected))

    return () => {
      void connection.stop()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return state
}
