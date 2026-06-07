import { FillBlankQuestion } from "./fill-blank-question"
import { MultipleChoiceQuestion, type RenderMode } from "./multiple-choice-question"
import type { QuestionShape } from "@/types/api"

export type { RenderMode }

/**
 * PR 34c — dispatcher de renderers de pergunta baseado em `shape`.
 *
 * Ponto único de integração pra todas as quiz pages
 * (quiz-page, adaptive-quiz-page, boss-fight-page, reinforce-page).
 * Cada caller mantém seu próprio state machine (selected, submitting,
 * feedback rewards) — esse componente só renderiza o "miolo" da pergunta.
 *
 * **Por que dispatcher e não polimorfismo via props**: cada shape tem
 * layout suficientemente diferente (cards vs pílulas, prompt como bloco
 * vs frase com chip inline) que mesclar num único componente com flags
 * vira lasanha. Componentes separados, dispatcher fino.
 *
 * **Fallback**: shape desconhecido (versão futura do backend) cai em
 * MCQ — UI nunca quebra; pior caso é layout não-ideal pra shape novo.
 */
export function QuestionRenderer({
  shape,
  prompt,
  options,
  selectedIndex,
  correctIndex,
  disabled = false,
  mode = "review",
  onSelect,
}: {
  shape?:        QuestionShape   // undefined = "MultipleChoice" (retrocompat)
  prompt:        string
  options:       string[]
  selectedIndex: number | null
  correctIndex:  number
  disabled?:     boolean
  /** Modo de exibição. `review` (default) revela gabarito após escolha;
   * `select-only` só destaca a seleção (boss fight). FillBlank ignora
   * por enquanto — sempre revela; boss fight não emite FillBlank no
   * pool atual, então não há caso de uso real ainda. */
  mode?:         RenderMode
  onSelect:      (index: number) => void
}) {
  switch (shape) {
    case "FillInTheBlank":
      return (
        <FillBlankQuestion
          prompt={prompt}
          options={options}
          selectedIndex={selectedIndex}
          correctIndex={correctIndex}
          disabled={disabled}
          onSelect={onSelect}
        />
      )
    case "MultipleChoice":
    case "TrueFalseGrounded":   // reservado; fallback MCQ até PR 34a-bis
    case undefined:
    default:
      return (
        <MultipleChoiceQuestion
          prompt={prompt}
          options={options}
          selectedIndex={selectedIndex}
          correctIndex={correctIndex}
          disabled={disabled}
          mode={mode}
          onSelect={onSelect}
        />
      )
  }
}
