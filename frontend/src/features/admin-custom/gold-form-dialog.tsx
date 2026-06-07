import { useEffect, useState } from "react"
import { useMutation, useQueryClient } from "@tanstack/react-query"
import { Loader2, Sparkles, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { moderatorGoldApi } from "@/api/moderator-gold"
import { Button } from "@/components/ui/button"
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { cn } from "@/lib/utils"
import type { ModeratorGoldItem } from "@/types/gold"

/**
 * PR 56-a — Modal pra criar/editar item de gold curado pelo moderador.
 *
 * **MVP**: somente MCQ (4 opções, 1 correta, 3 distratores). FillBlank
 * fica pro 56-c — backend gold já tolera ambos via prompt com `_____`
 * mas a UI fica mais complexa de explicar e fica fora desse passo.
 *
 * **Modos**:
 * - `mode="create"` + `contentId` → POST /admin/gold/{contentId}
 * - `mode="edit"` + `item` → PUT /admin/gold/{goldId}
 *
 * **Validações client-side**:
 * - prompt: ≥15 chars
 * - correctAnswer: ≥1 char não-vazio
 * - distratores: exatamente 3 não-vazios
 * - todas 4 opções distintas case-insensitive
 */
export function GoldFormDialog({
  open, onClose, mode, contentId, item,
}: {
  open:       boolean
  onClose:    () => void
  mode:       "create" | "edit"
  /** Obrigatório em "create". Ignorado em "edit" (item já tem contentId). */
  contentId?: number
  /** Obrigatório em "edit". */
  item?:      ModeratorGoldItem
}) {
  const qc = useQueryClient()

  // Estado do form: 4 strings indexáveis (0=correct, 1-3=distratores).
  // Persistir a posição da correta separadamente daria flexibilidade
  // de reordenar, mas pra MVP mais simples assumir posição fixa.
  const [prompt,        setPrompt]        = useState("")
  const [correctAnswer, setCorrectAnswer] = useState("")
  const [distractors,   setDistractors]   = useState<string[]>(["", "", ""])
  const [explanation,   setExplanation]   = useState("")
  const [sourceClaim,   setSourceClaim]   = useState("")

  // Reset/load on open OR mode change
  useEffect(() => {
    if (!open) return
    if (mode === "edit" && item) {
      setPrompt(item.prompt)
      setCorrectAnswer(item.correctAnswer)
      try {
        const dArr = JSON.parse(item.distractorsJson) as string[]
        setDistractors(dArr.length === 3 ? dArr : ["", "", ""])
      } catch { setDistractors(["", "", ""]) }
      setExplanation(item.explanation ?? "")
      setSourceClaim(item.sourceClaim ?? "")
    } else {
      setPrompt("")
      setCorrectAnswer("")
      setDistractors(["", "", ""])
      setExplanation("")
      setSourceClaim("")
    }
  }, [open, mode, item])

  // Validation
  const trimmedDistr = distractors.map((d) => d.trim())
  const allOptions   = [correctAnswer.trim(), ...trimmedDistr]
  const allFilled    = allOptions.every((o) => o.length > 0)
  const allDistinct  = new Set(allOptions.map((o) => o.toLowerCase())).size === 4
  const promptOk     = prompt.trim().length >= 15
  const canSubmit    = promptOk && allFilled && allDistinct

  const mutation = useMutation({
    mutationFn: async () => {
      const body = {
        prompt:        prompt.trim(),
        correctAnswer: correctAnswer.trim(),
        distractors:   trimmedDistr,
        explanation:   explanation.trim() || undefined,
        sourceClaim:   sourceClaim.trim() || undefined,
      }
      if (mode === "edit" && item) {
        return moderatorGoldApi.update(item.id, body)
      }
      if (!contentId) throw new Error("contentId obrigatório em create")
      return moderatorGoldApi.create(contentId, body)
    },
    onSuccess: () => {
      toast.success(mode === "edit" ? "Pergunta atualizada." : "Pergunta criada.")
      // Invalida lista de gold do content; manager re-fetch
      qc.invalidateQueries({ queryKey: ["admin", "gold"] })
      onClose()
    },
    onError: (e: any) => {
      const msg = e?.response?.data?.message ?? "Falha ao salvar."
      toast.error(msg)
    },
  })

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Sparkles className="h-5 w-5 text-warning" />
            {mode === "edit" ? "Editar pergunta" : "Nova pergunta curada"}
          </DialogTitle>
          <DialogDescription>
            {mode === "edit"
              ? "Ajuste prompt, opções ou explicação. Source e curador não mudam."
              : "Pergunta MCQ com 4 opções (1 correta + 3 distratores). Aparece no quiz como qualquer outra."}
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={(e) => { e.preventDefault(); if (canSubmit && !mutation.isPending) mutation.mutate() }}
          className="space-y-3"
        >
          <Field
            label="Prompt"
            hint="A pergunta que o aluno vê. Use forma indireta — não revele a resposta no enunciado."
          >
            <Textarea
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              rows={3}
              placeholder="Ex: Qual a função do decorator @Component em Angular?"
              autoFocus
            />
            {prompt.trim().length > 0 && !promptOk && (
              <p className="text-[11px] text-destructive mt-1">Mínimo 15 caracteres.</p>
            )}
          </Field>

          <Field label="Resposta correta">
            <Input
              value={correctAnswer}
              onChange={(e) => setCorrectAnswer(e.target.value)}
              placeholder="Ex: Marca a classe como componente Angular"
              className={cn(correctAnswer.trim() && "border-success/40 bg-success/5")}
            />
          </Field>

          <Field label="Distratores (3)">
            <div className="space-y-1.5">
              {[0, 1, 2].map((i) => (
                <div key={i} className="flex items-center gap-2">
                  <span className="text-[10px] font-mono text-muted-foreground w-4 text-center">
                    {["B", "C", "D"][i]}
                  </span>
                  <Input
                    value={distractors[i]}
                    onChange={(e) => {
                      const next = [...distractors]
                      next[i] = e.target.value
                      setDistractors(next)
                    }}
                    placeholder={`Alternativa errada plausível ${i + 1}`}
                  />
                </div>
              ))}
            </div>
            {allFilled && !allDistinct && (
              <p className="text-[11px] text-destructive mt-1">As 4 opções devem ser distintas.</p>
            )}
          </Field>

          <Field label="Explicação" hint="Opcional — mostrada após o aluno responder.">
            <Textarea
              value={explanation}
              onChange={(e) => setExplanation(e.target.value)}
              rows={2}
              placeholder="Por que a resposta correta é correta (1 frase, referência ao conteúdo)..."
            />
          </Field>

          <Field label="Claim fonte (opcional)" hint="Trecho específico do conteúdo que essa pergunta avalia.">
            <Input
              value={sourceClaim}
              onChange={(e) => setSourceClaim(e.target.value)}
              placeholder="Ex: O decorator @Component define metadados da classe"
            />
          </Field>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancelar
            </Button>
            <Button type="submit" disabled={!canSubmit || mutation.isPending}>
              {mutation.isPending
                ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Salvando…</>
                : mode === "edit" ? "Salvar mudanças" : "Criar pergunta"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function Field({
  label, hint, children,
}: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <Label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
        {label}
      </Label>
      {children}
      {hint && <p className="text-[11px] text-muted-foreground">{hint}</p>}
    </div>
  )
}

/**
 * Botão helper de delete com confirm — exportado pra reuso na lista
 * de gold (manager).
 */
export function DeleteGoldButton({
  goldId, onDeleted,
}: {
  goldId: number
  onDeleted?: () => void
}) {
  const qc = useQueryClient()
  const mutation = useMutation({
    mutationFn: () => moderatorGoldApi.remove(goldId),
    onSuccess: () => {
      toast.success("Pergunta removida.")
      qc.invalidateQueries({ queryKey: ["admin", "gold"] })
      onDeleted?.()
    },
    onError: () => toast.error("Falha ao remover."),
  })
  return (
    <Button
      size="icon"
      variant="ghost"
      className="text-destructive hover:bg-destructive/10 hover:text-destructive"
      onClick={() => {
        if (confirm("Remover esta pergunta? (soft-delete — pode ser recuperada via DB)"))
          mutation.mutate()
      }}
      disabled={mutation.isPending}
    >
      {mutation.isPending
        ? <Loader2 className="h-4 w-4 animate-spin" />
        : <Trash2 className="h-4 w-4" />}
    </Button>
  )
}
