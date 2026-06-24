import { useQuery } from "@tanstack/react-query"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import {
  AlertTriangle, BookOpen, Brain, ChevronLeft, Clock,
  Sparkles, Target, TrendingDown,
} from "lucide-react"
import { journeyApi } from "@/api/journey"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { MasterySeverity, TopicMasteryItem } from "@/types/api"

/**
 * PR 41 — radar de fraquezas. Mostra mastery efetiva (com decay) por
 * tópico da trilha, severity-ranked, com atalhos pra estudar/reforçar.
 *
 * <para>Sem chart radar tradicional (SVG 12-spoke é trabalho de polish);
 * uso de barras horizontais coloridas pelo severity entrega o mesmo
 * sinal cognitivo (vermelho = fraco, verde = sólido) sem complexidade
 * de implementação. Radar SVG fica como PR de design.</para>
 */
export function MasteryPage() {
  const { trailId } = useParams({ from: "/authed/trails/$trailId/mastery" })
  const trailIdNum  = Number(trailId)
  const navigate    = useNavigate()

  const masteryQuery = useQuery({
    queryKey: ["mastery", trailIdNum],
    queryFn:  () => journeyApi.mastery(trailIdNum),
    staleTime: 0,
  })

  const report  = masteryQuery.data
  const overall = report ? Math.round(report.averageEffectiveScore * 100) : 0

  return (
    <div className="mx-auto w-full max-w-6xl p-6 lg:p-10 space-y-5">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/jornada/$trailId" params={{ trailId }}>
          <ChevronLeft className="h-4 w-4 mr-1" />
          Voltar ao mapa
        </Link>
      </Button>

      <header className="space-y-1">
        <h1 className="text-3xl font-display font-extrabold tracking-tight flex items-center gap-2">
          <Brain className="h-7 w-7 text-primary" />
          Radar de domínio
        </h1>
        <p className="text-sm text-muted-foreground">
          {report?.trailName ?? <Skeleton className="inline-block h-4 w-32" />}
          {" · "}Veja onde você está forte, onde precisa revisar e onde nunca tocou.
        </p>
      </header>

      {masteryQuery.isLoading && (
        <div className="space-y-3">
          <Skeleton className="h-28 w-full" />
          {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-16" />)}
        </div>
      )}

      {masteryQuery.isError && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardContent className="pt-6 text-sm text-destructive">
            Falha ao carregar. Verifique se a trilha existe.
          </CardContent>
        </Card>
      )}

      {report && (
        <>
          {/* Overview */}
          <Card className="bg-gradient-to-br from-primary/10 via-card to-card border-primary/20">
            <CardContent className="pt-6 grid grid-cols-2 md:grid-cols-4 gap-3">
              <Stat
                icon={<Target className="h-4 w-4" />}
                label="Domínio médio"
                value={`${overall}%`}
                tone={overall >= 60 ? "good" : overall >= 30 ? "warning" : "bad"}
              />
              <Stat
                icon={<TrendingDown className="h-4 w-4" />}
                label="Fraquezas"
                value={String(report.weakCount)}
                tone={report.weakCount > 0 ? "warning" : "good"}
              />
              <Stat
                icon={<Clock className="h-4 w-4" />}
                label="Revisão SRS"
                value={String(report.srsDueCount)}
                tone={report.srsDueCount > 0 ? "warning" : "good"}
              />
              <Stat
                icon={<AlertTriangle className="h-4 w-4" />}
                label="Sem mastery"
                value={String(report.untouchedCount)}
                tone="muted"
              />
            </CardContent>
          </Card>

          {/* CTA reforço (so quando ha fraqueza real) */}
          {report.weakCount > 0 && (
            <Card className="border-accent/30 bg-accent/5">
              <CardContent className="pt-6 flex flex-wrap items-center justify-between gap-3">
                <div className="text-sm">
                  <strong className="text-accent">{report.weakCount}</strong>{" "}
                  {report.weakCount === 1 ? "tópico com domínio abaixo de 60%" : "tópicos com domínio abaixo de 60%"}.
                  Quer treinar focado nessas fraquezas?
                </div>
                <Button
                  onClick={() => navigate({ to: "/reinforce/$trailId", params: { trailId } })}
                  className="bg-accent hover:bg-accent/90 text-accent-foreground"
                >
                  <Sparkles className="h-4 w-4 mr-1" />
                  Treinar fraquezas
                </Button>
              </CardContent>
            </Card>
          )}

          {report.weakCount === 0 && report.topics.length > 0 && (
            <Card className="border-success/30 bg-success/5">
              <CardContent className="pt-6 text-center">
                <Sparkles className="h-8 w-8 text-success mx-auto mb-2" />
                <p className="font-display font-bold">Sem fraquezas detectadas!</p>
                <p className="text-sm text-muted-foreground">
                  Todos os tópicos que você praticou estão acima de 60% de domínio.
                </p>
              </CardContent>
            </Card>
          )}

          {/* Lista por severity */}
          <SeverityGroup
            severity="Weak"
            label="🔴 Precisa reforçar"
            description="Domínio abaixo de 60% (efetivo com decaimento)"
            items={report.topics.filter((t) => t.severity === "Weak")}
            navigate={navigate}
          />
          <SeverityGroup
            severity="Stale"
            label="🟡 Hora de revisar"
            description="Domínio bom, mas a revisão está vencida (SRS)"
            items={report.topics.filter((t) => t.severity === "Stale")}
            navigate={navigate}
          />
          <SeverityGroup
            severity="Solid"
            label="🟢 Consolidado"
            description="Domínio acima de 60% e revisão em dia"
            items={report.topics.filter((t) => t.severity === "Solid")}
            navigate={navigate}
            collapsedByDefault
          />
        </>
      )}
    </div>
  )
}

