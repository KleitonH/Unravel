import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Link, useNavigate } from "@tanstack/react-router"
import { ChevronLeft, Loader2 } from "lucide-react"
import { toast } from "sonner"
import { trailsApi } from "@/api/trails"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { Button } from "@/components/ui/button"
import type { Trail } from "@/types/api"

/**
 * Catálogo completo — todas as trilhas (inscritas + não-inscritas).
 * Separada de /trails (que agora é "Minhas Trilhas") pra evitar mistura
 * entre "continuar o que comecei" e "explorar o que existe".
 *
 * <para>Inscrição é one-click (sem calibração). Quem quer nivelamento
 * inicial vai pelo /onboarding (CTA "Criar Jornada" no empty state
 * da /trails).</para>
 */
export function TrailsDiscoverPage() {
  const trailsQuery = useQuery({ queryKey: ["trails"], queryFn: trailsApi.list })

  const enrolledCount = trailsQuery.data?.filter((t) => t.userProgress >= 0).length ?? 0
  const totalCount    = trailsQuery.data?.length ?? 0

  return (
    <div className="p-6 lg:p-10 space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/trails">
          <ChevronLeft className="h-4 w-4 mr-1" />
          Minhas trilhas
        </Link>
      </Button>

      <header>
        <h1 className="text-3xl font-display font-extrabold tracking-tight">🗺️ Todas as trilhas</h1>
        <p className="text-muted-foreground mt-1">
          Explore todas as áreas disponíveis. Inscreva-se direto ou faça o
          <Link to="/onboarding" className="text-primary underline mx-1">onboarding com calibração</Link>
          pra começar com o ritmo certo pra você.
        </p>
        {!trailsQuery.isLoading && (
          <p className="text-xs text-muted-foreground mt-2">
            {enrolledCount > 0
              ? <>Você está em <strong>{enrolledCount}</strong> de <strong>{totalCount}</strong> trilhas disponíveis</>
              : <>Total de <strong>{totalCount}</strong> trilhas disponíveis</>}
          </p>
        )}
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

  const enrollMutation = useMutation({
    mutationFn: () => trailsApi.enroll(trail.id),
    onSuccess: () => {
      toast.success(`Inscrito em ${trail.name}!`)
      qc.invalidateQueries({ queryKey: ["trails"] })
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
            <div className="flex items-center gap-2 flex-wrap">
              <CardTitle className="text-base">{trail.name}</CardTitle>
              {enrolled && (
                <Badge variant="default" className="text-[10px]">Inscrita</Badge>
              )}
            </div>
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
            <Link to="/jornada/$trailId" params={{ trailId: String(trail.id) }}>Continuar →</Link>
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
