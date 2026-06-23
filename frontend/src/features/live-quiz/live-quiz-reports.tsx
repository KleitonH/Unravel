import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { ArrowLeft, BarChart3, ChevronRight, Target, Users, Timer, Check } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Progress } from "@/components/ui/progress"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import { liveQuizReportsApi, type LiveQuizReport } from "@/api/live-quiz-reports"

const pct = (v: number) => `${Math.round(v * 100)}%`
const accTone = (v: number) => (v >= 0.7 ? "text-success" : v >= 0.4 ? "text-warning" : "text-destructive")

/**
 * UC30 — Relatório pedagógico da turma. O professor escolhe uma sessão de
 * Quiz ao Vivo e vê: resumo, acertos por questão, lacunas por tópico e
 * desempenho individual. Read-only sobre os dados já persistidos da sessão.
 */
export function LiveQuizReports() {
  const [sessionId, setSessionId] = useState<number | null>(null)

  if (sessionId) return <ReportDetail sessionId={sessionId} onBack={() => setSessionId(null)} />
  return <SessionList onPick={setSessionId} />
}

function SessionList({ onPick }: { onPick: (id: number) => void }) {
  const sessions = useQuery({ queryKey: ["lq-reports", "sessions"], queryFn: liveQuizReportsApi.sessions })

  return (
    <div className="space-y-3">
      <p className="text-sm text-muted-foreground">
        Escolha uma sessão de Quiz ao Vivo para ver o desempenho da turma.
      </p>
      {sessions.isLoading ? (
        <div className="space-y-2">{[0, 1, 2].map((i) => <Skeleton key={i} className="h-16 w-full" />)}</div>
      ) : (sessions.data ?? []).length === 0 ? (
        <p className="text-sm text-muted-foreground">Você ainda não hospedou nenhuma sessão.</p>
      ) : (
        sessions.data!.map((s) => (
          <button
            key={s.id}
            onClick={() => onPick(s.id)}
            className="flex w-full items-center gap-3 rounded-lg border border-border bg-card px-4 py-3 text-left transition-colors hover:border-primary/50"
          >
            <div className="min-w-0 flex-1">
              <p className="font-semibold">
                Sala {s.joinCode}{" "}
                <span className="ml-1 text-xs font-normal text-muted-foreground">· {s.mode} · {s.status}</span>
              </p>
              <p className="text-xs text-muted-foreground">
                {s.createdAt} · {s.participantCount} participante(s) · {s.questionCount} pergunta(s)
              </p>
            </div>
            <ChevronRight className="h-5 w-5 text-muted-foreground" />
          </button>
        ))
      )}
    </div>
  )
}

function ReportDetail({ sessionId, onBack }: { sessionId: number; onBack: () => void }) {
  const report = useQuery({ queryKey: ["lq-reports", sessionId], queryFn: () => liveQuizReportsApi.report(sessionId) })

  if (report.isLoading) return <Skeleton className="h-64 w-full" />
  if (!report.data) return (
    <div className="space-y-3">
      <Button variant="ghost" size="sm" onClick={onBack}><ArrowLeft className="mr-1 h-4 w-4" /> Voltar</Button>
      <p className="text-sm text-muted-foreground">Relatório indisponível.</p>
    </div>
  )

  const r: LiveQuizReport = report.data
  const sum = r.summary

  return (
    <div className="space-y-5">
      <Button variant="ghost" size="sm" onClick={onBack}><ArrowLeft className="mr-1 h-4 w-4" /> Voltar às sessões</Button>

      {/* Resumo */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat icon={<Users className="h-4 w-4" />} value={sum.participantCount} label="participantes" />
        <Stat icon={<Target className="h-4 w-4" />} value={pct(sum.overallAccuracy)} label="acerto geral" tone={accTone(sum.overallAccuracy)} />
        <Stat icon={<BarChart3 className="h-4 w-4" />} value={sum.questionCount} label="perguntas" />
        <Stat icon={<Check className="h-4 w-4" />} value={Math.round(sum.avgScore)} label="pontuação média" />
      </div>

      {/* Lacunas por tópico */}
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">Lacunas por tópico</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {r.topics.length === 0 ? (
            <p className="text-sm text-muted-foreground">Sem dados de tópico.</p>
          ) : r.topics.map((t) => (
            <div key={t.topic} className="space-y-1">
              <div className="flex items-center justify-between text-sm">
                <span className="font-medium">{t.topic}</span>
                <span className={cn("tabular-nums", accTone(t.accuracy))}>{pct(t.accuracy)} ({t.correctCount}/{t.totalAnswers})</span>
              </div>
              <Progress value={Math.round(t.accuracy * 100)} />
            </div>
          ))}
        </CardContent>
      </Card>

      {/* Por questão */}
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">Desempenho por questão</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {r.questions.map((q) => (
            <div key={q.orderIndex} className="rounded-lg border border-border p-3">
              <div className="flex items-start justify-between gap-3">
                <p className="text-sm font-medium">
                  <span className="text-muted-foreground">Q{q.orderIndex + 1}.</span> {q.prompt}
                </p>
                <span className={cn("shrink-0 text-sm font-bold tabular-nums", accTone(q.accuracy))}>{pct(q.accuracy)}</span>
              </div>
              <div className="mt-1.5 flex items-center gap-3 text-xs text-muted-foreground">
                <span>{q.correctCount}/{q.totalAnswers} acertos</span>
                <span className="inline-flex items-center gap-1"><Timer className="h-3 w-3" /> {(q.avgMs / 1000).toFixed(1)}s médio</span>
              </div>
              {q.optionDistribution.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {q.optionDistribution.map((count, i) => (
                    <span
                      key={i}
                      className={cn(
                        "rounded px-2 py-0.5 text-[11px] tabular-nums",
                        i === q.correctIndex ? "bg-success/15 text-success" : "bg-muted/50 text-muted-foreground",
                      )}
                    >
                      {String.fromCharCode(65 + i)}: {count}{i === q.correctIndex && " ✓"}
                    </span>
                  ))}
                </div>
              )}
            </div>
          ))}
        </CardContent>
      </Card>

      {/* Por participante */}
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">Desempenho individual</CardTitle></CardHeader>
        <CardContent>
          <ol className="space-y-1">
            {r.participants.map((p) => (
              <li key={p.userId} className="flex items-center gap-3 rounded-lg px-3 py-2 text-sm odd:bg-muted/30">
                <span className="w-6 text-center font-bold tabular-nums">{p.rank}</span>
                <span className="min-w-0 flex-1 truncate font-medium">{p.displayName}</span>
                <span className={cn("text-xs tabular-nums", accTone(p.accuracy))}>{pct(p.accuracy)} ({p.correctCount}/{p.answered})</span>
                <span className="w-14 text-right font-bold tabular-nums">{p.score}</span>
              </li>
            ))}
          </ol>
        </CardContent>
      </Card>
    </div>
  )
}

function Stat({ icon, value, label, tone }: { icon: React.ReactNode; value: string | number; label: string; tone?: string }) {
  return (
    <div className="flex flex-col gap-1 rounded-lg border border-border bg-popover/40 p-3">
      <span className="text-muted-foreground">{icon}</span>
      <span className={cn("font-display text-xl font-extrabold tabular-nums", tone)}>{value}</span>
      <span className="text-[11px] text-muted-foreground">{label}</span>
    </div>
  )
}
