import { cn } from "@/lib/utils"

/**
 * PR 34c — chip inline que substitui `_____` numa frase fill-blank.
 * Componente puramente visual (sem estado próprio); recebe estado via
 * props pra ser reaproveitado em quiz/boss/reinforce/futuras telas.
 *
 * Estados visuais:
 * - `idle`      → dashed border + cursor piscando, mensagem "?"
 * - `selected`  → preenchido com termo, bg-primary/15
 * - `correct`   → bg-success/20, ícone check, termo destacado
 * - `wrong`     → bg-destructive/20, ícone x, termo riscado; abaixo
 *                 aparece o termo correto em verde
 *
 * **Responsivo**: o chip cresce com o termo (max-width: 16ch pra evitar
 * overflow em frases longas; quebra de linha natural quando ultrapassa).
 */
export type InlineBlankState = "idle" | "selected" | "correct" | "wrong"

export function InlineBlank({
  state,
  term,
  correctTerm,
}: {
  state:        InlineBlankState
  /** Termo que o aluno selecionou (se houver). */
  term?:        string | null
  /** Termo correto — usado pra mostrar abaixo quando state="wrong". */
  correctTerm?: string | null
}) {
  if (state === "idle") {
    return (
      <span
        className={cn(
          "inline-flex items-center justify-center align-middle mx-1",
          "min-w-[6ch] px-2 py-0.5 rounded-md",
          "border-2 border-dashed border-primary/60",
          "bg-primary/5 text-primary/70 text-sm font-mono",
        )}
        aria-label="lacuna pra preencher"
      >
        <span className="animate-pulse">?</span>
      </span>
    )
  }

  if (state === "selected") {
    return (
      <span
        className={cn(
          "inline-flex items-center align-middle mx-1",
          "px-2 py-0.5 rounded-md",
          "border border-primary bg-primary/15 text-primary font-semibold",
          "animate-pop-in",
        )}
      >
        {term}
      </span>
    )
  }

  if (state === "correct") {
    return (
      <span
        className={cn(
          "inline-flex items-center align-middle mx-1 gap-1",
          "px-2 py-0.5 rounded-md",
          "border border-success bg-success/20 text-success font-semibold",
          "animate-pop-in",
        )}
      >
        <CheckIcon /> {term}
      </span>
    )
  }

  // wrong
  return (
    <span className="inline-flex flex-col items-start align-middle mx-1">
      <span
        className={cn(
          "inline-flex items-center gap-1 px-2 py-0.5 rounded-md",
          "border border-destructive bg-destructive/20 text-destructive font-semibold line-through",
          "animate-pop-in",
        )}
      >
        <XIcon /> {term}
      </span>
      {correctTerm && (
        <span className="inline-flex items-center gap-1 mt-1 px-2 py-0.5 rounded-md border border-success bg-success/10 text-success text-xs font-medium">
          <CheckIcon /> {correctTerm}
        </span>
      )}
    </span>
  )
}

// Ícones inline pra evitar dependência circular do lucide nesse componente
// genérico (mantém-se reusável em contextos sem provider).
function CheckIcon() {
  return (
    <svg className="h-3 w-3" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="3 8 6.5 11.5 13 5" />
    </svg>
  )
}

function XIcon() {
  return (
    <svg className="h-3 w-3" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <line x1="4" y1="4" x2="12" y2="12" />
      <line x1="12" y1="4" x2="4" y2="12" />
    </svg>
  )
}
