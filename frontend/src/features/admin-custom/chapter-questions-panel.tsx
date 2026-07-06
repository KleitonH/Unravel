import { useEffect, useMemo, useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import {
  AlertTriangle, Bot, CheckCircle2, ChevronDown, ChevronRight, Flag,
  Loader2, PenLine, Pencil, Sparkles, Trash2, X,
} from "lucide-react"
import { toast } from "sonner"
import { chaptersApi } from "@/api/chapters"
import { feedbackApi, FEEDBACK_REASON_LABELS } from "@/api/feedback"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { Textarea } from "@/components/ui/textarea"
import { cn } from "@/lib/utils"
import type { ContentQuestion } from "@/types/chapters"
import { GenerateQuestionsDialog } from "./generate-questions-dialog"

/**
 * PR 60-f — Gerenciador de perguntas organizado POR CAPÍTULO (H2).
 *
 * Substitui a lista plana do PR 56-a. Cada capítulo mostra seu readiness
 * (ex.: 2/4) e, quando está curto, oferece DUAS ações scoped àquele
 * capítulo específico:
 *
 * 1. **Gerar com IA** → `POST /admin/forge/{id}?chunkIndex=N` (extrai claims
 *    só daquele chunk; antes o top-N global podia nunca cobrir um capítulo
 *    fraco).
 * 2. **Escrever questão** → cria uma `GeneratedChallenge` ModeratorAuthored
 *    com `sourceChunkIndex=N` — conta no readiness e é servida ao aluno.
 *
 * Ambas fecham o gap do capítulo de verdade. Perguntas autorais são
 * editáveis; as geradas pela IA só podem ser desativadas (podar ruins).
 */
export function ChapterQuestionsPanel({
  contentId, contentTitle,
}: {
  contentId:    number
  contentTitle: string
}) {
  // genChunk: capítulo-alvo do modal de geração IA (null = fechado).
  const [genChunk, setGenChunk] = useState<{ index: number; title: string } | null>(null)
  // authoring: estado do modal de escrita manual.
  const [authoring, setAuthoring] = useState<
    | { chunkIndex: number; chapterTitle: string; editing?: ContentQuestion }
    | null
  >(null)

  const readinessQuery = useQuery({
    queryKey: ["admin", "publication-readiness", contentId],
    queryFn:  () => chaptersApi.readiness(contentId),
    refetchOnWindowFocus: true,
    refetchInterval: 30_000, // pega perguntas que chegaram do worker
  })

  const questionsQuery = useQuery({
    queryKey: ["admin", "content-questions", contentId],
    queryFn:  () => chaptersApi.listQuestions(contentId),
    refetchOnWindowFocus: true,
    refetchInterval: 30_000,
  })

  // Agrupa perguntas por chunkIndex
  const byChunk = useMemo(() => {
    const map = new Map<number, ContentQuestion[]>()
    for (const q of questionsQuery.data ?? []) {
      const arr = map.get(q.chunkIndex) ?? []
      arr.push(q)
      map.set(q.chunkIndex, arr)
    }
    return map
  }, [questionsQuery.data])

  const chapters = readinessQuery.data?.chapters ?? []

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center justify-between gap-2">
          <span>Perguntas por capítulo</span>
          {readinessQuery.data && (
            readinessQuery.data.ready ? (
              <Badge variant="outline" className="text-[10px] border-success/40 text-success gap-1">
                <CheckCircle2 className="h-3 w-3" /> Pronto pra publicar
              </Badge>
            ) : (
              <Badge variant="outline" className="text-[10px] border-warning/40 text-warning gap-1">
                <AlertTriangle className="h-3 w-3" />
                {chapters.filter((c) => !c.ready).length} capítulo(s) incompleto(s)
              </Badge>
            )
          )}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {readinessQuery.isLoading && (
          <div className="space-y-2">
            {[1, 2, 3].map((i) => <Skeleton key={i} className="h-20" />)}
          </div>
        )}

        {readinessQuery.data && chapters.length === 0 && (
          <p className="text-xs text-muted-foreground">
            Este conteúdo não tem capítulos. Use títulos <code>##</code> (H2) no
            markdown pra demarcar capítulos — cada um precisa de pelo menos{" "}
            {readinessQuery.data.minRequiredPerChapter} perguntas pra publicar.
          </p>
        )}

        {chapters.map((ch) => {
          const questions = byChunk.get(ch.chunkIndex) ?? []
          return (
            <ChapterBlock
              key={ch.chunkIndex}
              contentId={contentId}
              chunkIndex={ch.chunkIndex}
              title={ch.title}
              current={ch.current}
              required={ch.required}
              ready={ch.ready}
              questions={questions}
              onGenerate={() => setGenChunk({ index: ch.chunkIndex, title: ch.title })}
              onAuthor={() => setAuthoring({ chunkIndex: ch.chunkIndex, chapterTitle: ch.title })}
              onEdit={(q) => setAuthoring({ chunkIndex: ch.chunkIndex, chapterTitle: ch.title, editing: q })}
            />
          )
        })}
      </CardContent>

      {/* Geração IA scoped ao capítulo */}
      {genChunk && (
        <GenerateQuestionsDialog
          open
          onClose={() => setGenChunk(null)}
          scope="content"
          scopeId={contentId}
          scopeName={`${contentTitle} · cap. "${genChunk.title}"`}
          chunkIndex={genChunk.index}
        />
      )}

      {/* Escrita manual scoped ao capítulo */}
      {authoring && (
        <AuthorQuestionDialog
          open
          onClose={() => setAuthoring(null)}
          contentId={contentId}
          chunkIndex={authoring.chunkIndex}
          chapterTitle={authoring.chapterTitle}
          editing={authoring.editing}
        />
      )}
    </Card>
  )
}

