import { useQuery } from "@tanstack/react-query"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import { Activity, BookOpen, Brain, Check, ChevronLeft, Crown, Lock, MapPin, Play, RefreshCw, Sparkles } from "lucide-react"
import { journeyApi } from "@/api/journey"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { TrailMapNode, UserContentStatus } from "@/types/api"

/**
 * PR 40 / repaginação 04-2026 — mapa de jornada da trilha. Cada nó é um
 * Content; destrava sequencialmente conforme o aluno completa challenges.
 *
 * <para>Layout = <b>timeline vertical</b> (porta do protótipo
 * "prototipo22042026" → tela Trilha Detalhe). Substitui o antigo
 * zigue-zague de ilhas por um trilho contínuo, mais legível em largura
 * ampla e fiel ao design aprovado:</para>
 * <list type="bullet">
 *   <item>Trilho à esquerda com dot por nó (✓ concluído / ▶ atual com
 *     pulse-ring / 🔒 bloqueado / número disponível).</item>
 *   <item>Conector vertical contínuo entre dots; segmentos concluídos em
 *     verde, os demais em <c>border</c>.</item>
 *   <item>Card à direita com título, progresso e CTA Estudar/Revisar.</item>
 *   <item>Boss ao final quando todas as ilhas regulares estão Completed.</item>
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
  const pct            = totalNodes > 0 ? Math.round((completedNodes / totalNodes) * 100) : 0
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

      {/* Hero de progresso — barra geral da trilha (porta do card-hero do
          protótipo). Só aparece quando há nós carregados. */}
      {map && totalNodes > 0 && (
        <Card className="relative overflow-hidden border-primary/30 bg-gradient-to-br from-primary/10 via-card to-card">
          <span aria-hidden className="absolute left-0 top-4 bottom-4 w-1.5 rounded-r-full bg-primary" />
          <span aria-hidden className="pointer-events-none absolute -top-16 right-12 h-44 w-52 rounded-full bg-primary/15 blur-3xl" />
          <CardContent className="pt-5 pb-5">
            <div className="flex items-end justify-between gap-3">
              <p className="text-sm text-muted-foreground">
                {bossUnlocked
                  ? "Trilha completa — encare o Boss pra conquistar o título de Mestre."
                  : "Seu progresso na trilha"}
              </p>
              <p className="font-display font-extrabold text-3xl text-primary leading-none">
                {pct}<span className="text-base text-muted-foreground">%</span>
              </p>
            </div>
            <div className="mt-3 h-2 rounded-full bg-muted/40 overflow-hidden">
              <div
                className="h-full rounded-full bg-primary transition-[width] duration-700"
                style={{ width: `${pct}%` }}
              />
            </div>
          </CardContent>
        </Card>
      )}

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
        <div className="relative">
          {map.nodes.map((node, i) => (
            <TimelineNode
              key={node.contentId}
              node={node}
              isFirst={i === 0}
              isLast={i === map.nodes.length - 1 && !bossUnlocked}
              isActive={activeNode?.contentId === node.contentId}
              onOpen={() => navigate({ to: "/contents/$contentId", params: { contentId: String(node.contentId) } })}
            />
          ))}
          {/* PR 50 — Boss ao final quando todas regulares estão Completed.
              Linkado à rota /boss/$trailId. */}
          {bossUnlocked && (
            <TimelineBoss onOpen={() => navigate({ to: "/boss/$trailId", params: { trailId } })} />
          )}
        </div>
      )}
    </div>
  )
}

/**
 * Um nó do trilho vertical: coluna do trilho (conector + dot) à esquerda,
 * card de conteúdo à direita. O conector "concluído" fica verde quando o
 * nó está Completed (linha que desce dele para o próximo).
 */
