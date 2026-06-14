// NAVI — mascote do Unravel como SVG paramétrico inline (port do protótipo
// loja-cosmetica-v3). Gato preto, óculos redondos escuros com olhos verdes,
// orelhas/nariz salmão, camiseta roxa com pata. Cosméticos (hat/accessory/fur)
// e mood são overlays, pra loja prever ao vivo.
//
// PR 63b. O NAVI final será mais detalhado; este SVG é a base funcional e o
// ponto único a trocar quando os assets caprichados chegarem.

export type NaviFur = "preto" | "cinza" | "laranja" | "branco" | "dourado"
export type NaviMood = "neutral" | "happy" | "sad" | "excited"

export const NAVI_FUR: Record<NaviFur, { body: string; shade: string; ear: string }> = {
  preto:   { body: "#1c1822", shade: "#15121b", ear: "#f4a6a0" },
  cinza:   { body: "#6b7280", shade: "#565d68", ear: "#f4a6a0" },
  laranja: { body: "#f59e0b", shade: "#d8860a", ear: "#ffd9b3" },
  branco:  { body: "#e9e6f2", shade: "#d2cee0", ear: "#f6b6b0" },
  dourado: { body: "#fbbf24", shade: "#e0a615", ear: "#fff0c2" },
}

export const NAVI_HATS = ["cartola", "bone", "headset", "antenas", "coroa"] as const
export const NAVI_ACCS = ["gravata", "jaleco", "capa", "mochila"] as const

export type NaviProps = {
  size?:      number
  fur?:       NaviFur | string | null
  hat?:       string | null
  accessory?: string | null
  mood?:      NaviMood
  style?:     React.CSSProperties
}

