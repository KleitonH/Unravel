import { useQuery } from "@tanstack/react-query"
import { Link } from "@tanstack/react-router"
import { Compass, MapPin, RefreshCw, Search, Sparkles } from "lucide-react"
import { trailsApi } from "@/api/trails"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { Trail } from "@/types/api"

/**
 * "Minhas Trilhas" — lista APENAS trilhas onde o aluno está inscrito,
 * pra ser o ponto de retorno natural ("continuar de onde parei").
 *
 * <para>Quando vazia (aluno sem nenhuma trilha), oferece o CTA principal
 * <b>"Criar jornada"</b> que leva ao /onboarding (calibração) — fluxo
 * recomendado pra primeira trilha. Botão secundário "Ver todas" linka
 * pra /trails/discover (catálogo, inscrição one-click sem calibração).</para>
 *
 * <para>Botão "Explorar todas →" também fica no header pra quem já tem
 * trilhas e quer descobrir novas sem passar pelo onboarding.</para>
 */
export function TrailsPage() {
  const trailsQuery = useQuery({ queryKey: ["trails"], queryFn: trailsApi.list })
  const enrolled    = (trailsQuery.data ?? []).filter((t) => t.userProgress >= 0)

  return (
    <div className="mx-auto w-full max-w-6xl p-6 lg:p-10 space-y-6">
      <header className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-3xl font-display font-extrabold tracking-tight flex items-center gap-2">
            <MapPin className="h-7 w-7 text-primary" />
            Minhas trilhas
          </h1>
          <p className="text-muted-foreground mt-1">
            Continue de onde parou. Cada trilha tem seu próprio mapa de ilhas pra explorar.
          </p>
        </div>
        {enrolled.length > 0 && (
          <div className="flex gap-2 flex-wrap">
            <Button asChild variant="outline">
              <Link to="/trails/discover">
                <Search className="h-4 w-4 mr-1" />
                Explorar todas
              </Link>
            </Button>
            {/* "Reformular jornada" = entrada pro onboarding pra quem JÁ
                tem trilhas. Onboarding pede confirmação antes de sobrescrever
                masteries existentes (modal "ReformulaConfirmDialog"). */}
            <Button asChild>
              <Link to="/onboarding">
                <RefreshCw className="h-4 w-4 mr-1" />
                Reformular jornada
              </Link>
            </Button>
          </div>
        )}
      </header>

      {trailsQuery.isLoading ? (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {[1, 2, 3].map((i) => (
            <Card key={i}><CardHeader><Skeleton className="h-5 w-32" /><Skeleton className="h-3 w-full mt-1" /></CardHeader></Card>
          ))}
        </div>
      ) : enrolled.length === 0 ? (
        <EmptyState />
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {enrolled.map((t, i) => (
            <div key={t.id} className="animate-pop-in" style={{ animationDelay: `${Math.min(i, 6) * 60}ms` }}>
              <EnrolledTrailCard trail={t} />
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function EmptyState() {
  return (
    <Card className="bg-gradient-to-br from-primary/10 via-card to-card border-primary/30">
      <CardHeader className="text-center pt-10 pb-2">
        <div className="mx-auto mb-3 flex h-16 w-16 items-center justify-center rounded-full bg-primary/15">
          <Compass className="h-8 w-8 text-primary" />
        </div>
        <CardTitle className="text-2xl">Você ainda não está em nenhuma trilha</CardTitle>
        <CardDescription className="max-w-md mx-auto mt-2">
          Pra começar do jeito certo, faça o onboarding — um teste rápido pra
          calibrar seu nível e montar uma jornada que combine com você.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col sm:flex-row items-center justify-center gap-3 py-8">
        <Button asChild size="lg">
          <Link to="/onboarding">
            <Sparkles className="h-4 w-4 mr-1" />
            Criar jornada
          </Link>
        </Button>
        <Button asChild variant="ghost" size="lg">
          <Link to="/trails/discover">
            Ver todas as trilhas
          </Link>
        </Button>
      </CardContent>
    </Card>
  )
}

function EnrolledTrailCard({ trail }: { trail: Trail }) {
  const pct = Math.max(0, Math.min(100, Math.round(trail.userProgress)))
  const accent = trail.accentColor

  return (
    <Link
      to="/jornada/$trailId"
      params={{ trailId: String(trail.id) }}
      className={cn(
        "group relative flex h-full flex-col overflow-hidden rounded-2xl border border-border bg-card",
        "p-4 pl-5 transition-all hover:-translate-y-0.5 hover:shadow-lg",
      )}
      style={{ ["--accent" as string]: accent }}
    >
      {/* barra de accent à esquerda */}
      <span
        className="absolute left-0 top-3 bottom-3 w-1 rounded-r-full transition-all group-hover:top-2 group-hover:bottom-2"
        style={{ background: accent }}
      />

      <div className="flex items-start gap-3">
        <div
          className="grid h-11 w-11 shrink-0 place-items-center rounded-xl text-2xl"
          style={{ background: `${accent}22`, border: `1.5px solid ${accent}55` }}
        >
          {trail.icon}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="truncate font-semibold">{trail.name}</span>
            <Badge variant="outline" className="shrink-0 text-[10px]">{trail.level}</Badge>
          </div>
          <p className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">{trail.description}</p>
          <p className="mt-1 text-[11px] text-muted-foreground">{trail.totalContents} ilhas</p>
        </div>
        <span className="shrink-0 text-right text-xs font-bold" style={{ color: accent }}>
          {pct}%
        </span>
      </div>

      {/* barra de progresso */}
      <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-muted">
        <div className="h-full rounded-full transition-[width] duration-500" style={{ width: `${pct}%`, background: accent }} />
      </div>

      <span className="mt-3 inline-flex items-center gap-1 text-xs font-semibold text-primary">
        Continuar <span className="transition-transform group-hover:translate-x-0.5">→</span>
      </span>
    </Link>
  )
}
