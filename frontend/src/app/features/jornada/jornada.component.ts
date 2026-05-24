import { Component, OnDestroy, OnInit, inject, signal } from "@angular/core";
import { CommonModule, DecimalPipe } from "@angular/common";
import { ActivatedRoute, Router } from "@angular/router";
import { Subscription } from "rxjs";
import { JourneyService } from "../../core/services/journey.service";
import {
  JourneyHubService,
  DailyPlanGeneratedEvent,
  StreakResetEvent,
} from "../../core/services/journey-hub.service";
import { JourneyPlan, JourneyReason } from "../../core/models/journey.model";

const REASON_LABEL: Record<JourneyReason, string> = {
  NewLearning: "Novo",
  DueReview: "Revisão",
  Reinforce: "Reforço",
};

const REASON_ICON: Record<JourneyReason, string> = {
  NewLearning: "✨",
  DueReview: "🔄",
  Reinforce: "💪",
};

/**
 * Página /jornada/:trailId — mostra o JourneyPlan do backend e abre
 * conexão SignalR para receber notificações live (DailyPlanGenerated,
 * StreakReset). Re-busca o plano quando o evento corresponde à trilha
 * em tela — assim o usuário vê a meta atualizar sem refresh manual.
 */
@Component({
  selector: "app-jornada",
  standalone: true,
  imports: [CommonModule, DecimalPipe],
  templateUrl: "./jornada.component.html",
  styleUrls: ["./jornada.component.scss"],
})
export class JornadaComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly journey = inject(JourneyService);
  private readonly hub = inject(JourneyHubService);

  readonly trailId = signal<number>(0);
  readonly plan = signal<JourneyPlan | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly liveBadge = signal<string | null>(null);

  private subs = new Subscription();

  readonly reasonLabel = REASON_LABEL;
  readonly reasonIcon = REASON_ICON;

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get("trailId"));
    this.trailId.set(id);
    this.refresh();
    this.connectHub();
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    void this.hub.disconnect();
  }

  refresh(): void {
    this.loading.set(true);
    this.error.set(null);
    this.journey.today(this.trailId()).subscribe({
      next: (p) => {
        this.plan.set(p);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(
          e?.status === 404
            ? "Trilha não encontrada ou usuário sem perfil — passe pelo onboarding antes."
            : "Falha ao carregar o plano.",
        );
        this.loading.set(false);
      },
    });
  }

  private connectHub(): void {
    this.hub
      .connect()
      .then(() => {
        this.subs.add(
          this.hub.dailyPlanGenerated$.subscribe((e: DailyPlanGeneratedEvent) => {
            if (e.trailId === this.trailId()) {
              this.flashLive(`📅 Nova meta gerada: ${e.metaDia} desafios${e.extraPenalty ? ` (+${e.extraPenalty} de penalidade)` : ""}`);
              this.refresh();
            }
          }),
        );
        this.subs.add(
          this.hub.streakReset$.subscribe((e: StreakResetEvent) => {
            this.flashLive(`🔥 Sua sequência foi resetada (era ${e.previousStreak} dias)`);
          }),
        );
      })
      .catch(() => {
        // Conexão WS falhou — não bloqueia o uso, só perde live updates.
      });
  }

  private flashLive(message: string): void {
    this.liveBadge.set(message);
    setTimeout(() => this.liveBadge.set(null), 6000);
  }

  goToQuiz(contentId: number): void {
    this.router.navigate(["/quiz", contentId]);
  }
}
