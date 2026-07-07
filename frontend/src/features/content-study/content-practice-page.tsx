import { useQuery } from "@tanstack/react-query"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import { Activity, BookOpen, ChevronLeft, ChevronRight, Zap } from "lucide-react"
import { api } from "@/api/client"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"

/**
 * Tela de escolha de prática (aberta a partir da tela de estudo do conteúdo).
 * Depois de ler o material, o aluno escolhe COMO praticar entre três modos,
 * apresentados como cards auto-explicativos (título + subtítulo + ícone) para
 * que a decisão não dependa de conhecer os rótulos técnicos.
 */
type Mode = {
  key:       string
  Icon:      React.ComponentType<{ className?: string }>
  title:     string
  subtitle:  string
  accent:    string // classe de cor do ícone/realce
  recommended?: boolean
  to:        "/study/$contentId" | "/quiz/$contentId" | "/quiz/$contentId/adaptive"
}

const MODES: Mode[] = [
  {
    key: "guided", Icon: BookOpen, to: "/study/$contentId", recommended: true,
    accent: "text-primary",
    title: "Estudo guiado",
    subtitle: "Aprenda do zero: leia cada capítulo e pratique logo em seguida. Ideal na primeira vez.",
  },
  {
    key: "quick", Icon: Zap, to: "/quiz/$contentId",
    accent: "text-warning",
    title: "Quiz rápido",
    subtitle: "Já estudou? Responda um lote curto de perguntas embaralhadas pra fixar o conteúdo.",
  },
  {
    key: "adaptive", Icon: Activity, to: "/quiz/$contentId/adaptive",
    accent: "text-accent",
    title: "Modo adaptativo",
    subtitle: "Descubra seu nível: poucas perguntas que se ajustam ao seu desempenho e param quando entendem você.",
  },
]

type ContentMeta = { id: number; trailId: number; title: string }

export function ContentPracticePage() {
  const { contentId } = useParams({ from: "/authed/contents/$contentId/practice" })
  const navigate = useNavigate()

  const contentQuery = useQuery({
    queryKey: ["content", Number(contentId)],
    queryFn:  () => api.get<ContentMeta>(`/api/contents/${contentId}`).then((r) => r.data),
  })

  return (
    <div className="p-6 lg:p-10 max-w-6xl mx-auto space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/contents/$contentId" params={{ contentId }}>
          <ChevronLeft className="h-4 w-4 mr-1" />
          Voltar ao conteúdo
        </Link>
      </Button>

      <header className="space-y-1">
        <h1 className="text-2xl font-display font-extrabold tracking-tight">
          Como você quer praticar?
        </h1>
        {contentQuery.isLoading
          ? <Skeleton className="h-5 w-1/2" />
          : <p className="text-sm text-muted-foreground">{contentQuery.data?.title}</p>}
      </header>

      {/* Desktop: 3 cartas verticais lado a lado; mobile: empilhadas. */}
      <div className="grid gap-4 sm:grid-cols-3">
        {MODES.map((m) => (
          <Card
            key={m.key}
            role="button"
            tabIndex={0}
            onClick={() => navigate({ to: m.to, params: { contentId } })}
            onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); navigate({ to: m.to, params: { contentId } }) } }}
            className={cn(
              "group flex flex-col p-5 cursor-pointer transition-all min-h-[13rem]",
              "hover:border-primary/50 hover:-translate-y-0.5 hover:shadow-lg focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
              m.recommended && "border-primary/40 bg-primary/5",
            )}
          >
            <div className="flex items-start justify-between">
              <div className={cn(
                "grid place-items-center h-12 w-12 shrink-0 rounded-full bg-foreground/5",
                m.accent,
              )}>
                <m.Icon className="h-6 w-6" />
              </div>
              {m.recommended && (
                <Badge variant="outline" className="text-[10px] border-primary/50 text-primary">
                  Recomendado
                </Badge>
              )}
            </div>

            <h2 className="mt-3 font-semibold text-lg">{m.title}</h2>
            <p className="text-sm text-muted-foreground mt-1 flex-1">{m.subtitle}</p>

            <div className="mt-4 flex items-center text-sm font-medium text-primary">
              Escolher
              <ChevronRight className="h-4 w-4 ml-1 transition-transform group-hover:translate-x-0.5" />
            </div>
          </Card>
        ))}
      </div>
    </div>
  )
}
