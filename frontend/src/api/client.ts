import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios"
import { env } from "@/lib/env"

/**
 * Cliente HTTP único. Anexa Authorization Bearer automaticamente, tenta
 * refresh em 401 (exceto em rotas /auth/), e em falha de refresh
 * dispara logout via callback registrado pelo auth store (evita import
 * circular entre store e cliente).
 */
export const api = axios.create({
  baseURL: env.apiUrl,
  headers: { "Content-Type": "application/json" },
})

let tokenProvider:        (() => string | null) | null = null
let refreshTokenProvider: (() => string | null) | null = null
let onAuthRefreshed:      ((res: { accessToken: string; refreshToken: string }) => void) | null = null
let onAuthFailed:         (() => void) | null = null

/** Registrado uma única vez pelo auth store na inicialização. */
export function configureAuthHooks(opts: {
  getAccessToken: () => string | null
  getRefreshToken: () => string | null
  onRefreshed: (res: { accessToken: string; refreshToken: string }) => void
  onAuthFailed: () => void
}) {
  tokenProvider        = opts.getAccessToken
  refreshTokenProvider = opts.getRefreshToken
  onAuthRefreshed      = opts.onRefreshed
  onAuthFailed         = opts.onAuthFailed
}

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenProvider?.()
  if (token && config.headers) config.headers.Authorization = `Bearer ${token}`
  return config
})

// Estado de single-flight do refresh — múltiplas requests em paralelo que
// caírem em 401 disparam UM refresh; as outras esperam o resultado.
let refreshing: Promise<{ accessToken: string; refreshToken: string }> | null = null

api.interceptors.response.use(
  (r) => r,
  async (error: AxiosError) => {
    const original = error.config as InternalAxiosRequestConfig & { _retried?: boolean }
    if (error.response?.status !== 401) return Promise.reject(error)
    if (original.url?.includes("/auth/")) return Promise.reject(error)
    if (original._retried) {
      onAuthFailed?.()
      return Promise.reject(error)
    }
    original._retried = true

    try {
      refreshing ??= refreshAccessToken()
      const res = await refreshing
      refreshing = null
      onAuthRefreshed?.(res)
      if (original.headers) original.headers.Authorization = `Bearer ${res.accessToken}`
      return api(original)
    } catch (refreshError) {
      refreshing = null
      onAuthFailed?.()
      return Promise.reject(refreshError)
    }
  },
)

async function refreshAccessToken() {
  const refreshToken = refreshTokenProvider?.()
  if (!refreshToken) throw new Error("no refresh token")
  // Chama direto via axios (sem o interceptor) pra evitar recursão.
  const res = await axios.post<{ accessToken: string; refreshToken: string }>(
    `${env.apiUrl}/api/auth/refresh`,
    { refreshToken },
  )
  return res.data
}
