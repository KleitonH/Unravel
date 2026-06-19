// NAVI Mercador (NPC da loja) — pelagem dourada sombreada, avental marrom,
// boina musgo, óculos meia-lua, olhos fechados em sorriso ^_^.
// PR 63d — redesign detalhado alinhado ao Navi (gradientes + tufos + peito felpudo).

import { useId } from "react"

export function NaviMerchant({ size = 180, style }: { size?: number; style?: React.CSSProperties }) {
  const uid = useId().replace(/:/g, "")
  const gFur = `mf-${uid}`, gFace = `mc-${uid}`, gChest = `mh-${uid}`, gApron = `ma-${uid}`
  const ear = "#f0c98a"
  const beret = "#5a7340"

  return (
    <svg width={size} height={size * 1.05} viewBox="0 0 200 210" fill="none" style={style}>
      <defs>
        <linearGradient id={gFur} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#e6b85a" /><stop offset="50%" stopColor="#d4a045" /><stop offset="100%" stopColor="#b07f30" />
        </linearGradient>
        <radialGradient id={gFace} cx="50%" cy="38%" r="66%">
          <stop offset="0%" stopColor="#eec074" /><stop offset="62%" stopColor="#d4a045" /><stop offset="100%" stopColor="#b07f30" />
        </radialGradient>
        <radialGradient id={gChest} cx="50%" cy="40%" r="64%">
          <stop offset="0%" stopColor="#f6dca0" /><stop offset="100%" stopColor="#e6c074" />
        </radialGradient>
        <linearGradient id={gApron} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#6e4327" /><stop offset="100%" stopColor="#4a2c18" />
        </linearGradient>
      </defs>

      {/* cauda */}
      <path d="M150 152 q34 4 32 -28 q-2 -20 -18 -19 q11 7 9 21 q-3 17 -23 17 Z" fill={`url(#${gFur})`} />

      {/* pés */}
      <ellipse cx="62" cy="196" rx="15.5" ry="11" fill={`url(#${gFur})`} />
      <ellipse cx="138" cy="196" rx="15.5" ry="11" fill={`url(#${gFur})`} />
      <g stroke="#9c7028" strokeWidth="1.4" opacity="0.6" strokeLinecap="round">
        <path d="M58 199 v5 M62 200 v5 M66 199 v5" />
        <path d="M134 199 v5 M138 200 v5 M142 199 v5" />
      </g>

      {/* corpo */}
      <path d="M58 170 q0 -42 42 -42 q42 0 42 42 l3 40 l-90 0 Z" fill={`url(#${gFur})`} />

      {/* peito felpudo */}
      <path d="M74 138 q5 9 11 5 q4 9 15 7 q11 2 15 -7 q6 4 11 -5 q-3 22 -26 22 q-23 0 -26 -22 Z"
        fill={`url(#${gChest})`} opacity="0.9" />

      {/* avental */}
      <path d="M70 152 q30 -10 60 0 l5 58 l-70 0 Z" fill={`url(#${gApron})`} />
      <path d="M70 152 q18 -6 30 -4 l-2 62 l-30 0 Z" fill="#7d4e2c" opacity="0.45" />
      <rect x="84" y="178" width="32" height="22" rx="4" fill="#7d4e2c" stroke="#4a2c18" strokeWidth="1.5" />
      <path d="M88 184 h24 M88 190 h24" stroke="#4a2c18" strokeWidth="1.2" opacity="0.6" />
      <path d="M84 134 l8 18 M116 134 l-8 18" stroke="#4a2c18" strokeWidth="4" strokeLinecap="round" />

      {/* orelhas */}
      <path d="M61 62 L55 22 L95 47 Z" fill={`url(#${gFur})`} />
      <path d="M139 62 L145 22 L105 47 Z" fill={`url(#${gFur})`} />
      <path d="M65 55 L62 32 L86 47 Z" fill={ear} />
      <path d="M135 55 L138 32 L114 47 Z" fill={ear} />
      <path d="M65 55 L62 32 L75 41 Z" fill="#fff" opacity="0.22" />
      <path d="M135 55 L138 32 L125 41 Z" fill="#fff" opacity="0.22" />

      {/* cabeça */}
      <ellipse cx="100" cy="80" rx="45" ry="41" fill={`url(#${gFace})`} />
      <path d="M55 86 q-11 1 -14 8 q7 -1 9 2 q-6 3 -7 9 q8 -2 12 1 q-3 -10 0 -20 Z" fill={`url(#${gFace})`} />
      <path d="M145 86 q11 1 14 8 q-7 -1 -9 2 q6 3 7 9 q-8 -2 -12 1 q3 -10 0 -20 Z" fill={`url(#${gFace})`} />

      {/* bigodes */}
      <g stroke="#8a6322" strokeWidth="1.5" strokeLinecap="round" opacity="0.6" fill="none">
        <path d="M60 90 q-12 -3 -20 -1" /><path d="M60 96 q-12 1 -19 4" />
        <path d="M140 90 q12 -3 20 -1" /><path d="M140 96 q12 1 19 4" />
      </g>

      {/* sobrancelhas + olhos fechados (^_^) */}
      <g stroke="#3a2810" strokeWidth="3.4" strokeLinecap="round" fill="none">
        <path d="M75 74 q9 -8 17 0" />
        <path d="M108 74 q9 -8 17 0" />
      </g>

      {/* óculos meia-lua dourados */}
      <g stroke="#e0a615" strokeWidth="2.8" fill="none" strokeLinecap="round">
        <path d="M73 90 a14 11 0 0 0 25 0" />
        <path d="M102 90 a14 11 0 0 0 25 0" />
        <path d="M98 90 q2 -3 4 0" />
        <path d="M73 90 l-9 -2" />
        <path d="M127 90 l9 -2" />
      </g>

      {/* nariz + sorriso */}
      <path d="M94 90 q6 -3 12 0 q-1 5 -6 7 q-5 -2 -6 -7 Z" fill="#d97f5a" />
      <path d="M89 100 q11 10 22 0" stroke="#3a2810" strokeWidth="2.6" fill="none" strokeLinecap="round" />

      {/* boina */}
      <g transform="rotate(-12 96 44)">
        <ellipse cx="96" cy="46" rx="39" ry="18" fill="#4c6334" />
        <ellipse cx="96" cy="42" rx="39" ry="16" fill={beret} />
        <ellipse cx="96" cy="40" rx="32" ry="11" fill="#6b8a4c" opacity="0.8" />
        <path d="M70 40 q26 -14 52 0" stroke="#4c6334" strokeWidth="2" fill="none" opacity="0.6" />
        <circle cx="100" cy="28" r="4.5" fill="#4c6334" />
      </g>
    </svg>
  )
}
