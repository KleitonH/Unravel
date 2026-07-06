import { useEffect, useState } from "react"
import { useMutation } from "@tanstack/react-query"
import { Check, Flag, Loader2 } from "lucide-react"
import { toast } from "sonner"
import { feedbackApi, FEEDBACK_REASONS } from "@/api/feedback"
import { Button } from "@/components/ui/button"
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog"
import { Textarea } from "@/components/ui/textarea"
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"

/**
 * "Bandeirinha" do quiz — o aluno reporta uma pergunta inadequada.
 *
 * Ícone discreto no canto do card da pergunta. Ao clicar, abre um diálogo
 * pra escolher o TIPO do problema (gabarito errado / ambígua / múltipla
 * correta / fora do conteúdo / outro) e, opcionalmente, comentar. O
 * feedback vai pra fila de moderação (não altera a pergunta na hora).
 *
 * Otimista quanto ao "já reportei": após enviar, marca localmente como
 * sinalizada e desabilita — evita reenvio no mesmo card. O backend também
 * é idempotente por (pergunta, aluno).
 */
export function FlagQuestionButton({
  challengeId,
  className,
}: {
  challengeId: number
  className?:  string
}) {
  const [open, setOpen]     = useState(false)
  const [flagged, setFlagged] = useState(false)
  const [reason, setReason] = useState<number | null>(null)
  const [comment, setComment] = useState("")

  // Reseta a seleção sempre que reabre (novo report do zero).
  useEffect(() => {
    if (open) { setReason(null); setComment("") }
  }, [open])

  const needsComment = reason === 4 // "Outro"
  const canSubmit    = reason !== null && (!needsComment || comment.trim().length > 0)

  const mutation = useMutation({
    mutationFn: () =>
      feedbackApi.submit(challengeId, {
        reason: reason!,
        comment: comment.trim() || null,
      }),
    onSuccess: () => {
      setFlagged(true)
      setOpen(false)
      toast.success("Obrigado! Sua sinalização foi enviada pra revisão.")
    },
    onError: (e: any) =>
      toast.error(e?.response?.data?.message ?? "Não foi possível enviar a sinalização."),
  })

  return (
    <>
      <TooltipProvider delayDuration={200}>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              disabled={flagged}
              onClick={() => setOpen(true)}
              aria-label={flagged ? "Pergunta sinalizada" : "Sinalizar problema na pergunta"}
              className={cn(
                "h-7 w-7 text-muted-foreground hover:text-warning",
                flagged && "text-warning",
                className,
              )}
            >
              {flagged
                ? <Check className="h-4 w-4" />
                : <Flag className="h-4 w-4" />}
            </Button>
          </TooltipTrigger>
          <TooltipContent side="left">
            {flagged ? "Você sinalizou esta pergunta" : "Achou algo errado? Sinalize"}
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Flag className="h-5 w-5 text-warning" />
              Sinalizar problema
            </DialogTitle>
            <DialogDescription>
              O que há de errado nesta pergunta? Sua sinalização vai pra revisão
              — não muda a pergunta agora.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-1.5">
            {FEEDBACK_REASONS.map((r) => (
              <button
                key={r.value}
                type="button"
                onClick={() => setReason(r.value)}
                className={cn(
                  "w-full text-left rounded-md border px-3 py-2 transition-colors",
                  reason === r.value
                    ? "border-warning bg-warning/10"
                    : "border-border hover:bg-foreground/5",
                )}
                aria-pressed={reason === r.value}
              >
                <span className="text-sm font-medium flex items-center gap-2">
                  <span className={cn(
                    "h-3.5 w-3.5 rounded-full border shrink-0 grid place-items-center",
                    reason === r.value ? "border-warning" : "border-muted-foreground/40",
                  )}>
                    {reason === r.value && <span className="h-1.5 w-1.5 rounded-full bg-warning" />}
                  </span>
                  {r.label}
                </span>
                <span className="text-[11px] text-muted-foreground block ml-[1.375rem]">
                  {r.hint}
                </span>
              </button>
            ))}
          </div>

          <div className="space-y-1">
            <Textarea
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              rows={2}
              maxLength={1000}
              placeholder={needsComment
                ? "Descreva o problema (obrigatório)…"
                : "Comentário (opcional)…"}
            />
            {needsComment && comment.trim().length === 0 && (
              <p className="text-[11px] text-destructive">Comentário obrigatório para “Outro”.</p>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancelar
            </Button>
            <Button
              type="button"
              disabled={!canSubmit || mutation.isPending}
              onClick={() => mutation.mutate()}
            >
              {mutation.isPending
                ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Enviando…</>
                : "Enviar sinalização"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
