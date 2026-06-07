import { useQuery } from "@tanstack/react-query"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import { Activity, BookOpen, Brain, Check, ChevronLeft, Crown, Lock, MapPin, RefreshCw, Sparkles } from "lucide-react"
import { journeyApi } from "@/api/journey"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { TrailMapNode, UserContentStatus } from "@/types/api"

/**
 * PR 40 — mapa SMW da trilha. Substitui a JornadaPage anterior (lista
 * de itens recomendados pelo planner). Cada ilha é um Content;
 * destrava sequencialmente conforme o aluno completa challenges.
 *
 * <para>Layout zigue-zague vertical (mobile-friendly):</para>
 * <list type="bullet">
 *   <item>Ilhas alternam left/right via <c>isLeft = index % 2 === 0</c>.</item>
 *   <item>Conector vertical entre ilhas (CSS, sem SVG).</item>
 *   <item>NAVI 🐾 indicador na ilha InProgress (a "ativa" do momento).</item>
 *   <item>Locked = opacity reduzida + ícone cadeado, sem CTA.</item>
 * </list>
 *
 * <para>Botão "Reforçar fraquezas" sempre presente no topo —
 * complementa o mapa (mapa = avançar, reforço = consolidar).</para>
 */
export function TrailMapPage() {
  const { trailId } = useParams({ from: "/authed/jornada/$trailId" })
  const trailIdNum  = Number(trailId)
  const navigate    = useNavigate()

  const mapQuery = useQuery({
    queryKey: ["trail-map", trailIdNum],
    queryFn:  () => journeyApi.map(trailIdNum),
  })

  const map            = mapQuery.data
  const activeNode     = map?.nodes.find((n) => n.status === "InProgress")
                       ?? map?.nodes.find((n) => n.status === "Available")
  const totalNodes     = map?.nodes.length ?? 0
  const completedNodes = map?.nodes.filter((n) => n.status === "Completed").length ?? 0
  const recommendedCount = map?.nodes.filter((n) => n.isRecommended).length ?? 0
  // PR 50: Boss desbloqueado = todas as ilhas regulares Completed.
  const bossUnlocked   = totalNodes > 0 && completedNodes === totalNodes

  return (
    <div className="p-6 lg:p-10 max-w-3xl mx-auto space-y-6">
      <header className="flex items-start justify-between gap-3 flex-wrap">
        <div className="min-w-0">
          <h1 className="text-3xl font-display font-extrabold tracking-tight flex items-center gap-2">
            <MapPin className="h-7 w-7 text-primary shrink-0" />
            <span className="truncate">{map?.trailName ?? <Skeleton className="inline-block h-7 w-48 align-middle" />}</span>
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            {totalNodes > 0
              ? <>Ilhas conquistadas: <strong>{completedNodes}</strong> de <strong>{totalNodes}</strong></>
              : "Mapa em construção"}
          </p>
          {recommendedCount > 0 && (
            <p className="text-xs text-warning mt-1 flex items-center gap-1">
              <Sparkles className="h-3 w-3" />
              {recommendedCount === 1
                ? "1 ilha recomendada pra hoje pelo algoritmo de jornada"
                : `${recommendedCount} ilhas recomendadas pra hoje pelo algoritmo de jornada`}
            </p>
          )}
        </div>
        <div className="flex gap-2 shrink-0">
          {/* PR 41: atalho pro radar — antecede o reforco no fluxo
              "primeiro vejo onde estou fraco, depois treino". */}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate({ to: "/trails/$trailId/mastery", params: { trailId } })}
          >
            <Activity className="h-4 w-4 mr-1" />Radar
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="text-accent hover:text-accent hover:bg-accent/10"
            onClick={() => navigate({ to: "/reinforce/$trailId", params: { trailId } })}
          >
            <Brain className="h-4 w-4 mr-1" />Reforçar
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => mapQuery.refetch()}
            disabled={mapQuery.isFetching}
          >
            <RefreshCw className={cn("h-4 w-4", mapQuery.isFetching && "animate-spin")} />
          </Button>
        </div>
      </header>

      {mapQuery.isLoading && <MapSkeleton />}

      {mapQuery.isError && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardHeader>
            <CardTitle className="text-base text-destructive">Não foi possível carregar o mapa</CardTitle>
            <CardDescription>Verifique se a trilha existe e você está inscrito.</CardDescription>
          </CardHeader>
          <CardContent>
            <Button asChild>
              <Link to="/dashboard"><ChevronLeft className="h-4 w-4 mr-1" />Voltar</Link>
            </Button>
          </CardContent>
        </Card>
      )}

      {map && map.nodes.length === 0 && (
        <Card>
          <CardContent className="pt-10 pb-10 text-center text-muted-foreground">
            Essa trilha ainda não tem conteúdos.
          </CardContent>
        </Card>
      )}

      {map && map.nodes.length > 0 && (
        <div className="relative space-y-1">
          {map.nodes.map((node, i) => (
            <IslandRow
              key={node.contentId}
              node={node}
              isLeft={i % 2 === 0}
              isLast={i === map.nodes.length - 1 && !bossUnlocked}
              isActive={activeNode?.contentId === node.contentId}
              onOpen={() => navigate({ to: "/contents/$contentId", params: { contentId: String(node.contentId) } })}
            />
          ))}
          {/* PR 50 — Ilha do Boss aparece ao final quando todas regulares
              estão Completed. Linkada à rota /boss/$trailId. */}
          {bossUnlocked && (
            <BossIsland
              isLeft={map.nodes.length % 2 === 0}
              onOpen={() => navigate({ to: "/boss/$trailId", params: { trailId } })}
            />
          )}
        </div>
      )}
    </div>
  )
}

