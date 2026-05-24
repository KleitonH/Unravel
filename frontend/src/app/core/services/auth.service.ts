import { Injectable, signal, computed, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Router } from "@angular/router";
import { tap } from "rxjs/operators";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import {
  AuthResponse,
  CreateUserRequest,
  LoginRequest,
  User,
} from "../models/user.model";

const ACCESS_TOKEN_KEY = "access_token";
const REFRESH_TOKEN_KEY = "refresh_token";

@Injectable({ providedIn: "root" })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly apiUrl = `${environment.apiUrl}/api`;

  private readonly _currentUser = signal<User | null>(null);

  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this._currentUser() !== null);

  /** Role extraído do claim do JWT atual. null se não logado. Útil para
   *  esconder/exibir itens de UI; a autorização real está no backend. */
  readonly currentRole = computed<string | null>(() => {
    // computed reage ao currentUser; quando muda (login/logout), reavalia.
    void this._currentUser();
    return this.decodeRoleFromToken(this.getAccessToken());
  });

  readonly isModerator = computed(() => this.currentRole() === "Moderator");

  constructor() {
    const token = this.getAccessToken();
    if (token) {
      this.loadCurrentUser().subscribe({ error: () => this.clearTokens() });
    }
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/auth/login`, request)
      .pipe(tap((response) => this.handleAuthResponse(response)));
  }

  register(request: CreateUserRequest): Observable<User> {
    return this.http.post<User>(`${this.apiUrl}/users`, request);
  }

  refresh(): Observable<AuthResponse> {
    const refreshToken = this.getRefreshToken();
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/auth/refresh`, { refreshToken })
      .pipe(tap((response) => this.handleAuthResponse(response)));
  }

  loadCurrentUser(): Observable<User> {
    return this.http
      .get<User>(`${this.apiUrl}/users/me`)
      .pipe(tap((user) => this._currentUser.set(user)));
  }

  logout(): void {
    this.clearTokens();
    this._currentUser.set(null);
    this.router.navigate(["/auth/login"]);
  }

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  private handleAuthResponse(response: AuthResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    this._currentUser.set(response.user);
  }

  private clearTokens(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
  }

  /** Decodifica payload do JWT (sem validar assinatura — quem valida é o
   *  backend) e devolve o claim de role. Aceita tanto o claim long-form
   *  `http://schemas.microsoft.com/.../role` quanto o short `role`. */
  private decodeRoleFromToken(token: string | null): string | null {
    if (!token) return null;
    try {
      const payload = JSON.parse(
        atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")),
      );
      return (
        payload[
          "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        ] ??
        payload.role ??
        null
      );
    } catch {
      return null;
    }
  }
}
