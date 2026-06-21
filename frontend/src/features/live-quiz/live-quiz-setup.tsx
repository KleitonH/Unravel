import { useMemo, useState } from "react"
import { useQueries, useQuery } from "@tanstack/react-query"
import {
  Check, ChevronLeft, ChevronRight, Clock, Globe, ListChecks, ListOrdered,
  Radio, Shuffle, Ticket, Trophy, Users,
} from "lucide-react"
import { turmasApi } from "@/api/turmas"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import { LiveQuizQuestionPicker, type PickedQuestion } from "./live-quiz-question-picker"
import type { TurmaMember } from "@/types/api"

/**
 * Quiz ao Vivo — assistente de criação (vive numa aba da Curadoria).
 *
 * Construído em etapas. ETAPA 1 (esta): configuração — modo (turma × livre),
 * quantidade/tempo e opções, e (no modo turma) escolha de turmas + alunos
 * participantes. ETAPA 2 (próxima): seleção manual das perguntas.
 *
 * O estado do assistente fica aqui; a sessão só é criada no backend mais
 * adiante (ao iniciar), quando o token do modo livre é gerado.
 */

export type LiveQuizMode = "turma" | "livre"

export type LiveQuizConfig = {
  mode: LiveQuizMode
  questionCount: number
  secondsPerQuestion: number
  shuffleQuestions: boolean
  shuffleOptions: boolean
  showRankBetween: boolean
  turmaIds: number[]
  studentIds: string[] // participantes selecionados (modo turma)
}

