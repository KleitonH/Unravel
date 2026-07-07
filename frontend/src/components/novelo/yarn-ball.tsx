import { cn } from "@/lib/utils"

/**
 * Novelo de lã em SVG (Ideia 1 — representação visual animada).
 *
 * O anel externo mostra o progresso da meta do dia; conforme enche, o novelo
 * "desenrola". Gira devagar e ganha um brilho pulsante quando é a vez do dono
 * (`active`). Muda de cor quando enrolado/caído.
 */
export function YarnBall({
  pct,
  active = false,
  tangled = false,
  dropped = false,
  size = 60,
}: {
  pct:      number   // 0..100 — progresso da meta do dia
  active?:  boolean
  tangled?: boolean
  dropped?: boolean
  size?:    number
}) {
  const r = 40
  const c = 2 * Math.PI * r
  const off = c * (1 - Math.min(1, Math.max(0, pct / 100)))
  const bodyClass = dropped ? "fill-destructive" : tangled ? "fill-warning" : "fill-primary"
  const ringClass = dropped ? "stroke-destructive" : tangled ? "stroke-warning" : "stroke-primary"

  return (
    <div className="relative" style={{ width: size, height: size }}>
      {active && <div className="absolute inset-1 rounded-full bg-primary/30 blur-md animate-pulse" />}
      <svg viewBox="0 0 100 100" className="relative h-full w-full">
        {/* trilho do anel */}
        <circle cx="50" cy="50" r={r} className="fill-none stroke-border" strokeWidth="7" />
        {/* progresso da meta do dia */}
        <circle
          cx="50" cy="50" r={r}
          className={cn("fill-none", ringClass)}
          strokeWidth="7" strokeLinecap="round"
          strokeDasharray={c} strokeDashoffset={off}
          transform="rotate(-90 50 50)"
          style={{ transition: "stroke-dashoffset .6s ease" }}
        />
        {/* corpo do novelo (gira devagar quando ativo) */}
        <g
          className={cn(active && "animate-[spin_16s_linear_infinite]")}
          style={{ transformBox: "fill-box", transformOrigin: "center" }}
        >
          <circle cx="50" cy="50" r="27" className={bodyClass} />
          <g className="stroke-white/45" strokeWidth="2.2" fill="none" strokeLinecap="round">
            <ellipse cx="50" cy="50" rx="27" ry="12" transform="rotate(25 50 50)" />
            <ellipse cx="50" cy="50" rx="27" ry="12" transform="rotate(-25 50 50)" />
            <ellipse cx="50" cy="50" rx="12" ry="27" transform="rotate(20 50 50)" />
            <path d="M32 42 Q50 34 68 44" />
            <path d="M32 58 Q50 66 68 56" />
          </g>
          {/* pontinha de linha */}
          <path d="M74 58 q10 6 6 16" className="stroke-white/50" strokeWidth="2.2" fill="none" strokeLinecap="round" />
        </g>
      </svg>
    </div>
  )
}
