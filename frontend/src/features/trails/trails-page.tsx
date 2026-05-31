import { useQuery } from "@tanstack/react-query"
import { Link } from "@tanstack/react-router"
import { trailsApi } from "@/api/trails"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { Button } from "@/components/ui/button"

/** Catálogo de trilhas — todas, com indicação de quais o user já está inscrito. */
export function TrailsPage() {
  const trailsQuery = useQuery({ queryKey: ["trails"], queryFn: trailsApi.list })

  return (
    <div className="p-6 lg:p-10 space-y-6">
      <header>
        <h1 className="text-3xl font-display font-extrabold tracking-tight">🗺️ Trilhas</h1>
        <p className="text-muted-foreground mt-1">
          Explore todas as áreas de TI disponíveis. Inscreva-se via onboarding.
        </p>
      </header>

      {trailsQuery.isLoading ? (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {[1, 2, 3, 4].map((i) => (
            <Card key={i}><CardHeader><Skeleton className="h-5 w-32" /><Skeleton className="h-3 w-full mt-1" /></CardHeader></Card>
          ))}
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {trailsQuery.data?.map((t) => (
            <Card
              key={t.id}
              className="border-l-4"
              style={{ borderLeftColor: t.accentColor }}
            >
              <CardHeader>
                <div className="flex items-start gap-3">
                  <span className="text-3xl leading-none">{t.icon}</span>
                  <div className="flex-1">
                    <CardTitle className="text-base">{t.name}</CardTitle>
                    <CardDescription className="text-xs mt-1">{t.description}</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="flex items-center justify-between">
                <div className="flex gap-2 flex-wrap">
                  <Badge variant="outline" className="text-[10px]">{t.level}</Badge>
                  <Badge variant="outline" className="text-[10px]">{t.totalContents} conteúdos</Badge>
                </div>
                {t.userProgress >= 0 ? (
                  <Button size="sm" variant="ghost" asChild>
                    <Link to="/jornada/$trailId" params={{ trailId: String(t.id) }}>Ver →</Link>
                  </Button>
                ) : (
                  <Badge variant="secondary">Não inscrito</Badge>
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
