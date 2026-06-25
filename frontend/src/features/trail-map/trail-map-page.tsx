import { useEffect, useMemo, useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { Link, useNavigate, useParams } from "@tanstack/react-router"
import { Activity, Brain, ChevronLeft, MapPin, RefreshCw, Sparkles } from "lucide-react"
import { journeyApi } from "@/api/journey"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { UserNavi } from "@/components/navi/user-navi"
import { cn } from "@/lib/utils"
import type { TrailMapNode, UserContentStatus } from "@/types/api"

/**
 * PR 40 / repaginação 04-2026 — mapa de jornada "Arquipélago Flutuante"
 * (porta do protótipo design_handoff_jornada_trilha, variante A).
 *
 * <para>O aluno percorre os conteúdos como ilhas flutuantes num caminho
 * serpenteante (estilo Duolingo / Super Mario World), destravando
 * sequencialmente até o castelo do Boss. Cena noturna (céu, aurora,
 * estrelas, nuvens, sparkles); ilhas com plataforma 3D variada por
 * <c>contentId</c>; medalhão botão-físico; NAVI do aluno "em pé" na ilha
 * atual; caminho de pedras que acende (dourado) nos trechos concluídos.</para>
 *
 * <para>Dados via <c>journeyApi.map(trailId)</c> (React Query). Clicar numa
 * ilha desbloqueada navega pro conteúdo (<c>/contents/{id}</c>); bloqueada
 * "treme" (nega). O status volta do backend — sem loop de conclusão local.</para>
 */

/* ───────────────────────── tokens / helpers ───────────────────────── */
const HS = (v: string, a?: number) => (a == null ? `hsl(${v})` : `hsl(${v} / ${a})`)
const P = "var(--primary)", WARN = "var(--warning)", ACC = "var(--accent)",
      MUT = "var(--muted-foreground)", BORD = "var(--border)", CARD = "var(--card)",
      FG = "var(--foreground)", POP = "var(--popover)"

function useIsDesktop() {
  const [desktop, setDesktop] = useState(() =>
    typeof window !== "undefined" ? window.matchMedia("(min-width: 1024px)").matches : true)
  useEffect(() => {
    const mq = window.matchMedia("(min-width: 1024px)")
    const on = () => setDesktop(mq.matches)
    mq.addEventListener("change", on)
    return () => mq.removeEventListener("change", on)
  }, [])
  return desktop
}

type Geom = { PAD_TOP: number; STEP: number; AMP: number; NODE: number; BOSS: number; BOT: number }
function geom(desktop: boolean): Geom {
  return {
    PAD_TOP: desktop ? 78 : 64,
    STEP:    desktop ? 150 : 124,
    AMP:     desktop ? 32 : 28,   // amplitude do serpenteado (% da largura)
    NODE:    desktop ? 76 : 64,
    BOSS:    desktop ? 104 : 88,
    BOT:     70,
  }
}
const nodeX = (i: number, AMP: number) => Math.max(18, Math.min(82, 50 + AMP * Math.sin(i * 1.15)))
const nodeY = (i: number, g: Geom) => g.PAD_TOP + i * g.STEP

/* ───────────────────────── página ───────────────────────── */
export function TrailMapPage() {
  const { trailId } = useParams({ from: "/authed/jornada/$trailId" })
  const trailIdNum  = Number(trailId)
  const navigate    = useNavigate()
  const desktop     = useIsDesktop()
  const g           = geom(desktop)

  const [shakeId, setShakeId] = useState<number | null>(null)

  const mapQuery = useQuery({
    queryKey: ["trail-map", trailIdNum],
    queryFn:  () => journeyApi.map(trailIdNum),
  })

  const map            = mapQuery.data
  const nodes          = map?.nodes ?? []
  const activeNode     = nodes.find((n) => n.status === "InProgress")
                       ?? nodes.find((n) => n.status === "Available")
  const totalNodes     = nodes.length
  const completedNodes = nodes.filter((n) => n.status === "Completed").length
  const recommended    = nodes.filter((n) => n.isRecommended && n.status !== "Completed").length
  const pct            = totalNodes > 0 ? Math.round((completedNodes / totalNodes) * 100) : 0
  const bossUnlocked   = totalNodes > 0 && completedNodes === totalNodes

  const openNode = (node: TrailMapNode) => {
    if (node.status === "Locked") {
      setShakeId(node.contentId)
      setTimeout(() => setShakeId((id) => (id === node.contentId ? null : id)), 450)
      return
    }
    navigate({ to: "/contents/$contentId", params: { contentId: String(node.contentId) } })
  }

  return (
    <div className="p-6 lg:p-10 max-w-4xl mx-auto space-y-5">
      {/* HEADER */}
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
        </div>
        <div className="flex gap-2 shrink-0">
          <Button variant="ghost" size="sm" onClick={() => navigate({ to: "/trails/$trailId/mastery", params: { trailId } })}>
            <Activity className="h-4 w-4 mr-1" />Radar
          </Button>
          <Button variant="ghost" size="sm" className="text-accent hover:text-accent hover:bg-accent/10"
            onClick={() => navigate({ to: "/reinforce/$trailId", params: { trailId } })}>
            <Brain className="h-4 w-4 mr-1" />Reforçar
          </Button>
          <Button variant="outline" size="sm" onClick={() => mapQuery.refetch()} disabled={mapQuery.isFetching}>
            <RefreshCw className={cn("h-4 w-4", mapQuery.isFetching && "animate-spin")} />
          </Button>
        </div>
      </header>

      {/* HERO de progresso (com shine + chips) */}
      {map && totalNodes > 0 && (
        <div
          className="relative overflow-hidden rounded-2xl border p-4 lg:p-[14px_18px]"
          style={{ borderColor: HS(P, 0.3), background: `linear-gradient(135deg, ${HS(P, 0.14)}, ${HS(CARD)} 55%)` }}
        >
          <span className="absolute left-0 top-3 bottom-3 w-[5px] rounded-r-[4px]" style={{ background: HS(P) }} />
          <span className="pointer-events-none absolute -top-12 right-5 h-[130px] w-[150px] rounded-full" style={{ background: HS(P, 0.16), filter: "blur(40px)" }} />
          <div className="relative">
            <div className="flex items-baseline justify-between gap-2">
              <p className="text-[13px]" style={{ color: HS(MUT) }}>
                {bossUnlocked ? "Trilha completa — encare o Boss!" : "Seu progresso na trilha"}
              </p>
              <p className="font-display font-extrabold leading-none" style={{ fontSize: desktop ? 30 : 26, color: HS(P) }}>
                {pct}<span className="text-[15px]" style={{ color: HS(MUT) }}>%</span>
              </p>
            </div>
            <div className="mt-[9px] h-[9px] rounded-full overflow-hidden relative" style={{ background: HS(MUT, 0.35) }}>
              <div className="h-full rounded-full relative overflow-hidden transition-[width] duration-700"
                style={{ width: `${pct}%`, background: `linear-gradient(90deg, ${HS(P)}, ${HS(WARN)})` }}>
                <span className="absolute inset-0 w-2/5" style={{ background: "linear-gradient(100deg, transparent, rgba(255,255,255,0.35), transparent)", animation: "j-shine 2.4s ease-in-out infinite" }} />
              </div>
            </div>
            <div className="flex gap-[7px] mt-[11px] flex-wrap">
              <Chip icon="🏝️">{completedNodes}/{totalNodes} ilhas</Chip>
              {recommended > 0 && <Chip icon="✨" color={WARN}>{recommended} recomendada{recommended > 1 ? "s" : ""}</Chip>}
            </div>
          </div>
        </div>
      )}

      {mapQuery.isLoading && <MapSkeleton g={g} />}

      {mapQuery.isError && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardHeader>
            <CardTitle className="text-base text-destructive">Não foi possível carregar o mapa</CardTitle>
            <CardDescription>Verifique se a trilha existe e você está inscrito.</CardDescription>
          </CardHeader>
          <CardContent>
            <Button asChild><Link to="/dashboard"><ChevronLeft className="h-4 w-4 mr-1" />Voltar</Link></Button>
          </CardContent>
        </Card>
      )}

      {map && totalNodes === 0 && (
        <Card>
          <CardContent className="pt-10 pb-10 text-center text-muted-foreground">
            Essa trilha ainda não tem conteúdos.
          </CardContent>
        </Card>
      )}

      {map && totalNodes > 0 && (
        <IslandWorldMap
          nodes={nodes}
          g={g}
          desktop={desktop}
          activeContentId={activeNode?.contentId}
          shakeId={shakeId}
          bossUnlocked={bossUnlocked}
          onOpenNode={openNode}
          onOpenBoss={() => navigate({ to: "/boss/$trailId", params: { trailId } })}
        />
      )}

      {map && totalNodes > 0 && (
        <p className="text-center text-[11.5px]" style={{ color: HS(MUT) }}>
          Toque numa ilha pra <strong style={{ color: HS(FG) }}>estudar</strong> · o NAVI marca onde você está
        </p>
      )}
    </div>
  )
}