function ChapterBlock({
  contentId, chunkIndex, title, current, required, ready, questions,
  onGenerate, onAuthor, onEdit,
}: {
  contentId:  number
  chunkIndex: number
  title:      string
  current:    number
  required:   number
  ready:      boolean
  questions:  ContentQuestion[]
  onGenerate: () => void
  onAuthor:   () => void
  onEdit:     (q: ContentQuestion) => void
}) {
  // Capítulos incompletos abrem por padrão (puxam atenção pro gap);
  // completos ficam recolhidos pra exibição mais limpa.
  const [open, setOpen] = useState(!ready)
  return (
    <div className={cn(
      "rounded-md border px-3 py-2.5",
      ready ? "border-border" : "border-warning/40 bg-warning/5",
    )}>
      <div className="flex items-center justify-between gap-2 flex-wrap">
        {/* Header clicável = toggle do accordion. Botões de ação ficam
            fora dessa área pra não disparar o toggle ao clicar neles. */}
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          className="flex items-center gap-2 min-w-0 flex-1 text-left rounded hover:bg-foreground/5 -mx-1 px-1 py-0.5"
          aria-expanded={open}
        >
          {open
            ? <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground" />
            : <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />}
          <span className="text-[10px] font-mono text-muted-foreground shrink-0">
            #{chunkIndex}
          </span>
          <span className="text-sm font-semibold truncate">{title}</span>
          <Badge variant="outline" className={cn(
            "text-[10px] tabular-nums shrink-0",
            ready ? "border-success/40 text-success" : "border-warning/40 text-warning",
          )}>
            {current}/{required}
          </Badge>
          {questions.length > 0 && (
            <span className="text-[10px] text-muted-foreground shrink-0">
              · {questions.length} no pool
            </span>
          )}
        </button>
        <div className="flex items-center gap-1.5 shrink-0">
          <Button size="sm" variant="outline" onClick={onGenerate} className="h-7 text-xs">
            <Sparkles className="h-3.5 w-3.5 mr-1" /> Gerar IA
          </Button>
          <Button size="sm" onClick={onAuthor} className="h-7 text-xs">
            <PenLine className="h-3.5 w-3.5 mr-1" /> Escrever
          </Button>
        </div>
      </div>

      {open && (
        questions.length > 0 ? (
          <ul className="mt-2 space-y-1">
            {questions.map((q) => (
              <QuestionRow
                key={q.id}
                contentId={contentId}
                q={q}
                onEdit={() => onEdit(q)}
              />
            ))}
          </ul>
        ) : (
          <p className="mt-2 text-[11px] text-muted-foreground italic">
            Nenhuma pergunta neste capítulo ainda — use “Gerar IA” ou “Escrever”.
          </p>
        )
      )}
    </div>
  )
}