export function LiveQuizSetup() {
  const [step, setStep] = useState<1 | 2 | 3>(1)
  const [picked, setPicked] = useState<PickedQuestion[]>([])

  const [mode, setMode] = useState<LiveQuizMode>("turma")
  const [questionCount, setQuestionCount] = useState(10)
  const [secondsPerQuestion, setSeconds] = useState(20)
  const [shuffleQuestions, setShuffleQuestions] = useState(true)
  const [shuffleOptions, setShuffleOptions] = useState(false)
  const [showRankBetween, setShowRankBetween] = useState(true)

  const [selectedTurmas, setSelectedTurmas] = useState<Set<number>>(new Set())
  // Alunos começam todos selecionados; guardamos só os DESmarcados.
  const [deselected, setDeselected] = useState<Set<string>>(new Set())

  const turmasQuery = useQuery({ queryKey: ["turmas", "owned"], queryFn: turmasApi.owned })

  // Detalhes das turmas selecionadas (pra listar os alunos ativos).
  const turmaIds = [...selectedTurmas]
  const detailQueries = useQueries({
    queries: turmaIds.map((id) => ({
      queryKey: ["turmas", "detail", id] as const,
      queryFn:  () => turmasApi.detail(id),
      enabled:  mode === "turma",
    })),
  })

  // Membros ativos agregados (dedupe por userId) das turmas selecionadas.
  const members = useMemo(() => {
    const map = new Map<string, TurmaMember>()
    for (const q of detailQueries) {
      for (const m of q.data?.members ?? []) {
        if (m.status === "active" && !map.has(m.userId)) map.set(m.userId, m)
      }
    }
    return [...map.values()].sort((a, b) => b.xp - a.xp)
  }, [detailQueries])

  const selectedStudentIds = members.map((m) => m.userId).filter((id) => !deselected.has(id))

  const toggleTurma = (id: number) =>
    setSelectedTurmas((prev) => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })

  const toggleStudent = (id: string) =>
    setDeselected((prev) => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })

  const turmaValid = mode === "livre" || (selectedTurmas.size > 0 && selectedStudentIds.length > 0)
  const settingsValid = questionCount >= 1 && questionCount <= 50 && secondsPerQuestion >= 5 && secondsPerQuestion <= 120
  const canAdvance = turmaValid && settingsValid

  const config: LiveQuizConfig = {
    mode, questionCount, secondsPerQuestion, shuffleQuestions, shuffleOptions, showRankBetween,
    turmaIds, studentIds: selectedStudentIds,
  }

  if (step === 2) {
    return (
      <LiveQuizQuestionPicker
        target={questionCount}
        initial={picked}
        onBack={() => setStep(1)}
        onConfirm={(p) => { setPicked(p); setStep(3) }}
      />
    )
  }

  if (step === 3) {
    return <ReadyToStartPlaceholder config={config} picked={picked} onBack={() => setStep(2)} />
  }

  return (
    <div className="space-y-5 max-w-3xl">
      <p className="text-sm text-muted-foreground">
        Monte um quiz síncrono pra responder ao vivo. Etapa 1: configuração.
      </p>

      {/* Modo */}
      <section className="space-y-2">
        <SectionTitle>Modo</SectionTitle>
        <div className="grid gap-3 sm:grid-cols-2">
          <ModeCard
            active={mode === "turma"} onClick={() => setMode("turma")}
            icon={<Users className="h-5 w-5" />} title="Quiz da turma"
            desc="Só alunos das turmas que você escolher (já aprovados via convite)."
          />
          <ModeCard
            active={mode === "livre"} onClick={() => setMode("livre")}
            icon={<Globe className="h-5 w-5" />} title="Quiz livre"
            desc="Gera um código curto na hora; qualquer pessoa entra digitando o token."
          />
        </div>
      </section>

      {/* Turmas + alunos (modo turma) */}
      {mode === "turma" && (
        <section className="space-y-3">
          <SectionTitle>Turmas participantes</SectionTitle>
          {turmasQuery.isLoading && <Skeleton className="h-20 w-full" />}
          {!turmasQuery.isLoading && (turmasQuery.data?.length ?? 0) === 0 && (
            <Card><CardContent className="py-6 text-center text-sm text-muted-foreground">
              Você ainda não tem turmas. Crie uma na aba <strong>Turmas</strong> e convide alunos.
            </CardContent></Card>
          )}
          <div className="flex flex-wrap gap-2">
            {turmasQuery.data?.map((t) => (
              <button key={t.id} type="button" onClick={() => toggleTurma(t.id)}
                className={cn(
                  "inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-sm transition-colors",
                  selectedTurmas.has(t.id) ? "border-primary bg-primary/10 text-primary" : "border-border hover:border-primary/50",
                )}>
                <span>{t.emblem || "🎓"}</span>{t.name}
                <Badge variant="outline" className="text-[10px]">{t.memberCount}</Badge>
                {selectedTurmas.has(t.id) && <Check className="h-3.5 w-3.5" />}
              </button>
            ))}
          </div>

          {selectedTurmas.size > 0 && (
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Alunos ({selectedStudentIds.length}/{members.length})
                </p>
                {members.length > 0 && (
                  <div className="flex gap-2">
                    <button className="text-xs text-primary hover:underline" onClick={() => setDeselected(new Set())}>todos</button>
                    <button className="text-xs text-muted-foreground hover:underline" onClick={() => setDeselected(new Set(members.map((m) => m.userId)))}>nenhum</button>
                  </div>
                )}
              </div>
              {detailQueries.some((q) => q.isLoading) && <Skeleton className="h-10 w-full" />}
              {members.length === 0 && !detailQueries.some((q) => q.isLoading) && (
                <p className="text-sm text-muted-foreground">Nenhum aluno ativo nas turmas escolhidas.</p>
              )}
              <div className="grid gap-1 sm:grid-cols-2 max-h-52 overflow-y-auto">
                {members.map((m) => {
                  const on = !deselected.has(m.userId)
                  return (
                    <button key={m.userId} type="button" onClick={() => toggleStudent(m.userId)}
                      className={cn(
                        "flex items-center gap-2 rounded-md border px-2.5 py-1.5 text-left transition-colors",
                        on ? "border-primary/40 bg-primary/5" : "border-border opacity-60",
                      )}>
                      <span className={cn("flex h-4 w-4 items-center justify-center rounded border", on ? "bg-primary border-primary text-primary-foreground" : "border-muted-foreground/40")}>
                        {on && <Check className="h-3 w-3" />}
                      </span>
                      <span className="flex-1 min-w-0 text-sm truncate">{m.name}</span>
                      <span className="text-[10px] text-muted-foreground tabular-nums">{m.xp} XP</span>
                    </button>
                  )
                })}
              </div>
            </div>
          )}
        </section>
      )}

      {mode === "livre" && (
        <Card className="border-primary/30 bg-primary/5">
          <CardContent className="flex items-center gap-3 py-4">
            <Ticket className="h-6 w-6 text-primary shrink-0" />
            <p className="text-sm text-muted-foreground">
              Ao iniciar, um <strong>código curto</strong> será gerado. Quem tiver o código
              entra na sala — sem precisar estar numa turma.
            </p>
          </CardContent>
        </Card>
      )}

      {/* Configurações */}
      <section className="space-y-3">
        <SectionTitle>Configurações</SectionTitle>
        <div className="grid gap-3 sm:grid-cols-2">
          <NumberField icon={<ListChecks className="h-4 w-4" />} label="Quantidade de perguntas"
            value={questionCount} min={1} max={50} onChange={setQuestionCount} hint="1 a 50" />
          <NumberField icon={<Clock className="h-4 w-4" />} label="Tempo por pergunta (s)"
            value={secondsPerQuestion} min={5} max={120} onChange={setSeconds} hint="5 a 120s" />
        </div>
        <div className="space-y-1.5">
          <ToggleRow icon={<Shuffle className="h-4 w-4" />} label="Embaralhar ordem das perguntas"
            on={shuffleQuestions} onToggle={() => setShuffleQuestions((v) => !v)} />
          <ToggleRow icon={<ListOrdered className="h-4 w-4" />} label="Embaralhar alternativas"
            on={shuffleOptions} onToggle={() => setShuffleOptions((v) => !v)} />
          <ToggleRow icon={<Trophy className="h-4 w-4" />} label="Mostrar ranking entre perguntas"
            on={showRankBetween} onToggle={() => setShowRankBetween((v) => !v)} />
        </div>
      </section>

      <div className="flex items-center justify-between pt-2">
        <p className="text-xs text-muted-foreground">
          {mode === "turma"
            ? `${selectedStudentIds.length} participante(s) · ${questionCount} pergunta(s)`
            : `Código gerado ao iniciar · ${questionCount} pergunta(s)`}
        </p>
        <Button disabled={!canAdvance} onClick={() => setStep(2)}>
          Selecionar perguntas<ChevronRight className="h-4 w-4 ml-1" />
        </Button>
      </div>
    </div>
  )
}

