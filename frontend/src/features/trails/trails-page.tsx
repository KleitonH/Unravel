import { useQuery } from "@tanstack/react-query"
import { Link } from "@tanstack/react-router"
import { Compass, MapPin, Search, Sparkles } from "lucide-react"
import { trailsApi } from "@/api/trails"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
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
    <div className="p-6 lg:p-10 space-y-6">
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
          <Button asChild variant="outline">
            <Link to="/trails/discover">
              <Search className="h-4 w-4 mr-1" />
              Explorar todas
            </Link>
          </Button>
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
  return (
    <Card
      className="border-l-4 h-full flex flex-col hover:border-l-primary transition-colors"
      style={{ borderLeftColor: trail.accentColor }}
    >
      <CardHeader>
        <div className="flex items-start gap-3">
          <span className="text-3xl leading-none">{trail.icon}</span>
          <div className="flex-1 min-w-0">
            <CardTitle className="text-base truncate">{trail.name}</CardTitle>
            <CardDescription className="text-xs mt-1 line-clamp-2">{trail.description}</CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="flex items-center justify-between gap-3 mt-auto">
        <div className="flex gap-2 flex-wrap">
          <Badge variant="outline" className="text-[10px]">{trail.level}</Badge>
          <Badge variant="outline" className="text-[10px]">{trail.totalContents} ilhas</Badge>
        </div>
        <Button size="sm" asChild>
          <Link to="/jornada/$trailId" params={{ trailId: String(trail.id) }}>
            Continuar →
          </Link>
        </Button>
      </CardContent>
    </Card>
  )
}
