import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr"
import { useAuth } from "@/stores/auth"
import { env } from "@/lib/env"

/** Backoff de reconexão automática (ms): imediato, 2s, 5s, 10s, 20s, depois para. */
const RECONNECT_DELAYS = [0, 2000, 5000, 10000, 20000]

/**
 * Cria uma conexão SignalR padronizada pro projeto: base `env.apiUrl`, JWT via
 * `accessTokenFactory` (sempre lê o token atual do store, então sobrevive a
 * refresh), reconexão automática com backoff e log só de warnings.
 *
 * Conecte apenas quando houver token (use `useAuth(s => !!s.accessToken)` como
 * gate no efeito) — iniciar sem token faz a negociação falhar.
 */
export function buildHubConnection(path: string, logLevel: LogLevel = LogLevel.Warning): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${env.apiUrl}${path}`, {
      accessTokenFactory: () => useAuth.getState().accessToken ?? "",
    })
    .withAutomaticReconnect(RECONNECT_DELAYS)
    .configureLogging(logLevel)
    .build()
}