function Chip({ icon, children, color = FG }: { icon: string; children: React.ReactNode; color?: string }) {
  return (
    <span className="inline-flex items-center gap-[5px] rounded-full px-[11px] py-1 font-semibold text-[12.5px]"
      style={{ background: HS(POP, 0.7), border: `1px solid ${HS(BORD)}`, color: HS(color) }}>
      <span>{icon}</span>{children}
    </span>
  )
}

/* ───────────────────────── mapa ───────────────────────── */
function IslandWorldMap({
  nodes, g, desktop, activeContentId, shakeId, bossUnlocked, onOpenNode, onOpenBoss,
}: {
  nodes:           TrailMapNode[]
  g:               Geom
  desktop:         boolean
  activeContentId: number | undefined
  shakeId:         number | null
  bossUnlocked:    boolean
  onOpenNode:      (node: TrailMapNode) => void
  onOpenBoss:      () => void
}) {
  // Castelo apoiado no chão: o chão (grama) é uma faixa cheia no rodapé; o
  // castelo afunda levemente nela pra parecer "no chão".
  const GROUND   = desktop ? 88 : 68
  const bossX    = nodeX(nodes.length, g.AMP)
  const bossY    = nodeY(nodes.length, g)          // centro do castelo
  const groundTop = bossY + g.BOSS / 2 - 8         // topo da grama (base afunda 8px)
  const mapHeight = groundTop + GROUND

  const points = useMemo(() => {
    const pts = nodes.map((_, i) => ({ x: nodeX(i, g.AMP), y: nodeY(i, g) }))
    pts.push({ x: bossX, y: bossY }) // boss sempre visível
    return pts
  }, [nodes, g, bossX, bossY])

  const activeIdx = nodes.findIndex((n) => n.contentId === activeContentId)
  const naviIdx   = activeIdx >= 0 ? activeIdx : 0
  const naviPos   = points[naviIdx] ?? points[0]
  const naviNode  = nodes[naviIdx] ?? nodes[0]
  const naviStandR = (g.NODE * islandVariant(naviNode).sizeF) / 2

  return (
    <div className="relative w-full overflow-hidden rounded-3xl border" style={{ height: mapHeight, borderColor: HS(BORD) }}>
      <SceneA desktop={desktop} />
      <Ground top={groundTop} height={GROUND} />
      <PathLayer points={points} nodes={nodes} />
      {nodes.map((node, i) => (
        <IslandNode
          key={node.contentId}
          node={node}
          g={g}
          x={nodeX(i, g.AMP)}
          y={nodeY(i, g)}
          isActive={node.contentId === activeContentId}
          shake={shakeId === node.contentId}
          onOpen={onOpenNode}
        />
      ))}
      <BossCastle g={g} x={bossX} y={bossY} unlocked={bossUnlocked} onOpen={onOpenBoss} />
      {naviPos && <NaviWalker x={naviPos.x} y={naviPos.y} g={g} standR={naviStandR} />}
    </div>
  )
}

