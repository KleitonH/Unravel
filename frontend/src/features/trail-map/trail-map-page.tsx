import { useMemo, useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import { Activity, Brain, Check, ChevronLeft, Crown, Lock, MapPin, RefreshCw, Sparkles } from "lucide-react"
import { journeyApi } from "@/api/journey"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { NaviFace } from "@/components/navi/navi-face"
import { cn } from "@/lib/utils"
import type { TrailMapNode, UserContentStatus } from "@/types/api"

/**
 * PR 40 / repaginação 04-2026 — mapa de jornada estilo "world map"
 * (Super Mario World / Duolingo). Cada nó é um Content; destrava
 * sequencialmente conforme o aluno completa challenges.
 *
 * <para>Layout = <b>ilhas numa trilha serpenteante</b>:</para>
 * <list type="bullet">
 *   <item>Ilhas em zigue-zague (x via seno) ligadas por uma trilha
 *     pontilhada; os pontos já percorridos (até o nó atual) ficam dourados.</item>
 *   <item>Estados: ✓ concluída · ▶ atual (com NAVI do aluno em cima +
 *     anel de progresso) · número (disponível) · 🔒 bloqueada.</item>
 *   <item>Castelo do Boss ao final quando todas as ilhas regulares estão
 *     Completed.</item>
 *   <item>Clicar numa ilha desbloqueada abre o conteúdo
 *     (<c>/contents/{id}</c>); bloqueada não é clicável.</item>
 * </list>
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
    <div className="p-6 lg:p-10 max-w-2xl mx-auto space-y-6">
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

      {/* Hero de progresso — barra geral da trilha. */}
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
        <IslandWorldMap
          nodes={map.nodes}
          activeContentId={activeNode?.contentId}
          bossUnlocked={bossUnlocked}
          onOpenNode={(id) => navigate({ to: "/contents/$contentId", params: { contentId: String(id) } })}
          onOpenBoss={() => navigate({ to: "/boss/$trailId", params: { trailId } })}
        />
      )}
    </div>
  )
}

/* ───────────────────────── world map ───────────────────────── */

// Geometria do mapa. Tudo em px no eixo Y; X em % da largura (responsivo).
const PAD_TOP = 56
const STEP    = 132   // distância vertical entre ilhas
const NODE    = 76    // diâmetro da ilha
const BOSS    = 92    // diâmetro do castelo
const AMP     = 30    // amplitude do serpenteado (% da largura, em torno de 50%)

function nodeX(i: number) {
  // Seno → serpenteado tipo world map (S a cada ~5 ilhas, cruzando o centro
  // pros dois lados). Clampa em 18–82% pra não colar nas bordas.
  const x = 50 + AMP * Math.sin(i * 1.25)
  return Math.max(18, Math.min(82, x))
}
function nodeY(i: number) {
  return PAD_TOP + i * STEP
}

