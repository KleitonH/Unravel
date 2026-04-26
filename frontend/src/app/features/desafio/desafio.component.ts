import { Component, inject, signal, computed } from "@angular/core";
import { Router } from "@angular/router";
import { CommonModule } from "@angular/common";

interface Question {
  id: number;
  text: string;
  options: string[];
  correct: number;
}

type NaviMood = "neutral" | "happy" | "sad";

const QUESTIONS: Question[] = [
  {
    id: 1,
    text: "Qual propriedade CSS define as colunas de um Grid Container?",
    options: [
      "grid-template-columns",
      "grid-columns",
      "column-template",
      "grid-column-count",
    ],
    correct: 0,
  },
  {
    id: 2,
    text: "Como você faz um item do Grid ocupar 2 colunas?",
    options: [
      "grid-column: span 2",
      "column-span: 2",
      "grid-span: 2",
      "colspan: 2",
    ],
    correct: 0,
  },
  {
    id: 3,
    text: 'Qual valor de "display" ativa o CSS Grid?',
    options: [
      "display: flex",
      "display: block",
      "display: grid",
      "display: inline-grid-container",
    ],
    correct: 2,
  },
];

@Component({
  selector: "app-desafio",
  standalone: true,
  imports: [CommonModule],
  templateUrl: "./desafio.component.html",
  styleUrl: "./desafio.component.scss",
})
export class DesafioComponent {
  private readonly router = inject(Router);

  readonly questions = QUESTIONS;
  readonly currentIndex = signal(0);
  readonly selectedOption = signal<number | null>(null);
  readonly confirmed = signal(false);
  readonly correctCount = signal(0);
  readonly finished = signal(false);
  readonly xp = signal(0);

  readonly currentQuestion = computed(
    () => this.questions[this.currentIndex()],
  );
  readonly isLast = computed(
    () => this.currentIndex() === this.questions.length - 1,
  );

  readonly naviMood = computed((): NaviMood => {
    if (!this.confirmed()) return "neutral";
    return this.selectedOption() === this.currentQuestion().correct
      ? "happy"
      : "sad";
  });

  readonly naviEmoji = computed(() => {
    switch (this.naviMood()) {
      case "happy":
        return "😸";
      case "sad":
        return "😿";
      default:
        return "🐱";
    }
  });

  readonly naviMessage = computed(() => {
    switch (this.naviMood()) {
      case "happy":
        return "Correto! Arrasou! 🎉";
      case "sad":
        return "Ops! Não foi dessa vez... 💪";
      default:
        return "Pense bem antes de responder!";
    }
  });

  select(index: number): void {
    if (this.confirmed()) return;
    this.selectedOption.set(index);
  }

  confirm(): void {
    if (this.selectedOption() === null || this.confirmed()) return;
    this.confirmed.set(true);
    if (this.selectedOption() === this.currentQuestion().correct) {
      this.correctCount.update((n) => n + 1);
      this.xp.update((n) => n + 50);
    }
  }

  next(): void {
    if (this.isLast()) {
      this.finished.set(true);
    } else {
      this.currentIndex.update((i) => i + 1);
      this.selectedOption.set(null);
      this.confirmed.set(false);
    }
  }

  goBack(): void {
    this.router.navigate(["/dashboard"]);
  }

  optionClass(index: number): string {
    if (!this.confirmed()) {
      return this.selectedOption() === index ? "option--selected" : "";
    }
    if (index === this.currentQuestion().correct) return "option--correct";
    if (index === this.selectedOption()) return "option--wrong";
    return "";
  }
}