/* Faixa de chão (grama) preenchendo toda a largura no rodapé do mapa, com
 * borda superior serrilhada (blades). O castelo do Boss fica apoiado nela. */
function Ground({ top, height }: { top: number; height: number }) {
  const teeth = 40, w = 1000, step = w / teeth
  let d = `M0 18`
  for (let i = 0; i < teeth; i++) d += ` L ${i * step + step / 2} 2 L ${(i + 1) * step} 18`
  d += ` L ${w} 18 L ${w} 24 L 0 24 Z`
  return (
    <div className="absolute left-0 right-0" style={{ top, height, zIndex: 2 }}>
      {/* serrilhado da grama no topo */}
      <svg viewBox="0 0 1000 24" preserveAspectRatio="none" className="absolute left-0 w-full" style={{ top: -14, height: 18 }}>
        <path d={d} fill="#5ed080" />
      </svg>
      {/* corpo do chão */}
      <div className="absolute inset-0" style={{ background: "linear-gradient(180deg, #4cc879, #2f9a55 42%, #1f6b39)" }} />
      {/* manchas sutis de grama */}
      <svg className="absolute inset-0 h-full w-full" preserveAspectRatio="none" style={{ opacity: 0.35 }}>
        {Array.from({ length: 16 }).map((_, i) => (
          <ellipse key={i} cx={`${(i * 37) % 100}%`} cy={`${20 + ((i * 53) % 60)}%`} rx="20" ry="6" fill="#1f6b39" opacity="0.55" />
        ))}
      </svg>
    </div>
  )
}

/* ───────────────────────── cena noturna ───────────────────────── */
function SceneA({ desktop }: { desktop: boolean }) {
  const stars = useMemo(
    () => Array.from({ length: 26 }, (_, i) => ({ x: (i * 53) % 100, y: (i * 71) % 60, r: i % 4 === 0 ? 1.6 : 1 })),
    [],
  )
  const clouds = desktop
    ? [{ x: 10, y: 60, s: 0.8, dur: 30, delay: 0 }, { x: 68, y: 120, s: 1, dur: 36, delay: 2 }, { x: 40, y: 300, s: 0.7, dur: 28, delay: 0 }]
    : [{ x: 8, y: 80, s: 0.7, dur: 30, delay: 0 }, { x: 60, y: 200, s: 0.85, dur: 34, delay: 0 }]
  return (
    <div className="absolute inset-0 overflow-hidden">
      <div className="absolute inset-0" style={{ background: `linear-gradient(180deg, ${HS("var(--sky-top, 252 50% 16%)")}, ${HS("var(--sky-bot, 252 53% 9%)")})` }} />
      <div className="absolute left-1/2 -translate-x-1/2" style={{ top: "-10%", width: "120%", height: 280, background: `radial-gradient(ellipse, ${HS(P, 0.16)}, transparent 70%)`, filter: "blur(8px)" }} />
      <svg className="absolute inset-0 h-full w-full" preserveAspectRatio="none">
        {stars.map((s, i) => <circle key={i} cx={`${s.x}%`} cy={`${s.y}%`} r={s.r} fill="#fff" opacity={0.5} />)}
      </svg>
      {clouds.map((c, i) => <Cloud key={i} {...c} />)}
      <SparkleField count={desktop ? 14 : 8} />
    </div>
  )
}

