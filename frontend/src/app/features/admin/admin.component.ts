import { Component, OnDestroy, OnInit, computed, inject, signal } from "@angular/core";
import { CommonModule, DatePipe } from "@angular/common";
import { Router } from "@angular/router";
import { Subscription } from "rxjs";
import { HubConnectionState } from "@microsoft/signalr";
import { AuthService } from "../../core/services/auth.service";
import { AdminService, DailyReplanReport } from "../../core/services/admin.service";
import {
  JourneyHubService,
  DailyPlanGeneratedEvent,
  StreakResetEvent,
} from "../../core/services/journey-hub.service";
import { BottomNavComponent } from "../../shared/components/bottom-nav/bottom-nav.component";

type LiveEvent =
  | { kind: "DailyPlanGenerated"; at: string; data: DailyPlanGeneratedEvent }
  | { kind: "StreakReset"; at: string; data: StreakResetEvent }
  | { kind: "system"; at: string; message: string };

/**
 * /admin — exclusivo para Moderator. Dispara o cron diário on-demand
 * (POST /api/admin/replan-now) e mostra os eventos do SignalR chegando
 * em tempo real. Espelha o backend/test-signalr.html mas dentro do
 * Angular app para demoar o fluxo completo cron → bus → UI.
 *
 * Não é página guardada por role no roteador (apenas no nav esconde
 * o link); se um Student acessar via URL direta, o backend devolve 403
 * e a UI mostra o erro. Defesa em profundidade.
 */
@Component({
  selector: "app-admin",
  standalone: true,
  imports: [CommonModule, DatePipe, BottomNavComponent],
  templateUrl: "./admin.component.html",
  styleUrls: ["./admin.component.scss"],
})
export class AdminComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly admin = inject(AdminService);
  private readonly hub = inject(JourneyHubService);
  private readonly router = inject(Router);

  readonly isModerator = this.auth.isModerator;
  readonly hubState = this.hub.state;
  readonly hubStateLabel = computed(() => {
    switch (this.hubState()) {
      case HubConnectionState.Connected: return "🟢 Conectado";
      case HubConnectionState.Connecting: return "🟡 Conectando…";
      case HubConnectionState.Reconnecting: return "🟡 Reconectando…";
      case HubConnectionState.Disconnected: return "🔴 Desconectado";
      case HubConnectionState.Disconnecting: return "🟡 Desconectando…";
      default: return "—";
    }
  });

  readonly running = signal(false);
  readonly lastReport = signal<DailyReplanReport | null>(null);
  readonly lastError = signal<string | null>(null);
  readonly events = signal<LiveEvent[]>([]);

  private subs = new Subscription();

  async ngOnInit(): Promise<void> {
    try {
      await this.hub.connect();
      this.push({ kind: "system", at: nowIso(), message: "Hub conectado." });
    } catch {
      this.push({
        kind: "system",
        at: nowIso(),
        message: "Falha ao conectar no hub — eventos não serão recebidos.",
      });
    }

    this.subs.add(
      this.hub.dailyPlanGenerated$.subscribe((e) =>
        this.push({ kind: "DailyPlanGenerated", at: nowIso(), data: e }),
      ),
    );
    this.subs.add(
      this.hub.streakReset$.subscribe((e) =>
        this.push({ kind: "StreakReset", at: nowIso(), data: e }),
      ),
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    void this.hub.disconnect();
  }

  triggerReplan(): void {
    this.running.set(true);
    this.lastError.set(null);
    this.admin.replanNow().subscribe({
      next: (r) => {
        this.lastReport.set(r);
        this.running.set(false);
        this.push({
          kind: "system",
          at: nowIso(),
          message: `Cron disparado — processou ${r.processed} (falhas: ${r.failures}, meta cumprida: ${r.yesterdayGoalMet}).`,
        });
      },
      error: (e) => {
        this.running.set(false);
        const msg =
          e?.status === 403
            ? "Você não tem permissão (precisa ser Moderator)."
            : (e?.error?.message ?? "Falha ao disparar cron.");
        this.lastError.set(msg);
        this.push({ kind: "system", at: nowIso(), message: `Erro: ${msg}` });
      },
    });
  }

  clearLog(): void {
    this.events.set([]);
  }

  goBack(): void {
    this.router.navigate(["/dashboard"]);
  }

  private push(e: LiveEvent): void {
    // Prepend e cap em 50 — log infinito polui memória do tab.
    this.events.update((arr) => [e, ...arr].slice(0, 50));
  }
}

function nowIso(): string {
  return new Date().toISOString();
}
