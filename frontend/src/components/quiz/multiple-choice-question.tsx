import { cn } from "@/lib/utils"

/**
 * PR 34c — renderer pra perguntas com `shape === "MultipleChoice"`
 * (default histórico do quiz). Extraído do `quiz-page.tsx` sem mudança
 * funcional pra ser reaproveitado em adaptive/boss/reinforce/futuras.
 *
 * **Layout**: prompt num bloco destacado + lista vertical de cards de
 * opção full-width. Mantém o visual exato do PR 4 (familiar pro usuário).
 *
 * **Props** simétricas ao `<FillBlankQuestion />` pra permitir dispatch
 * polimórfico no `<QuestionRenderer />`.
 */
export type RenderMode =
  /** Revela gabarito após seleção (quiz/adaptive/reinforce). */
  | "review"
  /** Só destaca a seleção; gabarito nunca é mostrado (boss fight,
   *  onde o aluno responde sem feedback imediato e descobre o resultado
   *  só ao submeter o lote inteiro). */
  | "select-only"

export function MultipleChoiceQuestion({
  prompt,
  options,
  selectedIndex,
  correctIndex,
  disabled = false,
  mode = "review",
  onSelect,
}: {
  prompt:        string
  options:       string[]
  selectedIndex: number | null
  correctIndex:  number
  disabled?:     boolean
  mode?:         RenderMode
  onSelect:      (index: number) => void
}) {
  const answered = selectedIndex !== null

  return (
    <div className="space-y-4">
      <pre className="whitespace-pre-wrap font-sans text-base leading-relaxed bg-popover/40 p-4 rounded-md border border-border">
        {prompt}
      </pre>

      <ul className="space-y-2">
        {options.map((opt, i) => {
          const cls = mode === "select-only"
            ? selectOnlyClass(i, selectedIndex)
            : optionClass(i, selectedIndex, correctIndex, answered)
          return (
            <li key={i}>
              <button
                type="button"
                onClick={() => {
                  if (disabled) return
                  // No modo select-only o aluno pode trocar de opção até
                  // submeter. No review (default) trava após primeira escolha.
                  if (mode === "review" && answered) return
                  onSelect(i)
                }}
                disabled={disabled || (mode === "review" && answered)}
                aria-pressed={selectedIndex === i}
                className={cn(
                  "w-full flex items-center gap-3 rounded-md border px-3 py-3 text-sm text-left transition-all",
                  !disabled && (mode === "select-only" || !answered) && "hover:border-primary",
                  ((mode === "review" && answered) || disabled) && "cursor-default",
                  cls,
                  mode === "review" && !answered && "border-border bg-popover/40",
                  mode === "select-only" && selectedIndex !== i && "border-border bg-popover/40",
                )}
              >
                <span className={cn(
                  "flex-shrink-0 h-7 w-7 rounded-md text-xs font-bold flex items-center justify-center",
                  mode === "select-only" && selectedIndex === i
                    ? "bg-warning text-warning-foreground"
                    : "bg-background text-primary",
                )}>
                  {["A", "B", "C", "D", "E", "F"][i]}
                </span>
                <span>{opt}</span>
              </button>
            </li>
          )
        })}
      </ul>
    </div>
  )
}

function optionClass(
  i:             number,
  selectedIndex: number | null,
  correctIndex:  number,
  answered:      boolean,
): string {
  if (!answered) return ""
  if (i === correctIndex)  return "border-success bg-success/15"
  if (i === selectedIndex) return "border-destructive bg-destructive/15"
  return "opacity-40"
}

/** Mode select-only: só destaca a seleção em warning; nunca revela
 *  gabarito (usado no boss fight). */
function selectOnlyClass(i: number, selectedIndex: number | null): string {
  if (selectedIndex === null) return ""
  if (i === selectedIndex)    return "border-warning bg-warning/10"
  return ""
}
