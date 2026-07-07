import { useQuery } from "@tanstack/react-query"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import MarkdownPreview from "@uiw/react-markdown-preview"
import { Check, ChevronLeft, ChevronRight, Play, Sparkles } from "lucide-react"
import { api } from "@/api/client"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"

/**
 * PR 40 — modo "Estudar". Renderiza o markdown do conteúdo + barra de
 * progresso de desafios + CTA pro quiz. O backend já tem o endpoint
 * <c>/api/contents/{id}</c> retornando body + metadados — reusamos.
 *
 * <para>Sem progressão visível aqui (não-cumulativa em revisita) —
 * Status do UserContent é responsabilidade do mapa. Aqui mostramos só
 * "X/N desafios respondidos" pra contextualizar.</para>
 */
/** Espelha o ContentResponse do backend (PR 40 estendido com challengesRequired). */
type ContentDetail = {
  id:                  number
  trailId:             number
  title:               string
  body:                string
  externalUrl:         string | null
  type:                string
  level:               string
  order:               number
  isCompleted:         boolean
  challengesRequired:  number
}

type UserContentSummary = {
  contentId:           number
  challengesCompleted: number
  status:              "Locked" | "Available" | "InProgress" | "Completed"
}

export function ContentStudyPage() {
  const { contentId } = useParams({ from: "/authed/contents/$contentId" })
  const contentIdNum  = Number(contentId)
  const navigate      = useNavigate()

  const contentQuery = useQuery({
    queryKey: ["content", contentIdNum],
    queryFn:  () => api.get<ContentDetail>(`/api/contents/${contentIdNum}`).then((r) => r.data),
  })

  // Progresso do user no content é embutido no trail-map cache; reusamos
  // se já carregado. Senão, query separada via map da trilha.
  const trailId = contentQuery.data?.trailId
  const mapQuery = useQuery({
    queryKey: ["trail-map", trailId],
    queryFn:  () => api.get<{ nodes: UserContentSummary[] }>(`/api/journey/trails/${trailId}/map`).then((r) => r.data),
    enabled:  !!trailId,
  })

  const myProgress = mapQuery.data?.nodes.find((n) => n.contentId === contentIdNum)
  const isCompleted = myProgress?.status === "Completed"

  return (
    <div className="p-6 lg:p-10 max-w-7xl mx-auto space-y-5">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link
          to="/jornada/$trailId"
          params={contentQuery.data ? { trailId: String(contentQuery.data.trailId) } : { trailId: "0" }}
        >
          <ChevronLeft className="h-4 w-4 mr-1" />
          Voltar ao mapa
        </Link>
      </Button>

      {contentQuery.isLoading && (
        <div className="space-y-4">
          <Skeleton className="h-10 w-2/3" />
          <Skeleton className="h-5 w-1/3" />
          <Skeleton className="h-64 w-full" />
        </div>
      )}

      {contentQuery.isError && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardContent className="pt-6 text-sm">Conteúdo não encontrado.</CardContent>
        </Card>
      )}

      {contentQuery.data && (
        <>
          <header className="space-y-2">
            <h1 className="text-3xl font-display font-extrabold tracking-tight">
              {contentQuery.data.title}
            </h1>
            <div className="flex items-center gap-2 flex-wrap">
              <Badge variant="outline" className="text-xs">{contentQuery.data.level}</Badge>
              {myProgress && (
                <Badge
                  variant={isCompleted ? "default" : "outline"}
                  className="text-xs gap-1"
                >
                  {isCompleted && <Check className="h-3 w-3" />}
                  {myProgress.challengesCompleted}/{contentQuery.data.challengesRequired} desafios
                </Badge>
              )}
            </div>
          </header>

          <Card>
            <CardContent className="pt-6">
              {/* react-markdown-preview já vem com syntax highlight + dark mode.
                  data-color-mode="dark" alinha com o tema do projeto. */}
              <div data-color-mode="dark" className="prose-md">
                <MarkdownPreview
                  source={contentQuery.data.body}
                  style={{ background: "transparent", color: "inherit" }}
                />
              </div>
            </CardContent>
          </Card>

          {/* CTA pro quiz */}
          <Card className={
            isCompleted
              ? "border-success/30 bg-success/5"
              : "bg-gradient-to-br from-primary/10 via-card to-card border-primary/30"
          }>
            <CardHeader>
              <CardTitle className="text-lg flex items-center gap-2">
                {isCompleted
                  ? <><Sparkles className="h-5 w-5 text-success" />Ilha conquistada!</>
                  : <><Play className="h-5 w-5 text-primary" />Pronto pra praticar?</>}
              </CardTitle>
              <CardDescription>
                {isCompleted
                  ? "Você já completou o gate desta ilha. Praticar agora conta como reforço — sua mastery continua subindo."
                  : `Responda ${contentQuery.data.challengesRequired} desafios deste conteúdo pra desbloquear a próxima ilha do mapa.`}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {/* A escolha do modo de prática (guiado / rápido / adaptativo)
                  vive numa tela dedicada de cards — o aluno decide COMO
                  praticar depois de ler o conteúdo. */}
              <Button
                size="lg"
                onClick={() => navigate({ to: "/contents/$contentId/practice", params: { contentId } })}
              >
                <Play className="h-4 w-4 mr-1" />
                {isCompleted ? "Praticar de novo" : "Praticar"}
                <ChevronRight className="h-4 w-4 ml-1" />
              </Button>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  )
}