export function Navi({ size = 160, fur = "preto", hat = null, accessory = null, mood = "neutral", style }: NaviProps) {
  const f = NAVI_FUR[(fur ?? "preto") as NaviFur] || NAVI_FUR.preto
  const shirt = "#5a3a8f"
  const shirtHi = "#6e47a8"
  const eyeGreen = "#3ee07a"

  const Eyes = () => {
    if (mood === "happy") {
      return (
        <g stroke="#0e0a1e" strokeWidth="4" strokeLinecap="round" fill="none">
          <path d="M74 80 q7 -9 14 0" />
          <path d="M112 80 q7 -9 14 0" />
        </g>
      )
    }
    if (mood === "sad") {
      return (
        <g>
          <ellipse cx="81" cy="82" rx="7" ry="8" fill={eyeGreen} />
          <ellipse cx="119" cy="82" rx="7" ry="8" fill={eyeGreen} />
          <circle cx="83" cy="80" r="2.4" fill="#0e0a1e" />
          <circle cx="121" cy="80" r="2.4" fill="#0e0a1e" />
          <path d="M120 92 q3 8 -1 15" stroke="#7fd4ff" strokeWidth="3.5" fill="none" strokeLinecap="round" />
        </g>
      )
    }
    if (mood === "excited") {
      return (
        <g>
          <ellipse cx="81" cy="80" rx="9.5" ry="11" fill={eyeGreen} />
          <ellipse cx="119" cy="80" rx="9.5" ry="11" fill={eyeGreen} />
          <circle cx="84" cy="77" r="3" fill="#fff" />
          <circle cx="122" cy="77" r="3" fill="#fff" />
          <circle cx="79" cy="83" r="1.6" fill="#0e0a1e" />
          <circle cx="117" cy="83" r="1.6" fill="#0e0a1e" />
        </g>
      )
    }
    return (
      <g>
        <ellipse cx="81" cy="81" rx="8" ry="9.5" fill={eyeGreen} />
        <ellipse cx="119" cy="81" rx="8" ry="9.5" fill={eyeGreen} />
        <circle cx="82" cy="80" r="2.6" fill="#0e0a1e" />
        <circle cx="120" cy="80" r="2.6" fill="#0e0a1e" />
        <circle cx="84.5" cy="77.5" r="1.6" fill="#fff" />
        <circle cx="122.5" cy="77.5" r="1.6" fill="#fff" />
      </g>
    )
  }

  const Mouth = () => {
    if (mood === "happy" || mood === "excited")
      return <path d="M92 99 q8 11 16 0" stroke="#0e0a1e" strokeWidth="3" fill="#f4a6a0" strokeLinejoin="round" />
    if (mood === "sad")
      return <path d="M93 103 q7 -7 14 0" stroke="#0e0a1e" strokeWidth="3" fill="none" strokeLinecap="round" />
    return (
      <g stroke="#0e0a1e" strokeWidth="2.6" fill="none" strokeLinecap="round">
        <path d="M100 96 v5" />
        <path d="M100 101 q-5 4 -9 1" />
        <path d="M100 101 q5 4 9 1" />
      </g>
    )
  }

  return (
    <svg width={size} height={size} viewBox="0 0 200 210" style={style} fill="none">
      {accessory === "capa" && (
        <path d="M60 120 q40 26 80 0 l14 70 q-54 22 -108 0 Z" fill="#e0405b" opacity="0.95" />
      )}
      <path d="M150 168 q34 -6 30 -42 q-2 -22 -20 -22 q12 8 9 26 q-3 22 -28 24 Z" fill={f.shade} />
      <ellipse cx="80" cy="196" rx="15" ry="10" fill={f.shade} />
      <ellipse cx="120" cy="196" rx="15" ry="10" fill={f.shade} />
      <path d="M62 150 q0 -34 38 -34 q38 0 38 34 l4 40 q-42 16 -84 0 Z" fill={f.body} />
      <path d="M64 150 q0 -30 36 -30 q36 0 36 30 l3 36 q-39 14 -78 0 Z" fill={shirt} />
      <path d="M64 150 q0 -30 36 -30 q12 0 22 6 l-2 56 q-30 6 -53 -6 Z" fill={shirtHi} opacity="0.45" />
      {accessory === "jaleco" && (
        <g>
          <path d="M64 150 q0 -30 36 -30 q36 0 36 30 l3 36 q-39 14 -78 0 Z" fill="#f4f2fb" />
          <path d="M100 122 l-12 8 l12 64 l12 -64 Z" fill="#dcd8ea" />
          <path d="M100 122 v72" stroke="#c4bfd6" strokeWidth="1.5" />
          <circle cx="100" cy="150" r="2" fill="#a59fc8" />
          <circle cx="100" cy="166" r="2" fill="#a59fc8" />
        </g>
      )}
      {accessory !== "jaleco" && (
        <g fill="#fff" opacity="0.95">
          <ellipse cx="100" cy="156" rx="8" ry="7" />
          <circle cx="92" cy="146" r="3" />
          <circle cx="100" cy="143" r="3" />
          <circle cx="108" cy="146" r="3" />
        </g>
      )}
      {accessory === "mochila" && (
        <g fill="#38db8c">
          <path d="M82 124 l-4 56 q5 3 9 0 l2 -54 Z" />
          <path d="M118 124 l4 56 q-5 3 -9 0 l-2 -54 Z" />
        </g>
      )}
      <ellipse cx="60" cy="158" rx="11" ry="16" transform="rotate(18 60 158)" fill={f.body} />
      <ellipse cx="140" cy="158" rx="11" ry="16" transform="rotate(-18 140 158)" fill={f.body} />
      {accessory === "gravata" && (
        <g>
          <path d="M100 120 l-16 -8 v16 Z" fill="#ed54f2" />
          <path d="M100 120 l16 -8 v16 Z" fill="#ed54f2" />
          <circle cx="100" cy="120" r="4" fill="#c23fce" />
        </g>
      )}
      <path d="M58 56 L52 18 L92 44 Z" fill={f.body} />
      <path d="M142 56 L148 18 L108 44 Z" fill={f.body} />
      <path d="M62 50 L59 30 L82 45 Z" fill={f.ear} />
      <path d="M138 50 L141 30 L118 45 Z" fill={f.ear} />
      <ellipse cx="100" cy="76" rx="46" ry="42" fill={f.body} />
      <path d="M54 86 l-10 6 l11 3 Z" fill={f.body} />
      <path d="M146 86 l10 6 l-11 3 Z" fill={f.body} />
      <g stroke={f.body === "#e9e6f2" ? "#b8b2c8" : "#cfc8e0"} strokeWidth="1.6" strokeLinecap="round" opacity="0.7">
        <path d="M58 88 l-22 -4" />
        <path d="M58 94 l-21 4" />
        <path d="M142 88 l22 -4" />
        <path d="M142 94 l21 4" />
      </g>
      <Eyes />
      <g stroke="#0e0a1e" strokeWidth="3.4" fill="none">
        <circle cx="81" cy="81" r="15" fill="rgba(126,212,255,0.10)" />
        <circle cx="119" cy="81" r="15" fill="rgba(126,212,255,0.10)" />
        <path d="M96 80 q4 -3 8 0" />
        <path d="M66 78 l-12 -5" />
        <path d="M134 78 l12 -5" />
      </g>
      <path d="M95 90 l10 0 l-5 6 Z" fill="#ef7f9b" />
      <Mouth />
      {hat === "cartola" && (
        <g>
          <ellipse cx="100" cy="36" rx="44" ry="9" fill="#0e0a1e" />
          <rect x="72" y="-2" width="56" height="40" rx="5" fill="#181226" />
          <rect x="72" y="24" width="56" height="8" fill="#a78bfa" />
        </g>
      )}
      {hat === "bone" && (
        <g>
          <path d="M58 40 q42 -42 84 0 q-42 -14 -84 0 Z" fill="#a78bfa" />
          <path d="M142 40 q22 -2 30 8 l-2 7 q-16 -7 -30 -7 Z" fill="#8b6ef0" />
          <text x="100" y="26" textAnchor="middle" fontSize="15" fontFamily="monospace" fontWeight="700" fill="#0e0a1e">{"</>"}</text>
        </g>
      )}
      {hat === "headset" && (
        <g>
          <path d="M56 70 q0 -64 88 0" stroke="#2a2444" strokeWidth="9" fill="none" strokeLinecap="round" />
          <rect x="44" y="62" width="18" height="30" rx="7" fill="#38db8c" />
          <rect x="138" y="62" width="18" height="30" rx="7" fill="#38db8c" />
          <path d="M44 88 q-12 4 -10 22" stroke="#2a2444" strokeWidth="6" fill="none" strokeLinecap="round" />
          <circle cx="34" cy="112" r="5" fill="#ed54f2" />
        </g>
      )}
      {hat === "antenas" && (
        <g stroke="#38db8c" strokeWidth="5" strokeLinecap="round" fill="none">
          <path d="M82 44 q-8 -30 -20 -38" />
          <path d="M118 44 q8 -30 20 -38" />
          <circle cx="61" cy="4" r="7" fill="#a78bfa" stroke="none" />
          <circle cx="139" cy="4" r="7" fill="#a78bfa" stroke="none" />
        </g>
      )}
      {hat === "coroa" && (
        <g>
          <path d="M66 42 L66 18 L82 32 L100 12 L118 32 L134 18 L134 42 Z" fill="#fbbf24" stroke="#e0a615" strokeWidth="2" strokeLinejoin="round" />
          <circle cx="66" cy="18" r="4" fill="#ed54f2" />
          <circle cx="100" cy="12" r="4.5" fill="#ed54f2" />
          <circle cx="134" cy="18" r="4" fill="#ed54f2" />
          <rect x="66" y="40" width="68" height="5" fill="#e0a615" />
        </g>
      )}
    </svg>
  )
}
