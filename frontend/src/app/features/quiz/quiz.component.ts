import { Component, OnInit, computed, inject, signal } from "@angular/core";
import { CommonModule, DecimalPipe } from "@angular/common";
import { ActivatedRoute, Router } from "@angular/router";
import {
  ChallengePoolService,
  SubmitPoolChallengeResponse,
} from "../../core/services/challenge-pool.service";
import {
  ChallengePool,
  PoolChallenge,
} from "../../core/models/challenge-pool.model";
import { BottomNavComponent } from "../../shared/components/bottom-nav/bottom-nav.component";

type AnswerState = {
  selectedIndex: number;
  isCorrect: boolean;
  correctIndex: number;
  explanation: string | null;
  newMasteryScore: number;
  // PR 15 — ganhos exibidos abaixo do feedback
  xpEarned: number;
  coinsEarned: number;
  starsEarned: number;
  lifeDelta: number;
};

/**
 * Página /quiz/:contentId — carrega o pool gerado pelo Forge e apresenta
 * uma pergunta por vez. Ao escolher, submete ao backend
 * (POST /challenge-pool/submit, PR 13). O servidor é a fonte da verdade:
 * valida contra o gabarito persistido, atualiza Mastery do tópico e
 * devolve o resultado autoritativo, que a UI usa para feedback.
 *
 * O <c>correctIndex</c> ainda vem no GET por compatibilidade — mas o
 * submit é quem decide o acerto. Quando todos os clientes migrarem,
 * podemos parar de expor no GET (segurança em profundidade).
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
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  readonly currentIndex = signal(0);
  readonly answers = signal<Map<number, AnswerState>>(new Map());

  readonly current = computed<PoolChallenge | null>(() => {
    const d = this.data();
    if (!d) return null;
    return d.challenges[this.currentIndex()] ?? null;
  });

  readonly currentAnswer = computed<AnswerState | null>(() => {
    const c = this.current();
    return c ? this.answers().get(c.id) ?? null : null;
  });

  readonly answered = computed(() => this.currentAnswer() !== null);

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
    if (!c || this.answered() || this.submitting()) return;
    this.submitting.set(true);

    this.pool
      .submit(this.contentId(), {
        generatedChallengeId: c.id,
        selectedOptionIndex: index,
      })
      .subscribe({
        next: (r: SubmitPoolChallengeResponse) => {
          const next = new Map(this.answers());
          next.set(c.id, {
            selectedIndex: index,
            isCorrect: r.isCorrect,
            correctIndex: r.correctOptionIndex,
            explanation: r.explanation,
            newMasteryScore: r.newMasteryScore,
            xpEarned: r.xpEarned,
            coinsEarned: r.coinsEarned,
            starsEarned: r.starsEarned,
            lifeDelta: r.lifeDelta,
          });
          this.answers.set(next);
          this.submitting.set(false);
        },
        error: () => {
          // Fallback "offline": usa o gabarito local que veio no GET para
          // não travar o quiz se a submissão falhar transitoriamente. Os
          // ganhos não são contabilizados — trade-off consciente.
          const next = new Map(this.answers());
          next.set(c.id, {
            selectedIndex: index,
            isCorrect: index === c.correctIndex,
            correctIndex: c.correctIndex,
            explanation: c.explanation,
            newMasteryScore: this.data()?.targetUserMastery ?? 0,
            xpEarned: 0, coinsEarned: 0, starsEarned: 0, lifeDelta: 0,
          });
          this.answers.set(next);
          this.submitting.set(false);
        },
      });
  }

  selectedIndexOf(c: PoolChallenge): number | undefined {
    return this.answers().get(c.id)?.selectedIndex;
  }

  optionClass(c: PoolChallenge, i: number): string {
    const ans = this.answers().get(c.id);
    if (!ans) return "";
    if (i === ans.correctIndex) return "option--correct";
    if (ans.selectedIndex === i) return "option--wrong";
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