function QuestionRow({
  contentId, q, onEdit,
}: {
  contentId: number
  q:         ContentQuestion
  onEdit:    () => void
}) {
  const qc = useQueryClient()
  const [feedbackOpen, setFeedbackOpen] = useState(false)
  const deleteMutation = useMutation({
    mutationFn: () => chaptersApi.deleteQuestion(contentId, q.id),
    onSuccess: () => {
      toast.success("Pergunta removida do pool.")
      qc.invalidateQueries({ queryKey: ["admin", "content-questions", contentId] })
      qc.invalidateQueries({ queryKey: ["admin", "publication-readiness", contentId] })
    },
    onError: () => toast.error("Falha ao remover."),
  })

  return (
    <li className={cn(
      "rounded border px-2.5 py-1.5",
      q.flagsOpen > 0 ? "border-warning/50 bg-warning/5" : "border-border/60 bg-popover/40",
    )}>
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground flex-wrap">
          {q.authored ? (
            <Badge variant="outline" className="text-[10px] border-primary/40 text-primary gap-1">
              <PenLine className="h-2.5 w-2.5" /> sua
            </Badge>
          ) : (
            <Badge variant="outline" className="text-[10px] border-success/40 text-success gap-1">
              <Bot className="h-2.5 w-2.5" /> IA
            </Badge>
          )}
          {q.shape === "FillInTheBlank" && (
            <Badge variant="outline" className="text-[10px]">🧩 fill-blank</Badge>
          )}
          {q.flagsOpen > 0 && (
            <button
              type="button"
              onClick={() => setFeedbackOpen(true)}
              className="inline-flex items-center gap-1 rounded-full border border-warning/50 bg-warning/10 px-1.5 py-0.5 text-[10px] font-semibold text-warning hover:bg-warning/20"
              title="Ver sinalizações de alunos"
            >
              <Flag className="h-2.5 w-2.5" />
              {q.flagsOpen} {q.flagsOpen === 1 ? "sinalização" : "sinalizações"}
            </button>
          )}
          <span>dif {Math.round(q.estimatedDifficulty * 100)}%</span>
        </div>
        <div className="flex items-center gap-0.5">
          {/* PR 60-f bis — editar qualquer pergunta (autoral OU IA). */}
          <Button size="icon" variant="ghost" className="h-6 w-6" onClick={onEdit}>
            <Pencil className="h-3 w-3" />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            className="h-6 w-6 text-destructive hover:bg-destructive/10 hover:text-destructive"
            onClick={() => {
              if (confirm("Remover esta pergunta do pool? (soft-delete)"))
                deleteMutation.mutate()
            }}
            disabled={deleteMutation.isPending}
          >
            {deleteMutation.isPending
              ? <Loader2 className="h-3 w-3 animate-spin" />
              : <Trash2 className="h-3 w-3" />}
          </Button>
        </div>
      </div>
      <p className="text-xs mt-0.5 line-clamp-2">{q.prompt}</p>
      <p className="text-[11px] text-success mt-0.5">✓ {q.options[q.correctIndex]}</p>

      {feedbackOpen && (
        <QuestionFeedbackDialog
          open
          onClose={() => setFeedbackOpen(false)}
          contentId={contentId}
          challengeId={q.id}
          prompt={q.prompt}
        />
      )}
    </li>
  )
}