function IslandWorldMap({
  nodes, activeContentId, bossUnlocked, onOpenNode, onOpenBoss,
}: {
  nodes:           TrailMapNode[]
  activeContentId: number | undefined
  bossUnlocked:    boolean
  onOpenNode:      (contentId: number) => void
  onOpenBoss:      () => void
}) {
  // Pontos de todas as ilhas + (opcional) castelo do Boss no fim.
  const points = useMemo(() => {
    const pts = nodes.map((_, i) => ({ x: nodeX(i), y: nodeY(i) }))
    if (bossUnlocked) pts.push({ x: nodeX(nodes.length), y: nodeY(nodes.length) })
    return pts
  }, [nodes, bossUnlocked])

  const lastY = points.length ? points[points.length - 1].y : PAD_TOP
  const height = lastY + (bossUnlocked ? BOSS : NODE) / 2 + 56

  // Trilha pontilhada entre ilhas consecutivas. Trecho "andado" (origem
  // concluída) fica dourado; o resto, apagado.
  const trail: { x: number; y: number; done: boolean; key: string }[] = []
  for (let i = 0; i < points.length - 1; i++) {
    const a = points[i], b = points[i + 1]
    const fromCompleted = i < nodes.length && nodes[i].status === "Completed"
    const DOTS = 5
    for (let d = 1; d <= DOTS; d++) {
      const t = d / (DOTS + 1)
      trail.push({
        x: a.x + (b.x - a.x) * t,
        y: a.y + (b.y - a.y) * t,
        done: fromCompleted,
        key: `${i}-${d}`,
      })
    }
  }

  return (
    <div className="relative w-full" style={{ height }}>
      {/* fundo sutil de "mundo" */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0 -mx-2 rounded-3xl bg-gradient-to-b from-primary/[0.04] via-transparent to-primary/[0.04]"
      />

      {/* trilha pontilhada (atrás das ilhas) */}
      {trail.map((dot) => (
        <span
          key={dot.key}
          aria-hidden
          className={cn(
            "absolute z-0 h-2 w-2 -translate-x-1/2 -translate-y-1/2 rounded-full",
            dot.done ? "bg-warning/70" : "bg-muted-foreground/25",
          )}
          style={{ left: `${dot.x}%`, top: dot.y }}
        />
      ))}

      {/* ilhas */}
      {nodes.map((node, i) => (
        <Island
          key={node.contentId}
          node={node}
          x={nodeX(i)}
          y={nodeY(i)}
          isActive={activeContentId === node.contentId}
          onOpen={() => onOpenNode(node.contentId)}
        />
      ))}

      {/* castelo do Boss */}
      {bossUnlocked && (
        <BossCastle x={nodeX(nodes.length)} y={nodeY(nodes.length)} onOpen={onOpenBoss} />
      )}
    </div>
  )
}

function Island({
  node, x, y, isActive, onOpen,
}: {
  node:     TrailMapNode
  x:        number
  y:        number
  isActive: boolean
  onOpen:   () => void
}) {
  const locked    = node.status === "Locked"
  const completed = node.status === "Completed"
  const pct = node.challengesRequired > 0
    ? Math.min(100, Math.round((node.challengesCompleted / node.challengesRequired) * 100))
    : 0

  return (
    <div
      className="absolute z-10 -translate-x-1/2 -translate-y-1/2"
      style={{ left: `${x}%`, top: y, width: NODE, height: NODE }}
    >
      {/* NAVI do aluno "em pé" na ilha atual */}
      {isActive && (
        <div className="absolute left-1/2 -translate-x-1/2" style={{ top: -52 }}>
          <NaviFace size={44} />
          <span className="mt-0.5 block whitespace-nowrap rounded-full bg-primary px-2 py-0.5 text-center text-[9px] font-bold uppercase tracking-wider text-primary-foreground shadow">
            Você
          </span>
        </div>
      )}

      {/* sombra da ilha flutuante */}
      {!locked && (
        <span
          aria-hidden
          className="absolute left-1/2 -translate-x-1/2 rounded-[50%] bg-black/25 blur-[3px]"
          style={{ top: NODE - 8, width: NODE * 0.7, height: 9 }}
        />
      )}

      {/* anel de progresso (ilha atual / em progresso, com progresso parcial) */}
      {!locked && !completed && pct > 0 && (
        <ProgressRing pct={pct} />
      )}

      {/* a ilha */}
      <button
        type="button"
        onClick={locked ? undefined : onOpen}
        disabled={locked}
        title={node.title}
        aria-label={`${node.title}${locked ? " (bloqueada)" : ""}`}
        className={cn(
          "group relative grid h-full w-full place-items-center rounded-full border-[3px] font-display font-extrabold transition-all",
          locked
            ? "cursor-not-allowed border-border bg-muted/40 text-muted-foreground opacity-70"
            : "cursor-pointer shadow-lg hover:-translate-y-0.5 hover:shadow-xl active:translate-y-0",
          completed && "border-warning/60 bg-gradient-to-b from-warning/30 to-warning/10 text-warning",
          !completed && !locked && (isActive
            ? "border-primary bg-gradient-to-b from-primary/35 to-primary/15 text-primary ring-4 ring-primary/20"
            : "border-primary/50 bg-gradient-to-b from-primary/20 to-primary/5 text-primary"),
        )}
      >
        {/* anel pulsante de destaque na ilha atual */}
        {isActive && (
          <span aria-hidden className="absolute inset-0 rounded-full ring-2 ring-primary/40 animate-ping" />
        )}

        {completed ? <Check className="h-7 w-7" strokeWidth={3} />
          : locked  ? <Lock className="h-6 w-6" />
          : <span className="text-2xl">{node.order}</span>}

        {/* fita "Hoje" pra recomendados */}
        {node.isRecommended && (
          <span className="absolute -right-1 -top-1 rounded-full bg-warning px-1.5 py-0.5 text-[8px] font-bold uppercase leading-none tracking-wider text-warning-foreground shadow">
            Hoje
          </span>
        )}
      </button>

      {/* rótulo da ilha */}
      <span
        className={cn(
          "absolute left-1/2 -translate-x-1/2 max-w-[140px] truncate text-center text-[11px] font-semibold",
          locked ? "text-muted-foreground" : "text-foreground",
        )}
        style={{ top: NODE + 6 }}
      >
        {node.title}
      </span>
    </div>
  )
}

/** Anel SVG de progresso desenhado em volta da ilha. */
function ProgressRing({ pct }: { pct: number }) {
  const size = NODE + 10
  const r = (size - 6) / 2
  const c = 2 * Math.PI * r
  return (
    <svg
      className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 -rotate-90"
      width={size} height={size} aria-hidden
    >
      <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="currentColor" strokeWidth={4} className="text-muted/40" />
      <circle
        cx={size / 2} cy={size / 2} r={r} fill="none" stroke="currentColor" strokeWidth={4}
        strokeLinecap="round" className="text-warning"
        strokeDasharray={c} strokeDashoffset={c * (1 - pct / 100)}
      />
    </svg>
  )
}

function BossCastle({ x, y, onOpen }: { x: number; y: number; onOpen: () => void }) {
  return (
    <div
      className="absolute z-10 -translate-x-1/2 -translate-y-1/2"
      style={{ left: `${x}%`, top: y, width: BOSS, height: BOSS }}
    >
      <span
        aria-hidden
        className="absolute left-1/2 -translate-x-1/2 rounded-[50%] bg-black/30 blur-[3px]"
        style={{ top: BOSS - 8, width: BOSS * 0.7, height: 10 }}
      />
      <button
        type="button"
        onClick={onOpen}
        title="Desafio final da trilha"
        aria-label="Enfrentar o Boss"
        className="group relative grid h-full w-full place-items-center rounded-2xl border-[3px] border-warning/70 bg-gradient-to-b from-warning/35 to-warning/10 text-warning shadow-lg shadow-warning/20 transition-all hover:-translate-y-0.5 hover:shadow-xl"
      >
        <span aria-hidden className="absolute inset-0 rounded-2xl ring-2 ring-warning/40 animate-ping" />
        <Crown className="h-9 w-9" />
      </button>
      <span
        className="absolute left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full bg-warning px-2 py-0.5 text-center text-[10px] font-bold uppercase tracking-wider text-warning-foreground shadow"
        style={{ top: BOSS + 6 }}
      >
        Boss
      </span>
    </div>
  )
}

function MapSkeleton() {
  return (
    <div className="relative h-[560px] w-full">
      {[0, 1, 2, 3].map((i) => (
        <Skeleton
          key={i}
          className="absolute h-[76px] w-[76px] -translate-x-1/2 -translate-y-1/2 rounded-full"
          style={{ left: `${nodeX(i)}%`, top: nodeY(i) }}
        />
      ))}
    </div>
  )
}
