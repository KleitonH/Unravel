import { useState } from "react"
import { cn } from "@/lib/utils"
import type { YarnTier } from "@/types/tokens"
import { YarnBallModal } from "./yarn-ball-modal"

/**
 * PR 52 — Novelo de Lã do moderador. 5 tamanhos visuais conforme tier.
 * Versão "inline" pra mostrar em headers/cards; ao clicar, abre
 * <see cref="YarnBallModal"/> com saldo expandido + ações de earn.
 *
 * <para>Variantes:</para>
 * <list type="bullet">
 *   <item><b>size="xs"</b> — 24×24px, pra inline em badges/chips</item>
 *   <item><b>size="sm"</b> — 40×40px, header padrão</item>
 *   <item><b>size="lg"</b> — 96×96px, card do profile</item>
 *   <item><b>size="xl"</b> — 200×200px, modal expandido</item>
 * </list>
 *
 * <para>SVG inline puro (sem assets externos) — 5 variações de tier
 * controlam densidade de fios, brilho e cor base.</para>
 */
export function YarnBall({
  tier,
  balanceCm,
  displayBalance,
  size = "sm",
  interactive = true,
  className,
}: {
  tier:           YarnTier
  balanceCm:      number
  displayBalance: string
  size?:          "xs" | "sm" | "lg" | "xl"
  interactive?:   boolean
  className?:     string
}) {
  const [open, setOpen] = useState(false)

  const dims = {
    xs: 24,
    sm: 40,
    lg: 96,
    xl: 200,
  }[size]

  const showLabel = size !== "xs"

  return (
    <>
      <button
        type="button"
        onClick={interactive ? () => setOpen(true) : undefined}
        disabled={!interactive}
        className={cn(
          "inline-flex items-center gap-2 group",
          interactive && "cursor-pointer hover:opacity-90 transition-opacity",
          !interactive && "cursor-default",
          className,
        )}
        aria-label={`Saldo: ${displayBalance}`}
      >
        <YarnSvg tier={tier} size={dims} />
        {showLabel && (
          <span className={cn(
            "font-display font-bold tabular-nums",
            size === "sm" && "text-sm",
            size === "lg" && "text-lg",
            size === "xl" && "text-2xl",
            tierToTextColor(tier),
          )}>
            {displayBalance}
          </span>
        )}
      </button>

      {open && (
        <YarnBallModal
          tier={tier}
          balanceCm={balanceCm}
          displayBalance={displayBalance}
          onClose={() => setOpen(false)}
        />
      )}
    </>
  )
}

/** Cor base do novelo + cor da label. Coral pra estados saudáveis,
 *  cinza pálido pra vazio, dourado pra giant. */
function tierToColors(tier: YarnTier) {
  switch (tier) {
    case "Empty":  return { base: "#3F3D4D", thread: "#5C5A6E", glow: "transparent" }
    case "Tiny":   return { base: "#C9B89A", thread: "#A89976", glow: "transparent" }
    case "Small":  return { base: "#E3A8A4", thread: "#C28A86", glow: "rgba(227,168,164,0.25)" }
    case "Medium": return { base: "#F2A5A0", thread: "#D17C76", glow: "rgba(242,165,160,0.35)" }
    case "Giant":  return { base: "#F5C660", thread: "#D9A23C", glow: "rgba(245,198,96,0.50)" }
  }
}

function tierToTextColor(tier: YarnTier) {
  switch (tier) {
    case "Empty":  return "text-muted-foreground"
    case "Tiny":   return "text-foreground/80"
    case "Small":  return "text-foreground"
    case "Medium": return "text-foreground"
    case "Giant":  return "text-warning"   // dourado
  }
}

/**
 * SVG do novelo — 5 variações de densidade de fios.
 * Empty = circulo vazio + textura desfiada
 * Tiny  = poucos fios
 * Small = densidade média
 * Medium = denso, padrão completo
 * Giant = denso + glow + brilho extra
 */
