// PR 63 — cenário "Toca do NAVI" + efeitos gamificados (port de scene.jsx/fx.jsx).
import { useMemo } from "react"
import { Navi } from "@/components/navi/navi"
import type { ShopItem } from "@/types/api"
import { T, RARITY, SANS, DISP } from "./shop-theme"

const STARS: [number, number][] = [
  [60, 50], [140, 90], [220, 40], [300, 70], [380, 110], [470, 55],
  [560, 95], [650, 45], [720, 85], [110, 150], [330, 160], [600, 150],
]

export function ShopScene({ warm = true }: { warm?: boolean }) {
  return (
    <div style={{ position: "absolute", inset: 0, overflow: "hidden" }}>
      <svg viewBox="0 0 800 560" preserveAspectRatio="xMidYMid slice"
        style={{ position: "absolute", inset: 0, width: "100%", height: "100%" }}>
        <defs>
          <linearGradient id="wall" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#221a44" /><stop offset="55%" stopColor="#16102e" /><stop offset="100%" stopColor="#0e0a1e" />
          </linearGradient>
          <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#1d2c5e" /><stop offset="100%" stopColor="#101633" />
          </linearGradient>
          <radialGradient id="lanternGlow" cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor="rgba(250,204,21,0.55)" /><stop offset="100%" stopColor="rgba(250,204,21,0)" />
          </radialGradient>
          <linearGradient id="shelf" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#4a3320" /><stop offset="100%" stopColor="#33220f" />
          </linearGradient>
        </defs>
        <rect width="800" height="560" fill="url(#wall)" />
        {STARS.map(([x, y], i) => (
          <circle key={i} cx={x} cy={y} r={i % 3 === 0 ? 1.8 : 1.1} fill="#fff" opacity={0.5} />
        ))}
        <g>
          <rect x="592" y="48" width="150" height="150" rx="10" fill="#2a1f47" />
          <rect x="600" y="56" width="134" height="134" rx="6" fill="url(#sky)" />
          <circle cx="700" cy="96" r="22" fill="#fde68a" opacity="0.92" />
          <circle cx="690" cy="90" r="20" fill="#101633" />
          <circle cx="640" cy="150" r="1.6" fill="#fff" opacity="0.8" />
          <circle cx="665" cy="120" r="1.3" fill="#fff" opacity="0.7" />
          <circle cx="715" cy="160" r="1.4" fill="#fff" opacity="0.7" />
          <rect x="666" y="56" width="3" height="134" fill="#2a1f47" />
          <rect x="600" y="121" width="134" height="3" fill="#2a1f47" />
        </g>
        <g>
          <rect x="40" y="120" width="180" height="12" rx="3" fill="url(#shelf)" />
          <rect x="40" y="210" width="150" height="12" rx="3" fill="url(#shelf)" />
          <circle cx="70" cy="108" r="11" fill="#c084fc" opacity="0.6" />
          <rect x="96" y="92" width="13" height="24" rx="2" fill="#60a5fa" opacity="0.55" />
          <rect x="112" y="96" width="13" height="20" rx="2" fill="#38db8c" opacity="0.5" />
          <circle cx="160" cy="106" r="13" fill="#fbbf24" opacity="0.5" />
          <rect x="185" y="96" width="14" height="20" rx="2" fill="#f472b6" opacity="0.5" />
          <circle cx="70" cy="198" r="12" fill="#a78bfa" opacity="0.5" />
          <rect x="95" y="184" width="12" height="22" rx="2" fill="#facc15" opacity="0.5" />
          <rect x="110" y="188" width="12" height="18" rx="2" fill="#ed54f2" opacity="0.45" />
          <circle cx="150" cy="196" r="11" fill="#38db8c" opacity="0.45" />
        </g>
        <g>
          <line x1="400" y1="0" x2="400" y2="36" stroke="#2a2444" strokeWidth="2" />
          <ellipse cx="400" cy="100" rx="78" ry="78" fill="url(#lanternGlow)" />
          <rect x="372" y="40" width="56" height="44" rx="22" fill="#facc15" opacity="0.92" />
          <rect x="372" y="38" width="56" height="6" rx="3" fill="#b9860b" />
          <rect x="372" y="80" width="56" height="6" rx="3" fill="#b9860b" />
          <line x1="400" y1="86" x2="400" y2="96" stroke="#b9860b" strokeWidth="2" />
        </g>
      </svg>
      <Particles count={11} />
      {warm && (
        <div style={{
          position: "absolute", inset: 0, pointerEvents: "none",
          background: "radial-gradient(ellipse at 50% 42%, transparent 0%, rgba(255,180,100,0.06) 55%, rgba(0,0,0,0.42) 100%)",
        }} />
      )}
    </div>
  )
}

export function Particles({ count = 10 }: { count?: number }) {
  const dots = useMemo(() => Array.from({ length: count }, () => ({
    left: Math.random() * 100, bottom: Math.random() * 40, size: 2 + Math.random() * 3,
    dur: 5 + Math.random() * 5, delay: Math.random() * 6, gold: Math.random() > 0.5,
  })), [count])
  return (
    <div style={{ position: "absolute", inset: 0, overflow: "hidden", pointerEvents: "none" }}>
      {dots.map((d, i) => (
        <span key={i} style={{
          position: "absolute", left: `${d.left}%`, bottom: `${d.bottom}%`, width: d.size, height: d.size,
          borderRadius: "50%", background: d.gold ? "rgba(250,204,21,0.7)" : "rgba(255,255,255,0.7)",
          boxShadow: d.gold ? "0 0 6px rgba(250,204,21,0.6)" : "0 0 5px rgba(255,255,255,0.5)",
          animation: `float-up ${d.dur}s ${d.delay}s ease-in-out infinite`,
        }} />
      ))}
    </div>
  )
}