function Cloud({ x, y, s = 1, dur = 26, delay = 0 }: { x: number; y: number; s?: number; dur?: number; delay?: number }) {
  return (
    <svg width={90 * s} height={44 * s} viewBox="0 0 90 44" className="absolute"
      style={{ left: `${x}%`, top: y, opacity: 1, animation: `j-cloud ${dur}s ${delay}s ease-in-out infinite alternate` }}>
      <g fill="rgba(180,170,220,0.18)">
        <ellipse cx="28" cy="28" rx="20" ry="14" />
        <ellipse cx="48" cy="22" rx="22" ry="17" />
        <ellipse cx="66" cy="29" rx="17" ry="12" />
        <rect x="20" y="28" width="52" height="13" rx="6" />
      </g>
    </svg>
  )
}

function SparkleField({ count = 10 }: { count?: number }) {
  const dots = useMemo(() => Array.from({ length: count }, () => ({
    left: Math.random() * 100, top: Math.random() * 100,
    size: 2 + Math.random() * 3, dur: 4 + Math.random() * 4, delay: Math.random() * 5,
    gold: Math.random() > 0.5,
  })), [count])
  return (
    <div className="absolute inset-0 overflow-hidden pointer-events-none">
      {dots.map((d, i) => (
        <span key={i} className="absolute rounded-full" style={{
          left: `${d.left}%`, top: `${d.top}%`, width: d.size, height: d.size,
          background: d.gold ? "rgba(250,204,21,0.8)" : "rgba(190,170,255,0.75)",
          boxShadow: d.gold ? "0 0 6px rgba(250,204,21,0.7)" : "0 0 6px rgba(167,139,250,0.6)",
          animation: `j-spark ${d.dur}s ${d.delay}s ease-in-out infinite`,
        }} />
      ))}
    </div>
  )
}

/* ───────────────────────── caminho (pedras) ───────────────────────── */
function PathLayer({ points, nodes }: { points: { x: number; y: number }[]; nodes: TrailMapNode[] }) {
  const dots: { x: number; y: number; done: boolean; key: string }[] = []
  for (let i = 0; i < points.length - 1; i++) {
    const a = points[i], b = points[i + 1]
    const done = i < nodes.length && nodes[i].status === "Completed"
    for (let d = 1; d <= 4; d++) {
      const t = d / 5
      dots.push({ x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t, done, key: `${i}-${d}` })
    }
  }
  return (
    <div className="absolute inset-0" style={{ zIndex: 1 }}>
      {dots.map((dot) => (
        <span key={dot.key} className="absolute -translate-x-1/2 -translate-y-1/2 rounded-full" style={{
          left: `${dot.x}%`, top: dot.y,
          width: dot.done ? 12 : 9, height: dot.done ? 12 : 9,
          background: dot.done ? HS(WARN) : HS(MUT, 0.3),
          boxShadow: dot.done ? `0 0 10px ${HS(WARN, 0.7)}` : "none",
        }} />
      ))}
    </div>
  )
}

/* ───────────────────────── variação de ilhas ───────────────────────── */
type Shape = "disco" | "coluna" | "duplo" | "plato" | "pico"
type Deco = "tree" | "crystal" | "mushroom" | "bush" | "rock" | "none"
const A_SHAPES: Shape[] = ["disco", "coluna", "duplo", "plato", "pico"]
const A_DECOS: Deco[]   = ["tree", "crystal", "mushroom", "bush", "rock", "none", "tree", "bush"]
function islandVariant(node: TrailMapNode) {
  const s = node.contentId
  const sizeF = [1.12, 0.9, 1.0, 0.84, 1.06, 0.94, 1.16, 0.88, 1.02][s % 9]
  const shape = A_SHAPES[(s * 2) % A_SHAPES.length]
  const deco  = A_DECOS[s % A_DECOS.length]
  return { sizeF, shape, deco, dur: 3.6 + (s % 4) * 0.6, delay: (s % 5) * 0.4 }
}

type PlatState = "gold" | "green" | "locked"
function platColors(state: PlatState) {
  if (state === "gold")   return { gHi: "#f2d96e", gLo: "#cda53a", sHi: "#8a5e34", sLo: "#5e3f22", rk: "#6b4a2c" }
  if (state === "locked") return { gHi: "#565072", gLo: "#423d5a", sHi: "#3a3550", sLo: "#2a2640", rk: "#322d48" }
  return { gHi: "#5ed080", gLo: "#2f9a55", sHi: "#8a5e34", sLo: "#5e3f22", rk: "#6b4a2c" }
}

