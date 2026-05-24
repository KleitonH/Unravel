import { Injectable, OnDestroy, inject, signal } from "@angular/core";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { Subject } from "rxjs";
import { environment } from "../../../environments/environment";
import { AuthService } from "./auth.service";

/**
 * Cliente do hub /hubs/journey (PR 8 do backend). Conecta com JWT via
 * accessTokenFactory (cobre o caso WebSocket onde o navegador não envia
 * o header Authorization), entra automaticamente no grupo user:{userId}
 * (lógica no servidor) e expõe os eventos como Subjects RxJS.
 *
 * Padrão de uso: providedIn: 'root' (singleton), assina os Subjects
 * uma vez e chama connect() depois do login.
 */
export interface DailyPlanGeneratedEvent {
  userId: string;
  trailId: number;
  planDate: string;
  metaDia: number;
  extraPenalty: number;
  metGoalYesterday: boolean | null;
}

export interface StreakResetEvent {
  userId: string;
  previousStreak: number;
  resetAt: string;
}

@Injectable({ providedIn: "root" })
export class JourneyHubService implements OnDestroy {
  private readonly auth = inject(AuthService);
  private connection?: HubConnection;

  readonly dailyPlanGenerated$ = new Subject<DailyPlanGeneratedEvent>();
  readonly streakReset$ = new Subject<StreakResetEvent>();

  readonly state = signal<HubConnectionState>(HubConnectionState.Disconnected);

  async connect(): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiUrl}/hubs/journey`, {
        accessTokenFactory: () => this.auth.getAccessToken() ?? "",
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on("DailyPlanGenerated", (e: DailyPlanGeneratedEvent) =>
      this.dailyPlanGenerated$.next(e),
    );
    this.connection.on("StreakReset", (e: StreakResetEvent) =>
      this.streakReset$.next(e),
    );

    this.connection.onreconnecting(() =>
      this.state.set(HubConnectionState.Reconnecting),
    );
    this.connection.onreconnected(() =>
      this.state.set(HubConnectionState.Connected),
    );
    this.connection.onclose(() =>
      this.state.set(HubConnectionState.Disconnected),
    );

    await this.connection.start();
    this.state.set(HubConnectionState.Connected);
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.state.set(HubConnectionState.Disconnected);
    }
  }

  ngOnDestroy(): void {
    void this.disconnect();
  }
}