function Stat({
  icon, label, value, tone,
}: { icon: React.ReactNode; label: string; value: string; tone: "good" | "warning" | "bad" | "muted" }) {
  return (
    <div className="rounded-md border border-border bg-popover/40 p-3">
      <div className={cn(
        "flex items-center gap-1 text-xs uppercase tracking-wider",
        tone === "good"    && "text-success",
        tone === "warning" && "text-warning",
        tone === "bad"     && "text-destructive",
        tone === "muted"   && "text-muted-foreground",
      )}>
        {icon}{label}
      </div>
      <p className={cn(
        "font-display text-2xl font-extrabold mt-0.5",
        tone === "good"    && "text-success",
        tone === "warning" && "text-warning",
        tone === "bad"     && "text-destructive",
        tone === "muted"   && "text-foreground",
      )}>
        {value}
      </p>
    </div>
  )
}

function SeverityGroup({
  severity, label, description, items, navigate, collapsedByDefault = false,
}: {
  severity:          MasterySeverity
  label:             string
  description:       string
  items:             TopicMasteryItem[]
  navigate:          ReturnType<typeof useNavigate>
  collapsedByDefault?: boolean
}) {
  if (items.length === 0) return null

  return (
    <section className="space-y-2">
      <header className="flex items-baseline justify-between gap-3">
        <div>
          <h2 className="text-sm font-display font-bold uppercase tracking-wider">{label}</h2>
          <p className="text-xs text-muted-foreground">{description}</p>
        </div>
        <Badge variant="outline" className="text-[10px]">{items.length}</Badge>
      </header>
      <details open={!collapsedByDefault} className="space-y-2 group">
        <summary className="text-xs text-muted-foreground cursor-pointer hover:text-foreground select-none">
          <span className="group-open:hidden">▸ mostrar</span>
          <span className="hidden group-open:inline">▾ ocultar</span>
        </summary>
        <div className="space-y-2">
          {items.map((it) => (
            <TopicRow key={it.topicId} item={it} navigate={navigate} severity={severity} />
          ))}
        </div>
      </details>
    </section>
  )
}

function TopicRow({
  item, severity, navigate,
}: { item: TopicMasteryItem; severity: MasterySeverity; navigate: ReturnType<typeof useNavigate> }) {
  const pct = Math.round(item.effectiveScore * 100)

  return (
    <Card className={cn(
      "border-l-4",
      severity === "Weak"  && "border-l-destructive/70",
      severity === "Stale" && "border-l-warning/70",
      severity === "Solid" && "border-l-success/70",
    )}>
      <CardHeader className="flex flex-row items-center gap-3 space-y-0 py-3">
        <div className="min-w-0 flex-1">
          <CardTitle className="text-sm flex items-center gap-2 flex-wrap">
            <span className="truncate">{item.contentTitle}</span>
            {item.isSrsDue && (
              <Badge variant="outline" className="text-[10px] border-warning/40 text-warning gap-1">
                <Clock className="h-3 w-3" />
                Revisão vencida
              </Badge>
            )}
            {!item.hasMastery && (
              <Badge variant="outline" className="text-[10px] gap-1">
                Nunca toquei
              </Badge>
            )}
          </CardTitle>
          <CardDescription className="text-xs mt-1 flex items-center gap-3 flex-wrap">
            <span>
              Domínio: <strong className={cn(
                pct >= 60 ? "text-success" : pct >= 30 ? "text-warning" : "text-destructive",
              )}>{pct}%</strong>
            </span>
            {item.hasMastery && (
              <>
                <span>{item.confidence} tentativa{item.confidence === 1 ? "" : "s"}</span>
                {item.lastSeenAt && (
                  <span>Última: {fmtAgo(item.lastSeenAt)}</span>
                )}
              </>
            )}
          </CardDescription>
          {/* Mini-barra */}
          <div className="mt-2 h-1.5 rounded-full bg-muted/40 overflow-hidden">
            <div
              className={cn(
                "h-full transition-all",
                pct >= 60 ? "bg-success" : pct >= 30 ? "bg-warning" : "bg-destructive",
              )}
              style={{ width: `${pct}%` }}
            />
          </div>
        </div>
        <Button
          size="sm"
          variant="outline"
          onClick={() => navigate({ to: "/contents/$contentId", params: { contentId: String(item.contentId) } })}
        >
          <BookOpen className="h-4 w-4 mr-1" />
          Estudar
        </Button>
      </CardHeader>
    </Card>
  )
}

function fmtAgo(iso: string): string {
  const ms  = Date.now() - new Date(iso).getTime()
  const min = Math.floor(ms / 60_000)
  if (min < 1)  return "agora"
  if (min < 60) return `há ${min}min`
  const hr = Math.floor(min / 60)
  if (hr < 24)  return `há ${hr}h`
  const day = Math.floor(hr / 24)
  if (day < 7) return `há ${day}d`
  return `há ${Math.floor(day / 7)}sem`
}