function DecoSvg({ kind, x, y }: { kind: Deco; x: number; y: number }) {
  if (kind === "tree") return <g transform={`translate(${x} ${y})`}><rect x="-3" y="2" width="6" height="14" rx="2" fill="#7a4a23" /><circle cx="0" cy="-2" r="11" fill="#2f8f4e" /><circle cx="-7" cy="2" r="7" fill="#3aa75d" /><circle cx="7" cy="2" r="7" fill="#3aa75d" /><circle cx="0" cy="-6" r="8" fill="#46bd6c" /><circle cx="-3" cy="-8" r="2.4" fill="#6fe08a" opacity="0.7" /></g>
  if (kind === "crystal") return <g transform={`translate(${x} ${y})`}><path d="M0 -16 L7 -2 L3 12 L-3 12 L-7 -2 Z" fill="#a78bfa" /><path d="M0 -16 L7 -2 L0 -2 Z" fill="#c4b1ff" /><path d="M0 -16 L-7 -2 L0 -2 Z" fill="#8b6ef0" /><circle cx="-2" cy="-6" r="1.4" fill="#fff" opacity="0.8" /></g>
  if (kind === "mushroom") return <g transform={`translate(${x} ${y})`}><rect x="-3" y="0" width="6" height="12" rx="3" fill="#f3ead6" /><path d="M-11 0 a11 9 0 0 1 22 0 Z" fill="#e0405b" /><circle cx="-4" cy="-4" r="2.2" fill="#fff" /><circle cx="4" cy="-2" r="1.8" fill="#fff" /></g>
  if (kind === "bush") return <g transform={`translate(${x} ${y})`}><circle cx="-6" cy="4" r="7" fill="#2f8f4e" /><circle cx="6" cy="4" r="7" fill="#2f8f4e" /><circle cx="0" cy="0" r="9" fill="#3aa75d" /><circle cx="-3" cy="-3" r="2.4" fill="#6fe08a" opacity="0.7" /></g>
  if (kind === "rock") return <g transform={`translate(${x} ${y})`}><ellipse cx="0" cy="8" rx="12" ry="5" fill="#5a5570" /><path d="M-11 8 q2 -16 11 -16 q9 0 11 16 Z" fill="#6b6585" /><path d="M-11 8 q2 -16 11 -16 l0 16 Z" fill="#4a4560" /></g>
  return null
}

function IslandPlatform({ shape, state, deco, w }: { shape: Shape; state: PlatState; deco: Deco; w: number }) {
  const c = platColors(state)
  const cx = 90, gy = 60
  const grass = (rx: number, ry: number, extra?: React.ReactNode) => (
    <g>
      <ellipse cx={cx} cy={gy + 3} rx={rx} ry={ry} fill={c.gLo} />
      <ellipse cx={cx} cy={gy} rx={rx} ry={ry} fill={c.gHi} />
      <ellipse cx={cx - rx * 0.32} cy={gy - ry * 0.4} rx={rx * 0.22} ry={ry * 0.3} fill="#fff" opacity="0.22" />
      {extra}
    </g>
  )
  let soil: React.ReactNode, grassEl: React.ReactNode
  if (shape === "coluna") {
    soil = <g><path d={`M${cx - 44} ${gy} Q${cx - 32} ${gy + 92} ${cx} ${gy + 100} Q${cx + 32} ${gy + 92} ${cx + 44} ${gy} Z`} fill={c.sLo} /><path d={`M${cx - 44} ${gy} Q${cx - 34} ${gy + 70} ${cx - 6} ${gy + 88} L${cx - 6} ${gy} Z`} fill={c.sHi} opacity="0.6" /><g stroke={c.rk} strokeWidth="3" opacity="0.5" fill="none"><path d={`M${cx - 30} ${gy + 30} q14 4 30 0`} /><path d={`M${cx - 24} ${gy + 54} q12 4 24 0`} /></g></g>
    grassEl = grass(46, 18)
  } else if (shape === "duplo") {
    soil = <path d={`M${cx - 58} ${gy + 4} Q${cx - 40} ${gy + 78} ${cx} ${gy + 84} Q${cx + 40} ${gy + 78} ${cx + 58} ${gy + 4} Z`} fill={c.sLo} />
    grassEl = <g>{grass(58, 17)}<ellipse cx={cx - 32} cy={gy - 2} rx="24" ry="13" fill={c.gLo} /><ellipse cx={cx - 32} cy={gy - 5} rx="24" ry="13" fill={c.gHi} /><ellipse cx={cx} cy={gy - 4} rx="26" ry="14" fill={c.gLo} /><ellipse cx={cx} cy={gy - 7} rx="26" ry="14" fill={c.gHi} /><ellipse cx={cx + 32} cy={gy - 2} rx="24" ry="13" fill={c.gLo} /><ellipse cx={cx + 32} cy={gy - 5} rx="24" ry="13" fill={c.gHi} /></g>
  } else if (shape === "plato") {
    soil = <g><path d={`M${cx - 70} ${gy} L${cx - 58} ${gy + 40} L${cx + 58} ${gy + 40} L${cx + 70} ${gy} Z`} fill={c.sHi} /><path d={`M${cx - 58} ${gy + 40} L${cx - 44} ${gy + 78} L${cx + 44} ${gy + 78} L${cx + 58} ${gy + 40} Z`} fill={c.sLo} /><g stroke={c.rk} strokeWidth="2.5" opacity="0.45" fill="none"><path d={`M${cx - 50} ${gy + 22} h100`} /></g></g>
    grassEl = grass(72, 17)
  } else if (shape === "pico") {
    soil = <g><path d={`M${cx - 40} ${gy} L${cx - 16} ${gy + 74} L${cx} ${gy + 98} L${cx + 16} ${gy + 74} L${cx + 40} ${gy} Z`} fill={c.sLo} /><path d={`M${cx - 40} ${gy} L${cx - 16} ${gy + 74} L${cx} ${gy + 74} L${cx} ${gy} Z`} fill={c.sHi} opacity="0.55" /></g>
    grassEl = grass(40, 16)
  } else {
    soil = <g><path d={`M${cx - 62} ${gy} Q${cx - 44} ${gy + 74} ${cx} ${gy + 82} Q${cx + 44} ${gy + 74} ${cx + 62} ${gy} Z`} fill={c.sLo} /><path d={`M${cx - 62} ${gy} Q${cx - 48} ${gy + 54} ${cx - 10} ${gy + 72} L${cx - 10} ${gy} Z`} fill={c.sHi} opacity="0.55" /><ellipse cx={cx + 30} cy={gy + 40} rx="10" ry="7" fill={c.rk} opacity="0.5" /></g>
    grassEl = grass(62, 20)
  }
  const decoX = shape === "duplo" ? cx + 30 : cx + (shape === "pico" ? 22 : 34)
  return (
    <svg width={w} height={(w * 165) / 180} viewBox="0 0 180 165" style={{ display: "block", overflow: "visible" }}>
      <ellipse cx={cx} cy="150" rx={shape === "plato" ? 60 : 42} ry="8" fill="rgba(0,0,0,0.32)" />
      {soil}
      {grassEl}
      {state !== "locked" && deco !== "none" && <DecoSvg kind={deco} x={decoX} y={gy - 8} />}
    </svg>
  )
}

