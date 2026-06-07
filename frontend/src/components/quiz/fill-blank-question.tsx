import { useMemo } from "react"
import { cn } from "@/lib/utils"
import { InlineBlank, type InlineBlankState } from "./inline-blank"

/**
 * PR 34c — renderer pra perguntas com `shape === "FillInTheBlank"`.
 *
 * **Layout**: frase do prompt com `_____` substituído por chip inline
 * (`<InlineBlank />`), depois grid de pílulas com os termos disponíveis.
 * Visualmente diferente do MCQ (que tem cards empilhados full-width)
 * pra reforçar pra aluno que a tarefa é "qual termo encaixa", não
 * "qual alternativa explica melhor".
 *
 * **Props**:
 * - `prompt`        — frase com `_____` em algum lugar
 * - `options`       — termos candidatos (4)
 * - `selectedIndex` — índice escolhido ou `null` antes de responder
 * - `correctIndex`  — índice correto (visível após resposta)
 * - `disabled`      — bloqueia interação durante submit
 * - `onSelect(i)`   — callback ao clicar numa pílula
 *
 * **Estado da lacuna**:
 * - antes de responder: idle
 * - após acerto: correct (chip verde com termo escolhido)
 * - após erro:  wrong (chip vermelho + termo correto abaixo)
 *
 * **Acessibilidade**: lacuna tem `aria-label` descritivo; pílulas têm
 * `aria-pressed` quando selecionada e `aria-disabled` após resposta.
 */
export function FillBlankQuestion({
  prompt,
  options,
  selectedIndex,
  correctIndex,
  disabled = false,
  onSelect,
}: {
  prompt:        string
  options:       string[]
  selectedIndex: number | null
  /** Index correto. Quando `selectedIndex !== null`, usado pra mostrar
   * o termo correto na lacuna (caso aluno tenha errado). */
  correctIndex:  number
  disabled?:     boolean
  onSelect:      (index: number) => void
}) {
  const answered     = selectedIndex !== null
  const isCorrect    = answered && selectedIndex === correctIndex
  const selectedTerm = answered ? options[selectedIndex] ?? "" : null
  const correctTerm  = options[correctIndex] ?? ""

  const blankState: InlineBlankState = !answered
    ? "idle"
    : isCorrect
      ? "correct"
      : "wrong"

  // Quebra o prompt em pedaços ao redor de `_____` (5+ underscores).
  // Memoizado porque o split é puro do prompt — não muda entre cliques
  // do aluno; só re-roda quando muda de pergunta.
  const parts = useMemo(() => splitOnBlank(prompt), [prompt])

  return (
    <div className="space-y-5">
      {/* Frase com chip inline */}
      <div
        className={cn(
          "text-base leading-loose bg-popover/40 p-4 rounded-md border border-border",
          "font-sans",
        )}
      >
        {parts.map((part, i) => (
          <span key={i}>
            {part.text}
            {part.hasBlankAfter && (
              <InlineBlank
                state={blankState}
                term={selectedTerm}
                correctTerm={!isCorrect ? correctTerm : null}
              />
            )}
          </span>
        ))}
      </div>

      {/* Grid de pílulas — 2 colunas no desktop, 1 no mobile pra termos
          longos não vazarem */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
        {options.map((opt, i) => {
          const state = pillState(i, selectedIndex, correctIndex, answered)
          return (
            <button
              key={i}
              type="button"
              onClick={() => !disabled && !answered && onSelect(i)}
              disabled={disabled || answered}
              aria-pressed={selectedIndex === i}
              className={cn(
                "flex items-center gap-2 rounded-full border px-4 py-2 text-sm font-medium text-left transition-all",
                !answered && !disabled && "hover:border-primary hover:bg-primary/5",
                answered && "cursor-default",
                state === "correct" && "border-success bg-success/15 text-success",
                state === "wrong"   && "border-destructive bg-destructive/15 text-destructive",
                state === "muted"   && "opacity-40",
                state === "idle"    && "border-border bg-popover/40",
              )}
            >
              <span className="flex-shrink-0 h-6 w-6 rounded-full text-[10px] font-bold flex items-center justify-center bg-background text-primary border border-border">
                {["A", "B", "C", "D", "E", "F"][i]}
              </span>
              <span className="font-mono">{opt}</span>
            </button>
          )
        })}
      </div>
    </div>
  )
}

// ── helpers ────────────────────────────────────────────────────────

type Part = { text: string; hasBlankAfter: boolean }

/**
 * Divide o prompt em pedaços ao redor de `_____` (5+ underscores).
 * Cada pedaço é texto cru; entre dois pedaços vai um chip de lacuna.
 *
 * Backend (`BlankPlacementValidator`) garante exatamente 1 lacuna por
 * prompt fill-blank, então `parts.length` será ≤2 na prática. Mas o
 * código suporta múltiplas pra ser defensivo — se algum prompt antigo
 * escapar do validator com 2 lacunas, a UI renderiza coerente em vez
 * de quebrar.
 */
function splitOnBlank(prompt: string): Part[] {
  const parts: Part[] = []
  const re = /_{5,}/g
  let lastIdx = 0
  let m: RegExpExecArray | null

  while ((m = re.exec(prompt)) !== null) {
    parts.push({ text: prompt.slice(lastIdx, m.index), hasBlankAfter: true })
    lastIdx = m.index + m[0].length
  }
  parts.push({ text: prompt.slice(lastIdx), hasBlankAfter: false })

  return parts.length === 1 && !parts[0].text
    ? [{ text: "(pergunta sem lacuna)", hasBlankAfter: false }]
    : parts
}

type PillState = "idle" | "correct" | "wrong" | "muted"

function pillState(
  i:              number,
  selectedIndex:  number | null,
  correctIndex:   number,
  answered:       boolean,
): PillState {
  if (!answered) return "idle"
  if (i === correctIndex)   return "correct"
  if (i === selectedIndex)  return "wrong"
  return "muted"
}