function IslandRow({
  node, isLeft, isLast, isActive, onOpen,
}: {
  node:      TrailMapNode
  isLeft:    boolean
  isLast:    boolean
  isActive:  boolean
  onOpen:    () => void
}) {
  const locked = node.status === "Locked"

  return (
    <div className="relative">
      {/* Conector vertical descendo até a próxima ilha. Última não tem. */}
      {!isLast && (
        <div
          aria-hidden
          className={cn(
            "absolute left-1/2 top-full w-0.5 h-6 -translate-x-1/2",
            node.status === "Completed" ? "bg-success/60" : "bg-border",
          )}
        />
      )}

      <div className={cn("flex items-center", isLeft ? "justify-start" : "justify-end")}>
        <div
          className={cn(
            "w-full sm:w-[78%] transition-all",
            isLeft ? "sm:mr-auto" : "sm:ml-auto",
          )}
        >
          <Card
            className={cn(
              "relative overflow-hidden border-l-4 transition-all",
              locked          && "opacity-50 border-l-border",
              !locked &&
                node.status === "Available"  && "border-l-primary",
              node.status === "InProgress" && "border-l-warning shadow-md shadow-warning/10",
              node.status === "Completed"  && "border-l-success",
              isActive && "ring-1 ring-warning/40 animate-pop-in",
              node.isRecommended && "ring-1 ring-warning/30",
            )}
          >
            {/* PR 42b — fita "RECOMENDADO HOJE" no canto superior da ilha.
                Mostrada só quando JourneyPlanner sugeriu este content como
                meta do dia. Backend já filtra pra Status != Locked/Completed. */}
            {node.isRecommended && (
              <div
                className="absolute top-0 right-0 bg-warning text-warning-foreground text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-bl-md flex items-center gap-1 shadow-sm"
                title="O algoritmo de jornada sugere esta ilha pra hoje (mastery, SRS ou pré-requisito recém-desbloqueado)"
              >
                <Sparkles className="h-3 w-3" />
                Hoje
              </div>
            )}
            <CardHeader className="flex flex-row items-start gap-3 space-y-0 pb-2">
              <StatusBadge status={node.status} order={node.order} />
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <h3 className={cn(
                    "font-display font-bold text-base truncate",
                    locked && "text-muted-foreground",
                  )}>
                    {node.title}
                  </h3>
                  {isActive && <span aria-label="Aqui você está" title="Você está aqui">🐾</span>}
                </div>
                {!locked && (
                  <div className="mt-1.5">
                    <ProgressBar done={node.challengesCompleted} total={node.challengesRequired} status={node.status} />
                  </div>
                )}
                {locked && (
                  <p className="text-xs text-muted-foreground mt-1 flex items-center gap-1">
                    <Lock className="h-3 w-3" />
                    Complete a ilha anterior pra desbloquear
                  </p>
                )}
              </div>
            </CardHeader>
            {!locked && (
              <CardContent className="pt-0 flex flex-wrap gap-2 justify-end">
                <Button size="sm" variant="outline" onClick={onOpen}>
                  <BookOpen className="h-4 w-4 mr-1" />
                  {node.status === "Completed" ? "Revisar" : "Estudar"}
                </Button>
                {node.status !== "Completed" && (
                  <Button size="sm" onClick={onOpen}>
                    {node.status === "InProgress" ? "Continuar →" : "Começar →"}
                  </Button>
                )}
              </CardContent>
            )}
          </Card>
        </div>
      </div>

      {/* Spacer entre ilhas (suporte ao conector vertical). */}
      {!isLast && <div className="h-6" />}
    </div>
  )
}