/* ───────────────────────── medalhão (botão) ───────────────────────── */
function NodeButton({ node, size, isActive, onOpen }: {
  node: TrailMapNode; size: number; isActive: boolean; onOpen: (n: TrailMapNode) => void
}) {
  const locked = node.status === "Locked"
  const completed = node.status === "Completed"
  const available = node.status === "Available"
  const inprog = node.status === "InProgress"
  let face: string, edge: string, ink: string
  if (completed) { face = `linear-gradient(180deg, ${HS(WARN, 0.95)}, ${HS(WARN, 0.62)})`; edge = HS(WARN); ink = HS("var(--warning-foreground)") }
  else if (isActive || inprog) { face = `linear-gradient(180deg, ${HS(P, 0.98)}, ${HS(P, 0.66)})`; edge = HS(P); ink = HS("var(--primary-foreground)") }
  else if (available) { face = `linear-gradient(180deg, ${HS(P, 0.55)}, ${HS(P, 0.26)})`; edge = HS(P, 0.75); ink = "#fff" }
  else { face = `linear-gradient(180deg, ${HS(MUT, 0.45)}, ${HS(MUT, 0.24)})`; edge = HS(BORD); ink = HS(MUT) }
  const restShadow = `0 6px 0 ${edge}, 0 11px 16px rgba(0,0,0,0.45)`
  const downShadow = `0 2px 0 ${edge}, 0 4px 8px rgba(0,0,0,0.45)`
  return (
    <button onClick={() => onOpen(node)} title={node.title}
      aria-label={`${node.title}${locked ? " (bloqueada)" : ""}`}
      style={{
        position: "relative", width: size, height: size, cursor: locked ? "not-allowed" : "pointer",
        border: `${Math.max(3, size * 0.05)}px solid ${edge}`, borderRadius: "50%",
        background: face, color: ink, padding: 0, display: "flex", alignItems: "center", justifyContent: "center",
        boxShadow: locked ? "0 4px 10px rgba(0,0,0,0.35)" : restShadow,
        transition: "transform .14s, box-shadow .14s",
      }}
      onMouseDown={(e) => { if (!locked) { e.currentTarget.style.transform = "translateY(4px)"; e.currentTarget.style.boxShadow = downShadow } }}
      onMouseUp={(e) => { if (!locked) { e.currentTarget.style.transform = "translateY(0)"; e.currentTarget.style.boxShadow = restShadow } }}
      onMouseLeave={(e) => { if (!locked) { e.currentTarget.style.transform = "translateY(0)"; e.currentTarget.style.boxShadow = restShadow } }}
    >
      {!locked && <span style={{ position: "absolute", top: size * 0.12, left: "50%", transform: "translateX(-50%)", width: size * 0.5, height: size * 0.22, borderRadius: "50%", background: "rgba(255,255,255,0.35)", filter: "blur(1px)", pointerEvents: "none" }} />}
      <span style={{ fontFamily: "var(--font-display, Syne), sans-serif", fontWeight: 800, fontSize: completed ? size * 0.5 : size * 0.46, lineHeight: 1, transform: "translateY(1px)" }}>
        {completed ? "✓" : locked ? "🔒" : node.order}
      </span>
    </button>
  )
}