function YarnSvg({ tier, size }: { tier: YarnTier; size: number }) {
  const colors = tierToColors(tier)
  const r      = 45      // raio do novelo na viewBox 100x100
  const cx     = 50
  const cy     = 50

  // Densidade de fios baseado no tier
  const threadCount = tier === "Empty" ? 0
                    : tier === "Tiny"  ? 4
                    : tier === "Small" ? 8
                    : tier === "Medium"? 14
                    :                    20    // Giant

  // Gera linhas-fio aleatórias mas determinísticas (mesma seed)
  const seed = tier.charCodeAt(0)
  const threads = Array.from({ length: threadCount }, (_, i) => {
    const angle1 = ((i * 47 + seed * 13) % 360) * (Math.PI / 180)
    const angle2 = ((i * 79 + seed * 19) % 360) * (Math.PI / 180)
    return {
      x1: cx + Math.cos(angle1) * r * 0.95,
      y1: cy + Math.sin(angle1) * r * 0.95,
      x2: cx + Math.cos(angle2) * r * 0.95,
      y2: cy + Math.sin(angle2) * r * 0.95,
    }
  })

  // Empty special: novelo desfiado com fios soltos
  if (tier === "Empty") {
    return (
      <svg width={size} height={size} viewBox="0 0 100 100" aria-hidden>
        <circle cx={cx} cy={cy} r={r * 0.6} fill="none" stroke={colors.base}
                strokeWidth={1.5} strokeDasharray="2,3" opacity={0.6} />
        {/* Fios soltos saindo */}
        <path d={`M ${cx - 10} ${cy + 25} Q ${cx} ${cy + 35} ${cx + 12} ${cy + 30}`}
              fill="none" stroke={colors.thread} strokeWidth={1.5}
              strokeLinecap="round" opacity={0.5} />
        <path d={`M ${cx + 15} ${cy - 20} Q ${cx + 25} ${cy - 15} ${cx + 28} ${cy - 5}`}
              fill="none" stroke={colors.thread} strokeWidth={1.5}
              strokeLinecap="round" opacity={0.5} />
      </svg>
    )
  }

  return (
    <svg width={size} height={size} viewBox="0 0 100 100" aria-hidden>
      {/* Glow externo (Giant) */}
      {tier === "Giant" && (
        <circle cx={cx} cy={cy} r={r * 1.15} fill={colors.glow} />
      )}

      {/* Corpo do novelo (esfera) */}
      <defs>
        <radialGradient id={`yarn-${tier}-grad`} cx="35%" cy="30%">
          <stop offset="0%" stopColor={colors.base} stopOpacity={1} />
          <stop offset="100%" stopColor={colors.thread} stopOpacity={1} />
        </radialGradient>
      </defs>
      <circle cx={cx} cy={cy} r={r} fill={`url(#yarn-${tier}-grad)`} />

      {/* Threads (linhas que cruzam o novelo) */}
      <g clipPath={`circle(${r}px at ${cx}px ${cy}px)`}>
        {threads.map((t, i) => (
          <line key={i} x1={t.x1} y1={t.y1} x2={t.x2} y2={t.y2}
                stroke={colors.thread} strokeWidth={1.2}
                opacity={0.55} strokeLinecap="round" />
        ))}
      </g>

      {/* Highlight pra dar sensação 3D */}
      <ellipse cx={cx - r * 0.35} cy={cy - r * 0.4} rx={r * 0.25} ry={r * 0.15}
               fill="white" opacity={tier === "Giant" ? 0.5 : 0.3} />

      {/* Sparkles (Giant only) */}
      {tier === "Giant" && (
        <>
          <circle cx={cx + r * 0.7}  cy={cy - r * 0.5} r={1.5} fill="white" />
          <circle cx={cx - r * 0.8}  cy={cy + r * 0.2} r={1}   fill="white" opacity={0.8} />
          <circle cx={cx + r * 0.5}  cy={cy + r * 0.6} r={0.8} fill="white" opacity={0.7} />
        </>
      )}

      {/* Borda sutil */}
      <circle cx={cx} cy={cy} r={r} fill="none" stroke={colors.thread}
              strokeWidth={1} opacity={0.4} />
    </svg>
  )
}
