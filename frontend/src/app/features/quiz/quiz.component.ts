import { Component, OnInit, computed, inject, signal } from "@angular/core";
import { CommonModule, DecimalPipe } from "@angular/common";
import { ActivatedRoute, Router } from "@angular/router";
import { ChallengePoolService } from "../../core/services/challenge-pool.service";
import {
  ChallengePool,
  PoolChallenge,
} from "../../core/models/challenge-pool.model";
import { BottomNavComponent } from "../../shared/components/bottom-nav/bottom-nav.component";

type AnswerState = {
  selectedIndex: number;
  isCorrect: boolean;
};

/**
 * Página /quiz/:contentId — carrega o pool gerado pelo Forge e apresenta
 * uma pergunta por vez, com gabarito após a escolha. Não submete para o
 * backend (endpoint de submissão é o ChallengeService.SubmitAsync que
 * trabalha sobre Challenge, não GeneratedChallenge). Stub honesto: a
 * resposta fica só no front; quando o backend tiver endpoint dedicado
 * para validar GeneratedChallenge + atualizar Mastery, plugamos aqui.
 */
@Component({
  selector: "app-quiz",
  standalone: true,
  imports: [CommonModule, DecimalPipe, BottomNavComponent],
  templateUrl: "./quiz.component.html",
  styleUrls: ["./quiz.component.scss"],
})
export class QuizComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly pool = inject(ChallengePoolService);

  readonly contentId = signal<number>(0);
  readonly data = signal<ChallengePool | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly currentIndex = signal(0);
  readonly answers = signal<Map<number, AnswerState>>(new Map());

  readonly current = computed<PoolChallenge | null>(() => {
    const d = this.data();
    if (!d) return null;
    return d.challenges[this.currentIndex()] ?? null;
  });

  readonly answered = computed(() => {
    const c = this.current();
    return c ? this.answers().has(c.id) : false;
  });

  readonly score = computed(() => {
    let correct = 0;
    this.answers().forEach((a) => a.isCorrect && correct++);
    return correct;
  });

  readonly totalAnswered = computed(() => this.answers().size);
  readonly totalQuestions = computed(() => this.data()?.challenges.length ?? 0);
  readonly isFinished = computed(
    () => this.totalQuestions() > 0 && this.totalAnswered() === this.totalQuestions(),
  );

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get("contentId"));
    this.contentId.set(id);
    this.loadPool();
  }

  private loadPool(): void {
    this.loading.set(true);
    this.error.set(null);
    this.pool.pool(this.contentId(), 5).subscribe({
      next: (p) => {
        this.data.set(p);
        this.loading.set(false);
        if (p.challenges.length === 0) {
          this.error.set("Sem perguntas geradas para este conteúdo ainda.");
        }
      },
      error: (e) => {
        this.error.set(
          e?.status === 404
            ? "Conteúdo não encontrado."
            : "Falha ao carregar perguntas.",
        );
        this.loading.set(false);
      },
    });
  }

  choose(index: number): void {
    const c = this.current();
    if (!c || this.answered()) return;
    const next = new Map(this.answers());
    next.set(c.id, { selectedIndex: index, isCorrect: index === c.correctIndex });
    this.answers.set(next);
  }

  selectedIndexOf(c: PoolChallenge): number | undefined {
    return this.answers().get(c.id)?.selectedIndex;
  }

  optionClass(c: PoolChallenge, i: number): string {
    if (!this.answered()) return "";
    if (i === c.correctIndex) return "option--correct";
    if (this.selectedIndexOf(c) === i) return "option--wrong";
    return "option--dimmed";
  }

  next(): void {
    if (this.currentIndex() < this.totalQuestions() - 1) {
      this.currentIndex.set(this.currentIndex() + 1);
    }
  }

  previous(): void {
    if (this.currentIndex() > 0) {
      this.currentIndex.set(this.currentIndex() - 1);
    }
  }

  finish(): void {
    this.router.navigate(["/dashboard"]);
  }
}
