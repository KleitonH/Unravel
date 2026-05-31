import { create } from "zustand"
import { persist } from "zustand/middleware"
import { authApi } from "@/api/auth"
import { configureAuthHooks } from "@/api/client"
import type { AuthResponse, LoginRequest, RegisterRequest, User } from "@/types/api"

type AuthState = {
  accessToken:  string | null
  refreshToken: string | null
  user:         User | null
  // Role vem do claim do JWT (decodificado client-side); backend é a fonte real.
  role:         string | null

  isAuthenticated: () => boolean
  isModerator:     () => boolean

  login:    (body: LoginRequest)    => Promise<void>
  register: (body: RegisterRequest) => Promise<void>
  logout:   () => void
  hydrate:  () => Promise<void>
}

/**
 * Auth store Zustand persistido em localStorage. Combina com o
 * `configureAuthHooks` do API client para single-flight refresh.
 */
export const useAuth = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken:  null,
      refreshToken: null,
      user:         null,
      role:         null,

      // Token presente basta — se inválido, interceptor 401 dispara logout
      // via onAuthFailed. Esperar `user` carregar bloquearia a UX no boot.
      isAuthenticated: () => !!get().accessToken,
      isModerator:     () => get().role === "Moderator",

      async login(body) {
        const res = await authApi.login(body)
        applyAuth(set, res)
      },

      async register(body) {
        await authApi.register(body)
        await get().login({ email: body.email, password: body.password })
      },

      logout() {
        set({ accessToken: null, refreshToken: null, user: null, role: null })
      },

      /** Chamado uma vez no boot: se há token persistido, valida via /users/me. */
      async hydrate() {
        const token = get().accessToken
        if (!token) return
        try {
          const user = await authApi.me()
          set({ user, role: decodeRoleFromToken(token) })
        } catch {
          // Token inválido/expirado e refresh também — limpa.
          get().logout()
        }
      },
    }),
    {
      name: "unravel-auth",
      // Persiste só tokens — re-carrega user via /me na hidratação.
      partialize: (s) => ({
        accessToken:  s.accessToken,
        refreshToken: s.refreshToken,
      }),
    },
  ),
)

function applyAuth(set: (partial: Partial<AuthState>) => void, res: AuthResponse) {
  set({
    accessToken:  res.accessToken,
    refreshToken: res.refreshToken,
    user:         res.user,
    role:         decodeRoleFromToken(res.accessToken),
  })
}

/** Decode payload do JWT (sem validar assinatura — backend é quem valida). */
function decodeRoleFromToken(token: string | null): string | null {
  if (!token) return null
  try {
    const payload = JSON.parse(
      atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")),
    )
    return (
      payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ??
      payload.role ??
      null
    )
  } catch {
    return null
  }
}

// Conecta o auth store ao API client (configureAuthHooks).
// Isso roda uma vez no import deste módulo (no boot da app).
configureAuthHooks({
  getAccessToken:  () => useAuth.getState().accessToken,
  getRefreshToken: () => useAuth.getState().refreshToken,
  onRefreshed: ({ accessToken, refreshToken }) => {
    useAuth.setState({
      accessToken,
      refreshToken,
      role: decodeRoleFromToken(accessToken),
    })
  },
  onAuthFailed: () => useAuth.getState().logout(),
})
