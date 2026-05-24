import { Component, inject, signal, computed } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";
import { TrailService } from "../../core/services/trail.service";
import { OnboardingService } from "../../core/services/onboarding.service";
import { TrailResponse } from "../../core/models/trail.model";
import {
  LevelingAnswer,
  OnboardingResult,
  OnboardingTest,
} from "../../core/models/onboarding.model";

type Step = "pick" | "test" | "result";

/**
 * Onboarding de duas etapas:
 *   1) "pick"   — usuário escolhe trilhas
 *   2) "test"   — responde teste de nivelamento gerado pelo backend
 *   3) "result" — vê o nível estimado + CTA pro plano do dia
 *
 * Estado todo via signals (sem RxJS gymnastics). Submit reusa a lista
 * de trailIds escolhida porque o backend exige no query string.
 */
@Component({
  selector: "app-onboarding",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./onboarding.component.html",
  styleUrls: ["./onboarding.component.scss"],
})
export class OnboardingComponent {
  private readonly trailService = inject(TrailService);
  private readonly onboarding = inject(OnboardingService);
  private readonly router = inject(Router);

  // estado da página
  readonly step = signal<Step>("pick");
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  // step 1
  readonly trails = signal<TrailResponse[]>([]);
  readonly selectedIds = signal<Set<number>>(new Set());
  readonly hasSelection = computed(() => this.selectedIds().size > 0);

  // step 2
  readonly test = signal<OnboardingTest | null>(null);
  readonly answers = signal<Map<number, number>>(new Map());
  readonly totalQuestions = computed(
    () => this.test()?.trails.reduce((acc, g) => acc + g.questions.length, 0) ?? 0,
  );
  readonly answeredCount = computed(() => this.answers().size);
  readonly canSubmit = computed(
    () => this.totalQuestions() > 0 && this.answeredCount() === this.totalQuestions(),
  );

  // step 3
  readonly result = signal<OnboardingResult | null>(null);

  constructor() {
    this.trailService.getAll().subscribe({
      next: (t) => this.trails.set(t),
      error: () => this.error.set("Não foi possível carregar as trilhas."),
    });
  }

  toggleTrail(id: number): void {
    const next = new Set(this.selectedIds());
    next.has(id) ? next.delete(id) : next.add(id);
    this.selectedIds.set(next);
  }

  startTest(): void {
    if (!this.hasSelection()) return;
    this.loading.set(true);
    this.error.set(null);
    const ids = Array.from(this.selectedIds());
    this.onboarding.start(ids).subscribe({
      next: (t) => {
        this.test.set(t);
        this.step.set("test");
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(
          e?.error?.message ?? "Falha ao iniciar onboarding. Pode ser que já tenha sido feito.",
        );
        this.loading.set(false);
      },
    });
  }

  answer(topicId: number, optionIndex: number): void {
    const next = new Map(this.answers());
    next.set(topicId, optionIndex);
    this.answers.set(next);
  }

  isPicked(topicId: number, optionIndex: number): boolean {
    return this.answers().get(topicId) === optionIndex;
  }

  submitTest(): void {
    if (!this.canSubmit()) return;
    this.loading.set(true);
    this.error.set(null);
    const ids = Array.from(this.selectedIds());
    const payload: { answers: LevelingAnswer[] } = {
      answers: Array.from(this.answers().entries()).map(([topicId, selectedOptionIndex]) => ({
        topicId,
        selectedOptionIndex,
      })),
    };
    this.onboarding.submit(ids, payload).subscribe({
      next: (r) => {
        this.result.set(r);
        this.step.set("result");
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(e?.error?.message ?? "Falha ao submeter respostas.");
        this.loading.set(false);
      },
    });
  }

  goToJourney(trailId: number): void {
    this.router.navigate(["/jornada", trailId]);
  }
}
