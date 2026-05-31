import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Link, useNavigate } from "@tanstack/react-router"
import { Loader2 } from "lucide-react"
import { toast } from "sonner"
import { trailsApi } from "@/api/trails"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { Button } from "@/components/ui/button"
import type { Trail } from "@/types/api"

/** Catálogo de trilhas — todas, com indicação de quais o user já está inscrito.
 *  Trilhas não-inscritas têm CTA de inscrição direta (one-click); inscritas
 *  levam pra jornada do dia. PR 28 trouxe trilha Angular importada via MD. */
export function TrailsPage() {
  const trailsQuery = useQuery({ queryKey: ["trails"], queryFn: trailsApi.list })

  return (
    <div className="p-6 lg:p-10 space-y-6">
      <header>
        <h1 className="text-3xl font-display font-extrabold tracking-tight">🗺️ Trilhas</h1>
        <p className="text-muted-foreground mt-1">
          Explore todas as áreas de TI disponíveis. Inscreva-se direto no card ou via onboarding pra calibração inicial.
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
          {trailsQuery.data?.map((t, i) => (
            <div key={t.id} className="animate-pop-in" style={{ animationDelay: `${Math.min(i, 6) * 60}ms` }}>
              <TrailCard trail={t} />
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function TrailCard({ trail }: { trail: Trail }) {
  const navigate = useNavigate()
  const qc       = useQueryClient()
  const enrolled = trail.userProgress >= 0

  // Mutation de inscrição. Em sucesso, invalida ["trails"] pro card
  // mudar de "Não inscrito" → "Ver →" automaticamente, e leva o user
  // pra jornada da trilha (UX one-click → conteúdo).
  const enrollMutation = useMutation({
    mutationFn: () => trailsApi.enroll(trail.id),
    onSuccess: () => {
      toast.success(`Inscrito em ${trail.name}!`)
      qc.invalidateQueries({ queryKey: ["trails"] })
      // Também invalida o profile pra atualizar trilhas no /perfil
      qc.invalidateQueries({ queryKey: ["profile", "me"] })
      navigate({ to: "/jornada/$trailId", params: { trailId: String(trail.id) } })
    },
    onError: () => toast.error("Não foi possível inscrever. Tente de novo."),
  })

  return (
    <Card
      className="border-l-4 h-full flex flex-col"
      style={{ borderLeftColor: trail.accentColor }}
    >
      <CardHeader>
        <div className="flex items-start gap-3">
          <span className="text-3xl leading-none">{trail.icon}</span>
          <div className="flex-1 min-w-0">
            <CardTitle className="text-base">{trail.name}</CardTitle>
            <CardDescription className="text-xs mt-1">{trail.description}</CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="flex items-center justify-between gap-3 mt-auto">
        <div className="flex gap-2 flex-wrap">
          <Badge variant="outline" className="text-[10px]">{trail.level}</Badge>
          <Badge variant="outline" className="text-[10px]">{trail.totalContents} conteúdos</Badge>
        </div>
        {enrolled ? (
          <Button size="sm" variant="ghost" asChild>
            <Link to="/jornada/$trailId" params={{ trailId: String(trail.id) }}>Ver →</Link>
          </Button>
        ) : (
          <Button
            size="sm"
            onClick={() => enrollMutation.mutate()}
            disabled={enrollMutation.isPending}
          >
            {enrollMutation.isPending
              ? <><Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" />Inscrevendo…</>
              : "Inscrever-se"}
          </Button>
        )}
      </CardContent>
    </Card>
  )
}