export function Counter({ style = {} }: { style?: React.CSSProperties }) {
  return (
    <div style={{
      position: "absolute", left: 0, right: 0, height: 84, ...style,
      background: "linear-gradient(180deg, #6a4226 0%, #5a3520 42%, #3a2310 100%)",
      borderTop: "3px solid #7d5230",
      boxShadow: "0 -10px 26px rgba(0,0,0,0.4), inset 0 2px 0 rgba(255,200,140,0.18)",
    }}>
      <div style={{ position: "absolute", inset: 0, opacity: 0.5, backgroundImage: "repeating-linear-gradient(90deg, transparent 0, transparent 58px, rgba(0,0,0,0.28) 58px, rgba(0,0,0,0.28) 60px)" }} />
      <div style={{ position: "absolute", top: 0, left: 0, right: 0, height: 8, background: "linear-gradient(180deg, rgba(255,210,150,0.25), transparent)" }} />
    </div>
  )
}

const CONFETTI = ["#a78bfa", "#38db8c", "#facc15", "#ed54f2", "#60a5fa", "#fbbf24"]
function Confetti({ count = 30 }: { count?: number }) {
  const pieces = useMemo(() => Array.from({ length: count }, (_, i) => ({
    left: Math.random() * 100, delay: Math.random() * 0.5, dur: 1.6 + Math.random() * 1.2,
    color: CONFETTI[i % CONFETTI.length], size: 6 + Math.random() * 7, rot: Math.random() * 360, round: Math.random() > 0.6,
  })), [count])
  return (
    <div style={{ position: "absolute", inset: 0, overflow: "hidden", pointerEvents: "none", zIndex: 2 }}>
      {pieces.map((p, i) => (
        <span key={i} style={{
          position: "absolute", top: -16, left: `${p.left}%`, width: p.size, height: p.round ? p.size : p.size * 0.5,
          background: p.color, borderRadius: p.round ? "50%" : 2, transform: `rotate(${p.rot}deg)`,
          animation: `confetti-fall ${p.dur}s ${p.delay}s ease-in forwards`,
        }} />
      ))}
    </div>
  )
}

export function RewardModal({ item, onClose }: { item: ShopItem | null; onClose: () => void }) {
  if (!item) return null
  const r = RARITY[item.rarity] ?? RARITY.common
  const naviProps: Record<string, string> = { [item.slot]: item.assetSlug }
  return (
    <div onClick={onClose} style={{
      position: "fixed", inset: 0, zIndex: 200, background: "rgba(8,5,18,0.78)", backdropFilter: "blur(4px)",
      display: "flex", alignItems: "center", justifyContent: "center", padding: 20,
    }}>
      <div onClick={(e) => e.stopPropagation()} style={{
        position: "relative", width: "100%", maxWidth: 360, background: T.card, border: `1px solid ${r.c}66`,
        borderRadius: 22, padding: "30px 26px 26px", textAlign: "center", overflow: "hidden",
        boxShadow: `0 0 60px ${r.c}55, 0 24px 60px rgba(0,0,0,0.5)`, animation: "reward-pop .55s cubic-bezier(.18,.89,.32,1.28) both",
      }}>
        <Confetti />
        <div style={{ position: "relative", zIndex: 3 }}>
          <div style={{ fontFamily: DISP, fontWeight: 800, fontSize: 24, color: T.text }}>🎉 Recompensa 🎉</div>
          <div style={{
            margin: "14px auto", width: 128, height: 128, borderRadius: "50%",
            background: `radial-gradient(circle at 50% 40%, ${r.c}33, ${T.popover})`, border: `2px solid ${r.c}`,
            display: "flex", alignItems: "center", justifyContent: "center", boxShadow: `0 0 30px ${r.c}66`,
          }}>
            <Navi size={108} mood="excited" {...naviProps} />
          </div>
          <div style={{ fontFamily: SANS, fontSize: 13, fontWeight: 700, color: r.c, letterSpacing: "0.06em", textTransform: "uppercase" }}>🎁 Cosmético adquirido</div>
          <div style={{ fontFamily: DISP, fontWeight: 800, fontSize: 21, color: T.text, margin: "4px 0 16px" }}>{item.name}</div>
          <button onClick={onClose} style={{
            width: "100%", height: 48, borderRadius: 12, border: "none", cursor: "pointer",
            background: T.primary, color: T.primaryFg, fontFamily: SANS, fontWeight: 800, fontSize: 15,
            animation: "soft-pulse 1.6s ease-in-out infinite",
          }}>Continuar</button>
        </div>
      </div>
    </div>
  )
}

export function CoinBurst({ burst }: { burst: { x: number; y: number } | null }) {
  if (!burst) return null
  return (
    <div style={{ position: "absolute", left: burst.x, top: burst.y, zIndex: 140, pointerEvents: "none" }}>
      {Array.from({ length: 8 }).map((_, i) => {
        const ang = (i / 8) * Math.PI * 2
        return (
          <span key={i} style={{
            position: "absolute", fontSize: 16,
            ["--dx" as string]: `${Math.cos(ang) * 46}px`, ["--dy" as string]: `${Math.sin(ang) * 46 - 20}px`,
            animation: "coin-fly .7s ease-out forwards",
          }}>🪙</span>
        )
      })}
    </div>
  )
}