function StatusBadge({ status, order }: { status: UserContentStatus; order: number }) {
  if (status === "Completed") {
    return (
      <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-success/15 text-success border-2 border-success/30">
        <Check className="h-6 w-6" />
      </div>
    )
  }
  if (status === "Locked") {
    return (
      <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-muted/30 text-muted-foreground border-2 border-border">
        <Lock className="h-5 w-5" />
      </div>
    )
  }
  // Available / InProgress: mostra número da ilha
  return (
    <div className={cn(
      "flex h-12 w-12 shrink-0 items-center justify-center rounded-full font-display font-extrabold text-lg border-2",
      status === "Available"  && "bg-primary/15 text-primary border-primary/30",
      status === "InProgress" && "bg-warning/20 text-warning border-warning/40",
    )}>
      {order}
    </div>
  )
}

function ProgressBar({ done, total, status }: { done: number; total: number; status: UserContentStatus }) {
  const pct = Math.min(100, Math.round((done / total) * 100))
  return (
    <div className="space-y-0.5">
      <div className="h-1.5 rounded-full bg-muted/40 overflow-hidden">
        <div
          className={cn(
            "h-full transition-all duration-500",
            status === "Completed" ? "bg-success" : "bg-warning",
          )}
          style={{ width: `${pct}%` }}
        />
      </div>
      <div className="flex items-center gap-2 text-[11px] text-muted-foreground">
        <Badge variant="outline" className="text-[10px] py-0 px-1.5 font-bold">
          {done}/{total}
        </Badge>
        <span>desafios</span>
      </div>
    </div>
  )
}

function BossIsland({ isLeft, onOpen }: { isLeft: boolean; onOpen: () => void }) {
  return (
    <div className="relative">
      <div className={cn("flex items-center", isLeft ? "justify-start" : "justify-end")}>
        <div className={cn("w-full sm:w-[78%]", isLeft ? "sm:mr-auto" : "sm:ml-auto")}>
          <Card className="relative overflow-hidden border-l-4 border-l-warning bg-gradient-to-br from-warning/10 via-card to-card shadow-lg shadow-warning/10">
            <div className="absolute top-0 right-0 bg-warning text-warning-foreground text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-bl-md flex items-center gap-1 shadow-sm">
              <Crown className="h-3 w-3" />
              Boss
            </div>
            <CardHeader className="flex flex-row items-start gap-3 space-y-0 pb-2">
              <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-warning/20 text-warning border-2 border-warning/40">
                <Crown className="h-6 w-6" />
              </div>
              <div className="min-w-0 flex-1">
                <h3 className="font-display font-bold text-base">Desafio final da trilha</h3>
                <p className="text-xs text-muted-foreground mt-1">
                  10 perguntas cruzando todos os tópicos. Passe com ≥7 pra conquistar o título de Mestre.
                </p>
              </div>
            </CardHeader>
            <CardContent className="pt-0 flex justify-end">
              <Button
                size="sm"
                onClick={onOpen}
                className="bg-warning hover:bg-warning/90 text-warning-foreground"
              >
                <Crown className="h-4 w-4 mr-1" />
                Enfrentar Boss
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}

function MapSkeleton() {
  return (
    <div className="space-y-6">
      {[1, 2, 3, 4].map((i) => (
        <div key={i} className={cn("flex", i % 2 ? "justify-start" : "justify-end")}>
          <Skeleton className="h-24 w-full sm:w-[78%]" />
        </div>
      ))}
    </div>
  )
}
