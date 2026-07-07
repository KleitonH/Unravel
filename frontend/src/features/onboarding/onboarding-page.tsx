import { useMemo, useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { useNavigate } from "@tanstack/react-router"
import { toast } from "sonner"
import { AlertTriangle, ChevronRight } from "lucide-react"
import { onboardingApi } from "@/api/onboarding"
import { trailsApi } from "@/api/trails"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog"
import { Progress } from "@/components/ui/progress"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type {
  LevelingAnswer,
  OnboardingResult,
  OnboardingTest,
  Trail,
  TrailLevelEstimate,
} from "@/types/api"

type Step = "pick" | "test" | "result"

export function OnboardingPage() {
  const [step, setStep] = useState<Step>("pick")
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [test, setTest] = useState<OnboardingTest | null>(null)
  const [answers, setAnswers] = useState<Map<number, number>>(new Map())
  const [result, setResult] = useState<OnboardingResult | null>(null)
  const [loading, setLoading] = useState(false)

  /** PR — confirmação antes de sobrescrever mastery existente. Aberto
   *  pelo submitTest quando há trilhas selecionadas onde o aluno já
   *  tem progresso. Confirmar continua o flow normal. */
  const [confirmOpen, setConfirmOpen] = useState(false)

  const trailsQuery = useQuery({ queryKey: ["trails"], queryFn: trailsApi.list })
  const navigate = useNavigate()

  const totalQuestions = useMemo(
    () => test?.trails.reduce((acc, g) => acc + g.questions.length, 0) ?? 0,
    [test],
  )
  const canSubmit = totalQuestions > 0 && answers.size === totalQuestions

  function toggleTrail(id: number) {
    const next = new Set(selected)
    next.has(id) ? next.delete(id) : next.add(id)
    setSelected(next)
  }

  async function startTest() {
    if (selected.size === 0) return
    setLoading(true)
    try {
      const t = await onboardingApi.start(Array.from(selected))
      setTest(t)
      setStep("test")
    } catch (e) {
      const data = (e as { response?: { data?: { error?: string; message?: string } } })?.response?.data
      toast.error(data?.error ?? data?.message ?? "Não foi possível iniciar (talvez já feito).")
    } finally {
      setLoading(false)
    }
  }

  /** Trilhas selecionadas em que o aluno JÁ tem progresso — usado pra
   *  acender o modal de confirmação antes do submit sobrescrever mastery. */
  const conflictingTrails = useMemo<Trail[]>(() => {
    if (!trailsQuery.data) return []
    return trailsQuery.data.filter((t) => selected.has(t.id) && t.userProgress >= 0)
  }, [trailsQuery.data, selected])

  /** Submit guard: se algum dos selecionados é trilha já em andamento,
   *  abre modal. Senão, vai direto. */
  function handleSubmitClick() {
    if (!canSubmit) return
    if (conflictingTrails.length > 0) {
      setConfirmOpen(true)
      return
    }
    void submitTest()
  }

  async function submitTest() {
    if (!canSubmit) return
    setConfirmOpen(false)
    setLoading(true)
    try {
      const payload: { answers: LevelingAnswer[] } = {
        answers: Array.from(answers.entries()).map(([topicId, selectedOptionIndex]) => ({
          topicId, selectedOptionIndex,
        })),
      }
      const r = await onboardingApi.submit(Array.from(selected), payload)
      setResult(r)
      setStep("result")
    } catch {
      toast.error("Falha ao submeter respostas.")
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="mx-auto w-full max-w-3xl p-6 lg:p-10 space-y-6">
      <header className="space-y-3">
        <h1 className="text-3xl font-display font-extrabold tracking-tight">
          🐾 Vamos preparar sua jornada
        </h1>
        <Stepper current={step} />
      </header>

      {step === "pick" && (
        <section className="space-y-4">
          <p className="text-muted-foreground">
            Escolha as áreas que você quer estudar. O Navi vai criar um teste rápido
            para entender de onde você parte.
          </p>

          {trailsQuery.isLoading ? (
            <div className="grid gap-3 md:grid-cols-2">
              {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-24" />)}
            </div>
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              {trailsQuery.data?.map((t) => {
                const picked = selected.has(t.id)
                return (
                  <button
                    key={t.id}
                    type="button"
                    onClick={() => toggleTrail(t.id)}
                    className={cn(
                      "text-left rounded-lg border p-4 transition-all",
                      "hover:border-primary hover:bg-card",
                      picked
                        ? "border-primary bg-primary/5 ring-2 ring-primary/30"
                        : "border-border bg-card",
                    )}
                    style={picked ? undefined : { borderLeftWidth: 4, borderLeftColor: t.accentColor }}
                  >
                    <div className="flex items-start gap-3">
                      <span className="text-3xl leading-none">{t.icon}</span>
                      <div className="flex-1">
                        <h3 className="font-semibold">{t.name}</h3>
                        <p className="text-xs text-muted-foreground mt-1 line-clamp-2">
                          {t.description}
                        </p>
                        <p className="text-[11px] text-muted-foreground mt-2 uppercase tracking-wide">
                          {t.level} · {t.totalContents} conteúdos
                        </p>
                      </div>
                    </div>
                  </button>
                )
              })}
            </div>
          )}

          <div className="flex justify-end pt-4">
            <Button onClick={startTest} disabled={selected.size === 0 || loading}>
              {loading ? "Carregando…" : `Continuar (${selected.size})`}
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </section>
      )}

      {step === "test" && test && (
        <section className="space-y-4">
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>Responda como achar melhor. Não se preocupe em acertar tudo.</span>
            <Badge variant="outline">{answers.size} / {totalQuestions}</Badge>
          </div>

          {test.trails.map((group) => (
            <Card key={group.trailId}>
              <CardHeader>
                <CardTitle>{group.trailName}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-5">
                {group.questions.map((q) => (
                  <div key={q.topicId} className="space-y-2 pb-4 border-b border-border last:border-0 last:pb-0">
                    <p className="text-sm font-medium">{q.prompt}</p>
                    <div className="flex gap-1.5 flex-wrap">
                      <Badge variant="secondary" className="text-[10px]">{q.contentTitle}</Badge>
                      <Badge variant="outline" className="text-[10px]">{q.strategy}</Badge>
                    </div>
                    <div className="space-y-1.5 pt-1">
                      {q.options.map((opt, i) => {
                        const picked = answers.get(q.topicId) === i
                        return (
                          <button
                            key={i}
                            type="button"
                            onClick={() => {
                              const next = new Map(answers)
                              next.set(q.topicId, i)
                              setAnswers(next)
                            }}
                            className={cn(
                              "w-full flex items-center gap-3 rounded-md border px-3 py-2 text-sm text-left transition-colors",
                              picked
                                ? "border-primary bg-primary/10"
                                : "border-border hover:border-primary/50",
                            )}
                          >
                            <span className={cn(
                              "flex-shrink-0 h-6 w-6 rounded-md text-xs font-bold flex items-center justify-center",
                              picked ? "bg-primary text-primary-foreground" : "bg-background text-primary",
                            )}>
                              {["A","B","C","D","E","F"][i]}
                            </span>
                            <span>{opt}</span>
                          </button>
                        )
                      })}
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
          ))}

          <div className="flex justify-end pt-2">
            <Button onClick={handleSubmitClick} disabled={!canSubmit || loading}>
              {loading ? "Calibrando…" : "Finalizar"}
            </Button>
          </div>
        </section>
      )}

      {/* Modal de confirmação — só aparece se o submit vai sobrescrever
          progresso real. Não bloqueia novas trilhas (path comum, sem fricção). */}
      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-warning" />
              Reformular sua jornada?
            </DialogTitle>
            <DialogDescription className="pt-2 space-y-3">
              <span className="block">
                Você já tem progresso nas trilhas abaixo. Reformular vai
                <strong> recalibrar a mastery</strong> com base nas respostas
                que você acabou de dar — sobrescrevendo o histórico atual.
              </span>
              <span className="block">
                Suas perguntas vistas, XP, streak e ilhas completadas
                <strong> continuam intactos</strong>. Só os scores de
                domínio dos tópicos são recalculados.
              </span>
            </DialogDescription>
          </DialogHeader>

          <div className="my-2 rounded-md border border-warning/30 bg-warning/5 p-3">
            <p className="text-xs uppercase tracking-wider font-semibold text-warning mb-2">
              Trilhas afetadas
            </p>
            <ul className="space-y-1">
              {conflictingTrails.map((t) => (
                <li key={t.id} className="flex items-center gap-2 text-sm">
                  <span>{t.icon}</span>
                  <span className="font-medium">{t.name}</span>
                  <Badge variant="outline" className="text-[10px] ml-auto">
                    Progresso atual: {t.userProgress}%
                  </Badge>
                </li>
              ))}
            </ul>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)}>
              Cancelar
            </Button>
            <Button onClick={submitTest} disabled={loading}>
              {loading ? "Calibrando…" : "Reformular jornada"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {step === "result" && result && (
        <section className="space-y-4">
          <Card>
            <CardHeader className="text-center">
              <CardTitle className="text-2xl">🎉 Tudo pronto</CardTitle>
              <CardDescription>Suas trilhas foram calibradas:</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {result.estimates.map((e) => <EstimateRow key={e.trailId} e={e}
                onGo={() => navigate({ to: "/jornada/$trailId", params: { trailId: String(e.trailId) } })} />)}
            </CardContent>
            <CardFooter className="justify-center">
              <Button variant="outline" onClick={() => navigate({ to: "/dashboard" })}>
                Ir pro Início
              </Button>
            </CardFooter>
          </Card>
        </section>
      )}
    </div>
  )
}

function Stepper({ current }: { current: Step }) {
  const steps: { id: Step; label: string }[] = [
    { id: "pick",   label: "1. Trilhas" },
    { id: "test",   label: "2. Nivelamento" },
    { id: "result", label: "3. Pronto" },
  ]
  return (
    <ol className="flex gap-2 flex-wrap text-xs">
      {steps.map((s) => (
        <li key={s.id}
            className={cn(
              "rounded-full px-3 py-1 transition-colors",
              current === s.id
                ? "bg-primary text-primary-foreground font-bold"
                : "bg-card text-muted-foreground",
            )}>
          {s.label}
        </li>
      ))}
    </ol>
  )
}

function EstimateRow({ e, onGo }: { e: TrailLevelEstimate; onGo: () => void }) {
  const labelVariant = e.label === "Avançado" ? "warning"
                     : e.label === "Intermediário" ? "success"
                     : "default"
  return (
    <div className="space-y-2 rounded-lg border border-border p-3">
      <div className="flex items-center justify-between">
        <strong className="text-sm">{e.trailName}</strong>
        <Badge variant={labelVariant}>{e.label}</Badge>
      </div>
      <Progress value={e.estimatedMastery * 100} />
      <div className="flex items-center justify-between text-xs">
        <span className="text-muted-foreground">
          Domínio: {Math.round(e.estimatedMastery * 100)}%
        </span>
        <Button variant="link" size="sm" onClick={onGo}>
          Ver plano do dia →
        </Button>
      </div>
    </div>
  )
}