/**
 * Histórico de bandeirinhas de uma pergunta. Lista quem sinalizou, o tipo
 * do problema e o comentário; permite ao moderador triar cada uma como
 * "Revisado" (agiu) ou "Descartado" (improcedente). Resolver uma atualiza
 * o badge de contagem na lista de perguntas.
 */
function QuestionFeedbackDialog({
  open, onClose, contentId, challengeId, prompt,
}: {
  open:        boolean
  onClose:     () => void
  contentId:   number
  challengeId: number
  prompt:      string
}) {
  const qc = useQueryClient()
  const feedbackQuery = useQuery({
    queryKey: ["admin", "question-feedback", challengeId],
    queryFn:  () => feedbackApi.listForQuestion(challengeId),
  })

  const resolveMutation = useMutation({
    mutationFn: ({ id, status }: { id: number; status: 1 | 2 }) =>
      feedbackApi.resolve(id, status),
    onSuccess: () => {
      toast.success("Sinalização triada.")
      qc.invalidateQueries({ queryKey: ["admin", "question-feedback", challengeId] })
      // atualiza o badge de contagem na lista de perguntas
      qc.invalidateQueries({ queryKey: ["admin", "content-questions", contentId] })
    },
    onError: () => toast.error("Falha ao triar."),
  })

  const items = feedbackQuery.data ?? []

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Flag className="h-5 w-5 text-warning" />
            Sinalizações de alunos
          </DialogTitle>
          <DialogDescription className="line-clamp-2">{prompt}</DialogDescription>
        </DialogHeader>

        {feedbackQuery.isLoading && (
          <div className="space-y-2">
            {[1, 2].map((i) => <Skeleton key={i} className="h-16" />)}
          </div>
        )}

        {feedbackQuery.data && items.length === 0 && (
          <p className="text-sm text-muted-foreground py-4 text-center">
            Nenhuma sinalização para esta pergunta.
          </p>
        )}

        {items.length > 0 && (
          <ul className="space-y-2 max-h-[50vh] overflow-y-auto">
            {items.map((f) => {
              const open = f.status === "Aberto"
              return (
                <li
                  key={f.id}
                  className={cn(
                    "rounded-md border px-3 py-2 text-sm",
                    open ? "border-warning/40 bg-warning/5" : "border-border/60 opacity-70",
                  )}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium">
                      {FEEDBACK_REASON_LABELS[f.reason] ?? f.reason}
                    </span>
                    <Badge
                      variant="outline"
                      className={cn(
                        "text-[10px]",
                        f.status === "Aberto"    && "border-warning/50 text-warning",
                        f.status === "Revisado"  && "border-success/50 text-success",
                        f.status === "Descartado" && "border-muted-foreground/40 text-muted-foreground",
                      )}
                    >
                      {f.status}
                    </Badge>
                  </div>
                  {f.comment && (
                    <p className="text-xs text-muted-foreground mt-1 italic">“{f.comment}”</p>
                  )}
                  <p className="text-[10px] text-muted-foreground mt-1">
                    {f.studentName} · {new Date(f.createdAt).toLocaleDateString("pt-BR")}
                  </p>
                  {open && (
                    <div className="flex gap-1.5 mt-2">
                      <Button
                        size="sm"
                        className="h-7 text-xs"
                        disabled={resolveMutation.isPending}
                        onClick={() => resolveMutation.mutate({ id: f.id, status: 1 })}
                      >
                        <CheckCircle2 className="h-3.5 w-3.5 mr-1" /> Marcar revisado
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7 text-xs"
                        disabled={resolveMutation.isPending}
                        onClick={() => resolveMutation.mutate({ id: f.id, status: 2 })}
                      >
                        <X className="h-3.5 w-3.5 mr-1" /> Descartar
                      </Button>
                    </div>
                  )}
                </li>
              )
            })}
          </ul>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Fechar</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/**
 * Modal pra escrever/editar uma pergunta autoral (MCQ) amarrada a um
 * capítulo. Mesma validação do gold (PR 56-a): prompt ≥15, 4 opções
 * distintas. Cria/edita via `chaptersApi` → vira GeneratedChallenge real.
 */
function AuthorQuestionDialog({
  open, onClose, contentId, chunkIndex, chapterTitle, editing,
}: {
  open:         boolean
  onClose:      () => void
  contentId:    number
  chunkIndex:   number
  chapterTitle: string
  editing?:     ContentQuestion
}) {
  const qc = useQueryClient()
  const [prompt, setPrompt]               = useState("")
  const [correctAnswer, setCorrectAnswer] = useState("")
  const [distractors, setDistractors]     = useState<string[]>(["", "", ""])
  const [explanation, setExplanation]     = useState("")

  useEffect(() => {
    if (!open) return
    if (editing) {
      setPrompt(editing.prompt)
      setCorrectAnswer(editing.options[editing.correctIndex] ?? "")
      setDistractors(editing.options.filter((_, i) => i !== editing.correctIndex).slice(0, 3))
      setExplanation(editing.explanation ?? "")
    } else {
      setPrompt("")
      setCorrectAnswer("")
      setDistractors(["", "", ""])
      setExplanation("")
    }
  }, [open, editing])

  const trimmedDistr = distractors.map((d) => d.trim())
  const allOptions   = [correctAnswer.trim(), ...trimmedDistr]
  const allFilled    = allOptions.every((o) => o.length > 0)
  const allDistinct  = new Set(allOptions.map((o) => o.toLowerCase())).size === 4
  const promptOk     = prompt.trim().length >= 15
  const canSubmit    = promptOk && allFilled && allDistinct

  const mutation = useMutation({
    mutationFn: () => {
      const body = {
        chunkIndex,
        prompt:        prompt.trim(),
        correctAnswer: correctAnswer.trim(),
        distractors:   trimmedDistr,
        explanation:   explanation.trim() || null,
      }
      return editing
        ? chaptersApi.updateQuestion(contentId, editing.id, body)
        : chaptersApi.createQuestion(contentId, body)
    },
    onSuccess: () => {
      toast.success(editing ? "Pergunta atualizada." : "Pergunta adicionada ao capítulo.")
      qc.invalidateQueries({ queryKey: ["admin", "content-questions", contentId] })
      qc.invalidateQueries({ queryKey: ["admin", "publication-readiness", contentId] })
      onClose()
    },
    onError: (e: any) => toast.error(e?.response?.data?.message ?? "Falha ao salvar."),
  })

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <PenLine className="h-5 w-5 text-primary" />
            {!editing
              ? "Escrever pergunta"
              : editing.authored ? "Editar sua pergunta" : "Editar pergunta da IA"}
          </DialogTitle>
          <DialogDescription>
            Capítulo <strong>“{chapterTitle}”</strong>. 4 opções (1 correta + 3
            distratores). Vai pro quiz do aluno e conta no readiness deste capítulo.
            {editing && !editing.authored && (
              <span className="block mt-1 text-[11px]">
                Editando uma pergunta gerada pela IA — corrigir o texto mantém a
                origem (badge “IA”), só muda o conteúdo.
              </span>
            )}
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={(e) => { e.preventDefault(); if (canSubmit && !mutation.isPending) mutation.mutate() }}
          className="space-y-3"
        >
          <Field label="Enunciado" hint="A pergunta que o aluno vê. Forma indireta — não revele a resposta.">
            <Textarea
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              rows={3}
              placeholder="Ex: Qual o papel do mecanismo X quando o sistema precisa de Y?"
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
              placeholder="Ex: Coordena o trabalho em unidades interrompíveis"
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
              placeholder="Por que a correta é correta (1 frase, referência ao conteúdo)..."
            />
          </Field>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>Cancelar</Button>
            <Button type="submit" disabled={!canSubmit || mutation.isPending}>
              {mutation.isPending
                ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Salvando…</>
                : editing ? "Salvar mudanças" : "Adicionar ao capítulo"}
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