function TimelineNode({
  node, isFirst, isLast, isActive, onOpen,
}: {
  node:     TrailMapNode
  isFirst:  boolean
  isLast:   boolean
  isActive: boolean
  onOpen:   () => void
}) {
  const locked = node.status === "Locked"

  return (
    <div className="relative flex gap-4">
      {/* Coluna do trilho */}
      <div className="relative flex w-12 shrink-0 flex-col items-center">
        {/* Conector acima do dot */}
        {!isFirst && (
          <span
            aria-hidden
            className={cn(
              "absolute top-0 h-6 w-0.5",
              node.status === "Completed" ? "bg-success/60" : "bg-border",
            )}
          />
        )}
        {/* Conector abaixo do dot (preenche o resto da linha) */}
        {!isLast && (
          <span
            aria-hidden
            className={cn(
              "absolute bottom-0 top-12 w-0.5",
              node.status === "Completed" ? "bg-success/60" : "bg-border",
            )}
          />
        )}
        <div className="relative mt-0 pt-0">
          <TimelineDot status={node.status} order={node.order} isActive={isActive} />
        </div>
      </div>

      {/* Card de conteúdo */}
      <div className="min-w-0 flex-1 pb-6">
        <Card
          className={cn(
            "relative overflow-hidden transition-all",
            locked && "opacity-60",
            isActive && "ring-1 ring-primary/40 shadow-md shadow-primary/10 animate-pop-in",
            node.isRecommended && !isActive && "ring-1 ring-warning/30",
          )}
        >
          {/* PR 42b — fita "Hoje" quando o JourneyPlanner sugeriu este content. */}
          {node.isRecommended && (
            <div
              className="absolute top-0 right-0 bg-warning text-warning-foreground text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-bl-md flex items-center gap-1 shadow-sm"
              title="O algoritmo de jornada sugere esta ilha pra hoje (mastery, SRS ou pré-requisito recém-desbloqueado)"
            >
              <Sparkles className="h-3 w-3" />
              Hoje
            </div>
          )}
          <CardHeader className="pb-2">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className={cn(
                "font-display font-bold text-base",
                locked && "text-muted-foreground",
              )}>
                {node.title}
              </h3>
              {isActive && <span aria-label="Você está aqui" title="Você está aqui">🐾</span>}
            </div>
            {!locked ? (
              <div className="mt-1.5">
                <ProgressBar done={node.challengesCompleted} total={node.challengesRequired} status={node.status} />
              </div>
            ) : (
              <p className="text-xs text-muted-foreground mt-1 flex items-center gap-1">
                <Lock className="h-3 w-3" />
                Complete a ilha anterior pra desbloquear
              </p>
            )}
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
  )
}

/**
 * Dot do trilho (34px no protótipo → 40px aqui). O nó atual ganha um
 * pulse-ring (anel <c>animate-ping</c>) pra puxar o olhar pro "você está aqui".
 */
function TimelineDot({ status, order, isActive }: { status: UserContentStatus; order: number; isActive: boolean }) {
  const base = "relative z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full border-2 font-display font-extrabold text-base"

  if (status === "Completed") {
    return (
      <div className={cn(base, "bg-success/15 text-success border-success/40")}>
        <Check className="h-5 w-5" />
      </div>
    )
  }
  if (status === "Locked") {
    return (
      <div className={cn(base, "bg-muted/30 text-muted-foreground border-border")}>
        <Lock className="h-4 w-4" />
      </div>
    )
  }
  // Available / InProgress
  return (
    <div className="relative">
      {isActive && (
        <span aria-hidden className="absolute inset-0 rounded-full bg-primary/30 animate-ping" />
      )}
      <div className={cn(
        base,
        status === "InProgress" || isActive
          ? "bg-primary/20 text-primary border-primary/50"
          : "bg-primary/10 text-primary border-primary/30",
      )}>
        {isActive ? <Play className="h-4 w-4 fill-current" /> : order}
      </div>
    </div>
  )
}

function ProgressBar({ done, total, status }: { done: number; total: number; status: UserContentStatus }) {
  const pct = total > 0 ? Math.min(100, Math.round((done / total) * 100)) : 0
  return (
    <div className="space-y-0.5">
      <div className="h-1.5 rounded-full bg-muted/40 overflow-hidden">
        <div
          className={cn(
            "h-full transition-all duration-500",
            status === "Completed" ? "bg-success" : "bg-primary",
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

function TimelineBoss({ onOpen }: { onOpen: () => void }) {
  return (
    <div className="relative flex gap-4">
      <div className="relative flex w-12 shrink-0 flex-col items-center">
        {/* Conector acima ligando à última ilha (sempre verde — chega aqui
            só quando tudo está Completed). */}
        <span aria-hidden className="absolute top-0 h-6 w-0.5 bg-success/60" />
        <div className="relative z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full border-2 border-warning/50 bg-warning/20 text-warning">
          <Crown className="h-5 w-5" />
        </div>
      </div>
      <div className="min-w-0 flex-1">
        <Card className="relative overflow-hidden bg-gradient-to-br from-warning/10 via-card to-card shadow-lg shadow-warning/10">
          <div className="absolute top-0 right-0 bg-warning text-warning-foreground text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-bl-md flex items-center gap-1 shadow-sm">
            <Crown className="h-3 w-3" />
            Boss
          </div>
          <CardHeader className="pb-2">
            <h3 className="font-display font-bold text-base">Desafio final da trilha</h3>
            <p className="text-xs text-muted-foreground mt-1">
              10 perguntas cruzando todos os tópicos. Passe com ≥7 pra conquistar o título de Mestre.
            </p>
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
  )
}

function MapSkeleton() {
  return (
    <div className="space-y-2">
      {[1, 2, 3, 4].map((i) => (
        <div key={i} className="flex gap-4">
          <Skeleton className="h-10 w-10 shrink-0 rounded-full" />
          <Skeleton className="h-24 flex-1" />
        </div>
      ))}
    </div>
  )
}