/* ───────────────────────── ilha (plataforma + medalhão) ───────────────────────── */
function IslandNode({ node, x, y, g, isActive, shake, onOpen }: {
  node: TrailMapNode; x: number; y: number; g: Geom; isActive: boolean; shake: boolean; onOpen: (n: TrailMapNode) => void
}) {
  const locked = node.status === "Locked"
  const completed = node.status === "Completed"
  const pct = node.challengesRequired > 0 ? Math.min(100, Math.round((node.challengesCompleted / node.challengesRequired) * 100)) : 0
  const ring = !locked && !completed && pct > 0
  const v = islandVariant(node)
  const med = g.NODE * v.sizeF
  const platW = med * 2.05
  const state: PlatState = completed ? "gold" : locked ? "locked" : "green"
  const groupH = med * 2.4

  return (
    <div style={{ position: "absolute", left: `${x}%`, top: y, width: platW, height: groupH, transform: "translate(-50%,-50%)", zIndex: 10 }}>
      <div style={{ position: "absolute", inset: 0, animation: locked ? "none" : `j-island-float ${v.dur}s ${v.delay}s ease-in-out infinite` }}>
        {/* plataforma */}
        <div style={{ position: "absolute", left: "50%", top: "50%", transform: "translate(-50%,-14%)", width: platW, pointerEvents: "none" }}>
          <IslandPlatform shape={v.shape} state={state} deco={v.deco} w={platW} />
        </div>
        {/* medalhão */}
        <div style={{ position: "absolute", left: "50%", top: "50%", transform: "translate(-50%,-50%)", width: med, height: med, animation: shake ? "j-shake .4s" : "none" }}>
          {ring && <ProgressRing pct={pct} size={med + 12} />}
          {isActive && <span style={{ position: "absolute", inset: -2, borderRadius: "50%", border: `3px solid ${HS(P, 0.5)}`, animation: "j-ping 1.8s ease-out infinite" }} />}
          <NodeButton node={node} size={med} isActive={isActive} onOpen={onOpen} />
          {node.isRecommended && !completed && (
            <span style={{ position: "absolute", top: -8, right: -14, background: HS(WARN), color: HS("var(--warning-foreground)"), fontSize: 8.5, fontWeight: 800, padding: "2px 6px", borderRadius: 999, textTransform: "uppercase", letterSpacing: "0.04em", boxShadow: "0 2px 6px rgba(0,0,0,0.4)", animation: "j-flag-wave 2s ease-in-out infinite", transformOrigin: "left center", whiteSpace: "nowrap" }}>Hoje</span>
          )}
        </div>
      </div>
      {/* rótulo */}
      <span style={{ position: "absolute", left: "50%", top: "50%", marginTop: med * 0.62, transform: "translateX(-50%)", maxWidth: 150, textAlign: "center", fontWeight: 700, fontSize: 11.5, color: locked ? HS(MUT) : HS(FG), whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis", pointerEvents: "none", textShadow: "0 1px 3px rgba(0,0,0,0.6)" }}>{node.title}</span>
    </div>
  )
}

function ProgressRing({ pct, size, color = WARN, sw = 5 }: { pct: number; size: number; color?: string; sw?: number }) {
  const r = (size - sw) / 2, c = 2 * Math.PI * r
  return (
    <svg width={size} height={size} style={{ position: "absolute", left: "50%", top: "50%", transform: "translate(-50%,-50%) rotate(-90deg)" }}>
      <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={HS(MUT, 0.3)} strokeWidth={sw} />
      <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={HS(color)} strokeWidth={sw}
        strokeLinecap="round" strokeDasharray={c} strokeDashoffset={c * (1 - pct / 100)} style={{ transition: "stroke-dashoffset .7s ease" }} />
    </svg>
  )
}

/* ───────────────────────── castelo do boss ───────────────────────── */
function BossCastle({ x, y, g, unlocked, onOpen }: { x: number; y: number; g: Geom; unlocked: boolean; onOpen: () => void }) {
  const D = g.BOSS
  const stone = unlocked ? "#8b7fae" : "#4a4564"
  const stoneHi = unlocked ? "#a99cc9" : "#56507a"
  const roof = unlocked ? HS(WARN) : "#3a3550"
  return (
    <div style={{ position: "absolute", left: `${x}%`, top: y, width: D, height: D, transform: "translate(-50%,-50%)", zIndex: 11 }}>
      {/* sombra de contato (apoiado no chão) */}
      <div style={{ position: "absolute", left: "50%", bottom: -6, transform: "translateX(-50%)", width: D * 0.66, height: 11, background: "rgba(0,0,0,0.38)", borderRadius: "50%", filter: "blur(4px)" }} />
      {unlocked && <span style={{ position: "absolute", inset: -6, borderRadius: 20, border: `3px solid ${HS(WARN, 0.5)}`, animation: "j-ping 2s ease-out infinite" }} />}
      <button onClick={unlocked ? onOpen : undefined} disabled={!unlocked} title="Desafio final — Boss"
        style={{ position: "relative", width: "100%", height: "100%", border: "none", background: "none", cursor: unlocked ? "pointer" : "not-allowed", padding: 0 }}>
        <svg width={D} height={D} viewBox="0 0 100 100">
          <rect x="14" y="40" width="20" height="50" fill={stone} />
          <rect x="66" y="40" width="20" height="50" fill={stone} />
          <rect x="34" y="30" width="32" height="60" fill={stoneHi} />
          {[14, 22, 30].map((tx) => <rect key={tx} x={tx} y="34" width="6" height="8" fill={stone} />)}
          {[66, 74, 82].map((tx) => <rect key={tx} x={tx} y="34" width="6" height="8" fill={stone} />)}
          {[34, 42, 50, 58].map((tx) => <rect key={tx} x={tx} y="24" width="6" height="8" fill={stoneHi} />)}
          <path d="M12 40 l12 -16 l12 16 Z" fill={roof} />
          <path d="M64 40 l12 -16 l12 16 Z" fill={roof} />
          <line x1="50" y1="4" x2="50" y2="24" stroke="#6b6480" strokeWidth="2" />
          <path d="M50 6 l14 4 l-14 4 Z" fill={unlocked ? HS("var(--destructive)") : "#5a5474"} style={{ transformOrigin: "50px 8px", animation: unlocked ? "j-flag-wave 1.6s ease-in-out infinite" : "none" }} />
          <path d="M42 90 v-20 a8 8 0 0 1 16 0 v20 Z" fill="#241f3c" />
          <circle cx="50" cy="74" r="2" fill={unlocked ? HS(WARN) : "#3a3550"} />
          <circle cx="24" cy="56" r="3" fill={unlocked ? HS(WARN, 0.8) : "#2c2840"} />
          <circle cx="76" cy="56" r="3" fill={unlocked ? HS(WARN, 0.8) : "#2c2840"} />
        </svg>
        {unlocked && <span style={{ position: "absolute", top: -14, left: "50%", transform: "translateX(-50%)", fontSize: 20, animation: "j-bob-sm 2s ease-in-out infinite" }}>👑</span>}
      </button>
      <span style={{ position: "absolute", left: "50%", top: D + 2, transform: "translateX(-50%)", whiteSpace: "nowrap", background: unlocked ? HS(WARN) : HS(POP), color: unlocked ? HS("var(--warning-foreground)") : HS(MUT), fontFamily: "var(--font-display, Syne), sans-serif", fontWeight: 800, fontSize: 11, padding: "3px 12px", borderRadius: 999, textTransform: "uppercase", letterSpacing: "0.06em", boxShadow: "0 3px 8px rgba(0,0,0,0.4)" }}>Boss</span>
    </div>
  )
}

/* ───────────────────────── NAVI "Você" ───────────────────────── */
function NaviWalker({ x, y, g, standR }: { x: number; y: number; g: Geom; standR: number }) {
  const S = g.NODE * 0.72
  const r = standR || g.NODE * 0.5
  // À direita do medalhão (não tapa o número nem a fita "Hoje", que ficam à esq./topo).
  return (
    <div style={{
      position: "absolute", left: `${x}%`, top: y, transform: `translate(${r + 4}px, ${-S * 0.42}px)`,
      width: S, zIndex: 20, pointerEvents: "none",
      transition: "left .75s cubic-bezier(.5,0,.3,1), top .75s cubic-bezier(.5,0,.3,1)",
    }}>
      <div style={{ animation: "j-bob 2.4s ease-in-out infinite" }}>
        <UserNavi size={S} />
      </div>
      <span style={{ display: "block", margin: "2px auto 0", width: "fit-content", background: HS(P), color: HS("var(--primary-foreground)"), fontWeight: 800, fontSize: 9, padding: "2px 9px", borderRadius: 999, textTransform: "uppercase", letterSpacing: "0.08em", boxShadow: "0 2px 6px rgba(0,0,0,0.4)" }}>Você</span>
    </div>
  )
}

/* ───────────────────────── skeleton ───────────────────────── */
function MapSkeleton({ g }: { g: Geom }) {
  return (
    <div className="relative w-full overflow-hidden rounded-3xl border" style={{ height: 520, borderColor: HS(BORD) }}>
      {[0, 1, 2, 3].map((i) => (
        <Skeleton key={i} className="absolute -translate-x-1/2 -translate-y-1/2 rounded-full"
          style={{ left: `${nodeX(i, g.AMP)}%`, top: nodeY(i, g), width: g.NODE, height: g.NODE }} />
      ))}
    </div>
  )
}