// ── subcomponentes ────────────────────────────────────────────────────

function SectionTitle({ children }: { children: React.ReactNode }) {
  return <h3 className="text-xs font-display font-bold uppercase tracking-wider text-muted-foreground">{children}</h3>
}

function ModeCard({ active, onClick, icon, title, desc }: {
  active: boolean; onClick: () => void; icon: React.ReactNode; title: string; desc: string
}) {
  return (
    <button type="button" onClick={onClick}
      className={cn(
        "text-left rounded-lg border p-4 transition-colors",
        active ? "border-primary bg-primary/10" : "border-border hover:border-primary/50",
      )}>
      <div className={cn("flex items-center gap-2 font-display font-bold", active && "text-primary")}>
        {icon}{title}
        {active && <Check className="h-4 w-4 ml-auto" />}
      </div>
      <p className="text-xs text-muted-foreground mt-1">{desc}</p>
    </button>
  )
}

function NumberField({ icon, label, value, min, max, onChange, hint }: {
  icon: React.ReactNode; label: string; value: number; min: number; max: number; onChange: (n: number) => void; hint: string
}) {
  return (
    <div className="rounded-md border border-border p-3">
      <label className="flex items-center gap-1.5 text-sm font-medium mb-1.5">{icon}{label}</label>
      <Input type="number" min={min} max={max} value={value}
        onChange={(e) => onChange(Math.max(min, Math.min(max, Number(e.target.value) || min)))} />
      <p className="text-[10px] text-muted-foreground mt-1">{hint}</p>
    </div>
  )
}

function ToggleRow({ icon, label, on, onToggle }: {
  icon: React.ReactNode; label: string; on: boolean; onToggle: () => void
}) {
  return (
    <button type="button" onClick={onToggle}
      className="w-full flex items-center gap-2.5 rounded-md border border-border px-3 py-2 text-left hover:bg-popover/60 transition-colors">
      <span className="text-muted-foreground">{icon}</span>
      <span className="flex-1 text-sm">{label}</span>
      <span className={cn("relative h-5 w-9 rounded-full transition-colors", on ? "bg-primary" : "bg-muted")}>
        <span className={cn("absolute top-0.5 h-4 w-4 rounded-full bg-white transition-all", on ? "left-[18px]" : "left-0.5")} />
      </span>
    </button>
  )
}

function ReadyToStartPlaceholder({ config, picked, onBack }: { config: LiveQuizConfig; picked: PickedQuestion[]; onBack: () => void }) {
  return (
    <div className="space-y-4 max-w-3xl">
      <Button variant="ghost" size="sm" className="-ml-2" onClick={onBack}>
        <ChevronLeft className="h-4 w-4 mr-1" />Voltar à seleção
      </Button>
      <Card className="border-primary/30 bg-primary/5">
        <CardContent className="py-8 space-y-3 text-center">
          <Radio className="h-10 w-10 text-primary mx-auto" />
          <p className="font-display font-bold text-lg">Tudo pronto pra iniciar</p>
          <div className="flex flex-wrap justify-center gap-2 text-sm">
            <Badge variant="outline">{config.mode === "turma" ? `${config.studentIds.length} participante(s)` : "Quiz livre (token)"}</Badge>
            <Badge variant="outline">{picked.length} pergunta(s)</Badge>
            <Badge variant="outline">{config.secondsPerQuestion}s por pergunta</Badge>
            {config.shuffleQuestions && <Badge variant="outline">ordem aleatória</Badge>}
            {config.shuffleOptions && <Badge variant="outline">alternativas aleatórias</Badge>}
          </div>
          <p className="text-sm text-muted-foreground max-w-md mx-auto pt-1">
            Próxima etapa: o lobby ao vivo (gerar token/sala, alunos entram, controle de
            perguntas e ranking em tempo real) — em construção.
          </p>
        </CardContent>
      </Card>
    </div>
  )
}
