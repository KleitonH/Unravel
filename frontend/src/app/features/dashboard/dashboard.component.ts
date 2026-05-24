import { Component, computed, inject, signal } from "@angular/core";
import { CommonModule, DecimalPipe } from "@angular/common";
import { Router } from "@angular/router";
import { forkJoin, of } from "rxjs";
import { catchError, map, switchMap } from "rxjs/operators";
import { AuthService } from "../../core/services/auth.service";
import { ProfileService } from "../../core/services/profile.service";
import { TrailService } from "../../core/services/trail.service";
import { JourneyService } from "../../core/services/journey.service";
import { isStudentProfile, StudentProfile } from "../../core/models/profile.model";
import { TrailResponse } from "../../core/models/trail.model";
import { JourneyPlan } from "../../core/models/journey.model";
import { BottomNavComponent } from "../../shared/components/bottom-nav/bottom-nav.component";

type EnrolledTrailCard = {
  trail: TrailResponse;
  plan: JourneyPlan | null;     // null se falhou (ex: 404 sem mastery ainda)
};

/**
 * Dashboard reescrito para consumir o algoritmo de jornada (PR 12):
 *
 *   1) Carrega o profile (XP, streak, lives — chips do header).
 *   2) Lista trilhas inscritas (userProgress >= 0 do TrailResponse).
 *   3) Para cada trilha inscrita, busca o JourneyPlan do dia em paralelo
 *      (forkJoin); falhas isoladas viram `plan: null` para não derrubar
 *      o dashboard inteiro.
 *   4) Mostra meta-dia + 2 primeiros items do today + link "Ver jornada
 *      completa" → /jornada/:trailId.
 *
 * Cold-start (zero trilhas inscritas) ⇒ CTA único para /onboarding.
 *
 * Substitui o conteúdo mockado anterior (trilha ativa fixa + desafio do
 * dia hardcoded) e o nav inline. Agora usa app-bottom-nav padrão.
 */
@Component({
  selector: "app-dashboard",
  standalone: true,
  imports: [CommonModule, DecimalPipe, BottomNavComponent],
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss",
})
export class DashboardComponent {
  private readonly auth = inject(AuthService);
  private readonly profileService = inject(ProfileService);
  private readonly trailService = inject(TrailService);
  private readonly journey = inject(JourneyService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<StudentProfile | null>(null);
  readonly cards = signal<EnrolledTrailCard[]>([]);

  readonly userName = computed(
    () => this.profile()?.name ?? this.auth.currentUser()?.name ?? "estudante",
  );

  constructor() {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.error.set(null);

    // Carrega profile + trilhas em paralelo. Depois, para as inscritas,
    // dispara N journey.today() em paralelo.
    forkJoin({
      profile: this.profileService.getProfile().pipe(
        catchError(() => of(null)),
      ),
      trails: this.trailService.getAll().pipe(
        catchError(() => of<TrailResponse[]>([])),
      ),
    })
      .pipe(
        switchMap(({ profile, trails }) => {
          if (profile && isStudentProfile(profile)) {
            this.profile.set(profile);
          }
          const enrolled = trails.filter((t) => t.userProgress >= 0);
          if (enrolled.length === 0) return of<EnrolledTrailCard[]>([]);

          // Plano do dia por trilha — falha 1 não derruba as outras.
          return forkJoin(
            enrolled.map((t) =>
              this.journey.today(t.id).pipe(
                map((plan) => ({ trail: t, plan }) as EnrolledTrailCard),
                catchError(() => of({ trail: t, plan: null })),
              ),
            ),
          );
        }),
      )
      .subscribe({
        next: (cards) => {
          this.cards.set(cards);
          this.loading.set(false);
        },
        error: () => {
          this.error.set("Não foi possível carregar o dashboard.");
          this.loading.set(false);
        },
      });
  }

  goToOnboarding(): void {
    this.router.navigate(["/onboarding"]);
  }

  goToJourney(trailId: number): void {
    this.router.navigate(["/jornada", trailId]);
  }

  goToTrails(): void {
    this.router.navigate(["/trails"]);
  }

  logout(): void {
    this.auth.logout();
  }
}
