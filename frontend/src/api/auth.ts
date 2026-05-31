import { api } from "./client"
import type { AuthResponse, LoginRequest, RegisterRequest, User } from "@/types/api"

export const authApi = {
  login: (body: LoginRequest) =>
    api.post<AuthResponse>("/api/auth/login", body).then((r) => r.data),

  register: (body: RegisterRequest) =>
    api.post<User>("/api/users", body).then((r) => r.data),

  me: () => api.get<User>("/api/users/me").then((r) => r.data),
}
