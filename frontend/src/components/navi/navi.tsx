// NAVI — mascote do Unravel como paper-doll por camadas (PR 63e).
//
// O renderer empilha CAMADAS por z-index (back→front). Cada camada usa:
//   • a imagem raster registrada pro `slug` (assets/<slug>.webp), se existir;
//   • senão, o desenho SVG de placeholder abaixo (estilo "storybook" do 63d).
//
// Trocar arte = soltar um arquivo em assets/ com o nome do slug. Sem código.
// Spec de canvas/estilo/pipeline: assets/README.md.

import { useId } from "react"
import { assetForSlug, zForAccessory, Z } from "./navi-assets"

export type NaviFur = "preto" | "cinza" | "laranja" | "branco" | "dourado"
export type NaviMood = "neutral" | "happy" | "sad" | "excited"

type FurPalette = {
  hi: string; base: string; lo: string
  belly: string; tuft: string; earIn: string
  stripe: string | null
  eye: string; eyeHi: string; nose: string; whisker: string
}

const FUR: Record<NaviFur, FurPalette> = {
  preto: {
    hi: "#3b3550", base: "#221d30", lo: "#13101c",
    belly: "#332c44", tuft: "#473f5e", earIn: "#f0a39c",
    stripe: null, eye: "#5fe389", eyeHi: "#b6ffd2", nose: "#ef88a0", whisker: "#d8d2e8",
  },
  cinza: {
    hi: "#9ba6b6", base: "#798493", lo: "#565e6b",
    belly: "#b3bcc8", tuft: "#a9b1be", earIn: "#f0a39c",
    stripe: null, eye: "#7fe39a", eyeHi: "#d8ffe6", nose: "#ef88a0", whisker: "#eef1f6",
  },
  laranja: {
    hi: "#ffba52", base: "#f0972a", lo: "#cf760e",
    belly: "#ffe2b4", tuft: "#ffc97a", earIn: "#ffcfae",
    stripe: "#bf640a", eye: "#6fe08a", eyeHi: "#caffda", nose: "#e8607e", whisker: "#fff3df",
  },
  branco: {
    hi: "#ffffff", base: "#f1eef8", lo: "#d4cfe3",
    belly: "#ffffff", tuft: "#ffffff", earIn: "#f4b2ac",
    stripe: null, eye: "#7fe39a", eyeHi: "#d8ffe6", nose: "#ef88a0", whisker: "#cfc8e0",
  },
  dourado: {
    hi: "#ffe9a0", base: "#fbc63a", lo: "#df9f12",
    belly: "#ffeec2", tuft: "#ffe093", earIn: "#fff0c2",
    stripe: null, eye: "#6fe08a", eyeHi: "#caffda", nose: "#e8607e", whisker: "#fff6da",
  },
}

/** Compat com consumidores antigos do formato {body,shade,ear}. */
export const NAVI_FUR: Record<NaviFur, { body: string; shade: string; ear: string }> =
  Object.fromEntries(
    (Object.keys(FUR) as NaviFur[]).map((k) => [k, { body: FUR[k].base, shade: FUR[k].lo, ear: FUR[k].earIn }]),
  ) as Record<NaviFur, { body: string; shade: string; ear: string }>

export const NAVI_HATS = ["cartola", "bone", "headset", "antenas", "coroa"] as const
export const NAVI_ACCS = ["gravata", "jaleco", "capa", "mochila"] as const

const ink = "#0e0a1e"

// ── helpers de camada ────────────────────────────────────────────────
const svgLayerStyle = (z: number): React.CSSProperties => ({
  position: "absolute", inset: 0, width: "100%", height: "100%", zIndex: z,
})

function RasterLayer({ url, z, alt }: { url: string; z: number; alt: string }) {
  return <img src={url} alt={alt} draggable={false} style={{ ...svgLayerStyle(z), objectFit: "contain" }} />
}

// ── peças SVG de placeholder (caem aqui quando não há arte raster) ────

/** Base: corpo + cabeça + orelhas + braços + cauda + focinho + bigodes.
 *  SEM rosto/óculos (são overlay) — espelha o modelo raster. */
function FurBaseSvg({ fur, z }: { fur: NaviFur; z: number }) {
  const f = FUR[fur]
  const uid = useId().replace(/:/g, "")
  const gBody = `nb-${uid}`, gFace = `nf-${uid}`, gBelly = `ny-${uid}`, gShirt = `ns-${uid}`
  const shirt = "#5a3a8f", shirtHi = "#7a52b6", shirtLo = "#432b6b"
  return (
    <svg viewBox="0 0 200 210" fill="none" style={svgLayerStyle(z)}>
      <defs>
        <linearGradient id={gBody} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={f.hi} /><stop offset="48%" stopColor={f.base} /><stop offset="100%" stopColor={f.lo} />
        </linearGradient>
        <radialGradient id={gFace} cx="50%" cy="38%" r="68%">
          <stop offset="0%" stopColor={f.hi} /><stop offset="60%" stopColor={f.base} /><stop offset="100%" stopColor={f.lo} />
        </radialGradient>
        <radialGradient id={gBelly} cx="50%" cy="40%" r="65%">
          <stop offset="0%" stopColor={f.belly} /><stop offset="100%" stopColor={f.tuft} />
        </radialGradient>
        <linearGradient id={gShirt} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={shirtHi} /><stop offset="55%" stopColor={shirt} /><stop offset="100%" stopColor={shirtLo} />
        </linearGradient>
      </defs>

      {/* cauda */}
      <g>
        <path d="M150 170 q40 -4 34 -46 q-3 -26 -24 -24 q15 9 11 30 q-4 24 -32 26 Z" fill={`url(#${gBody})`} />
        <path d="M160 100 q18 2 20 24 q-2 -14 -14 -18 Z" fill={f.tuft} opacity="0.7" />
        {f.stripe && (
          <g stroke={f.stripe} strokeWidth="4" strokeLinecap="round" opacity="0.8" fill="none">
            <path d="M156 150 q12 0 18 -8" /><path d="M159 134 q12 0 18 -8" /><path d="M164 118 q10 -1 15 -8" />
          </g>
        )}
      </g>

      {/* pés */}
      <g>
        <ellipse cx="80" cy="197" rx="16" ry="10.5" fill={`url(#${gBody})`} />
        <ellipse cx="120" cy="197" rx="16" ry="10.5" fill={`url(#${gBody})`} />
        <g stroke={f.lo} strokeWidth="1.4" opacity="0.6" strokeLinecap="round">
          <path d="M76 200 v4" /><path d="M84 200 v4" /><path d="M116 200 v4" /><path d="M124 200 v4" />
        </g>
      </g>

      {/* torso */}
      <path d="M60 152 q0 -36 40 -36 q40 0 40 36 l4 42 q-44 17 -88 0 Z" fill={`url(#${gBody})`} />
      {/* peito felpudo */}
      <path d="M72 132 q5 9 11 5 q4 9 17 7 q13 2 17 -7 q6 4 11 -5 q-3 24 -28 24 q-25 0 -28 -24 Z" fill={`url(#${gBelly})`} opacity="0.92" />
      {/* camiseta + pata */}
      <path d="M64 152 q0 -30 36 -30 q36 0 36 30 l3 38 q-39 15 -78 0 Z" fill={`url(#${gShirt})`} />
      <path d="M64 152 q0 -30 36 -30 q13 0 23 6 l-3 58 q-31 6 -55 -6 Z" fill={shirtHi} opacity="0.25" />
      <path d="M70 188 q30 12 60 0 l1 4 q-31 13 -62 0 Z" fill={shirtLo} opacity="0.5" />
      <g fill="#fff" opacity="0.96">
        <ellipse cx="100" cy="158" rx="8.5" ry="7.5" />
        <circle cx="91" cy="147" r="3.1" /><circle cx="100" cy="144" r="3.1" /><circle cx="109" cy="147" r="3.1" />
      </g>

      {/* braços + patinhas */}
      <g>
        <ellipse cx="59" cy="160" rx="11.5" ry="17" transform="rotate(18 59 160)" fill={`url(#${gBody})`} />
        <ellipse cx="141" cy="160" rx="11.5" ry="17" transform="rotate(-18 141 160)" fill={`url(#${gBody})`} />
        <ellipse cx="56" cy="174" rx="9" ry="7.5" fill={f.tuft} opacity="0.85" />
        <ellipse cx="144" cy="174" rx="9" ry="7.5" fill={f.tuft} opacity="0.85" />
      </g>

      {/* orelhas */}
      <g>
        <path d="M57 58 L50 16 L93 43 Z" fill={`url(#${gBody})`} />
        <path d="M143 58 L150 16 L107 43 Z" fill={`url(#${gBody})`} />
        <path d="M62 51 L58 28 L85 45 Z" fill={f.earIn} /><path d="M138 51 L142 28 L115 45 Z" fill={f.earIn} />
        <path d="M62 51 L58 28 L72 38 Z" fill="#fff" opacity="0.25" /><path d="M138 51 L142 28 L128 38 Z" fill="#fff" opacity="0.25" />
        <path d="M55 56 q-7 -2 -9 -8 q6 1 9 4 Z" fill={f.tuft} opacity="0.8" /><path d="M145 56 q7 -2 9 -8 q-6 1 -9 4 Z" fill={f.tuft} opacity="0.8" />
      </g>

      {/* cabeça */}
      <ellipse cx="100" cy="76" rx="47" ry="43" fill={`url(#${gFace})`} />
      <path d="M55 80 q-11 1 -15 8 q7 -1 9 2 q-7 3 -8 9 q8 -2 12 1 q-3 -10 2 -20 Z" fill={`url(#${gFace})`} />
      <path d="M145 80 q11 1 15 8 q-7 -1 -9 2 q7 3 8 9 q-8 -2 -12 1 q3 -10 -2 -20 Z" fill={`url(#${gFace})`} />

      {/* listras tabby */}
      {f.stripe && (
        <g stroke={f.stripe} strokeWidth="3.4" strokeLinecap="round" fill="none" opacity="0.85">
          <path d="M100 40 v13" /><path d="M89 44 q3 7 -1 13" /><path d="M111 44 q-3 7 1 13" />
          <path d="M62 64 q8 2 13 -2" /><path d="M138 64 q-8 2 -13 -2" />
        </g>
      )}

      {/* focinho + bigodes */}
      <ellipse cx="100" cy="93" rx="18" ry="12.5" fill={`url(#${gBelly})`} opacity="0.55" />
      <g stroke={f.whisker} strokeWidth="1.6" strokeLinecap="round" opacity="0.75" fill="none">
        <path d="M60 86 q-13 -3 -24 -1" /><path d="M60 92 q-13 1 -23 5" />
        <path d="M140 86 q13 -3 24 -1" /><path d="M140 92 q13 1 23 5" />
      </g>
    </svg>
  )
}

/** Rosto: olhos + boca (mood) + óculos + nariz — overlay sobre a base. */
function FaceSvg({ fur, mood, z }: { fur: NaviFur; mood: NaviMood; z: number }) {
  const f = FUR[fur]
  const uid = useId().replace(/:/g, "")
  const gEye = `ne-${uid}`

  const eyes = (() => {
    if (mood === "happy")
      return (
        <g stroke={ink} strokeWidth="4.2" strokeLinecap="round" fill="none">
          <path d="M73 80 q8 -10 16 0" /><path d="M111 80 q8 -10 16 0" />
        </g>
      )
    if (mood === "sad")
      return (
        <g>
          <ellipse cx="81" cy="83" rx="7.5" ry="9" fill={`url(#${gEye})`} /><ellipse cx="119" cy="83" rx="7.5" ry="9" fill={`url(#${gEye})`} />
          <circle cx="83" cy="81" r="2.6" fill={ink} /><circle cx="121" cy="81" r="2.6" fill={ink} />
          <path d="M120 93 q3.5 8 -1 16" stroke="#7fd4ff" strokeWidth="3.6" fill="none" strokeLinecap="round" />
        </g>
      )
    if (mood === "excited")
      return (
        <g>
          <ellipse cx="81" cy="80" rx="10" ry="12" fill={`url(#${gEye})`} /><ellipse cx="119" cy="80" rx="10" ry="12" fill={`url(#${gEye})`} />
          <circle cx="80" cy="84" r="2" fill={ink} /><circle cx="118" cy="84" r="2" fill={ink} />
          <circle cx="84.5" cy="76" r="3.2" fill="#fff" /><circle cx="122.5" cy="76" r="3.2" fill="#fff" />
          <circle cx="78" cy="82" r="1.3" fill="#fff" opacity="0.8" /><circle cx="116" cy="82" r="1.3" fill="#fff" opacity="0.8" />
        </g>
      )
    return (
      <g>
        <ellipse cx="81" cy="81" rx="8.4" ry="10.4" fill={`url(#${gEye})`} /><ellipse cx="119" cy="81" rx="8.4" ry="10.4" fill={`url(#${gEye})`} />
        <ellipse cx="81" cy="82" rx="3.1" ry="6.4" fill={ink} /><ellipse cx="119" cy="82" rx="3.1" ry="6.4" fill={ink} />
        <circle cx="83.6" cy="77.4" r="1.9" fill="#fff" /><circle cx="121.6" cy="77.4" r="1.9" fill="#fff" />
        <circle cx="79.5" cy="84.5" r="1" fill="#fff" opacity="0.7" /><circle cx="117.5" cy="84.5" r="1" fill="#fff" opacity="0.7" />
      </g>
    )
  })()

  const mouth = (() => {
    if (mood === "happy" || mood === "excited")
      return (
        <g>
          <path d="M91 98 q9 12 18 0" stroke={ink} strokeWidth="3" fill="#7a1f33" strokeLinejoin="round" />
          <path d="M95 101 q5 4 10 0" fill={f.nose} stroke="none" opacity="0.9" />
        </g>
      )
    if (mood === "sad")
      return <path d="M92 103 q8 -7 16 0" stroke={ink} strokeWidth="3" fill="none" strokeLinecap="round" />
    return (
      <g stroke={ink} strokeWidth="2.6" fill="none" strokeLinecap="round">
        <path d="M100 95 v5" /><path d="M100 100 q-5.5 4.5 -10 1" /><path d="M100 100 q5.5 4.5 10 1" />
      </g>
    )
  })()

  return (
    <svg viewBox="0 0 200 210" fill="none" style={svgLayerStyle(z)}>
      <defs>
        <radialGradient id={gEye} cx="42%" cy="35%" r="75%">
          <stop offset="0%" stopColor={f.eyeHi} /><stop offset="55%" stopColor={f.eye} /><stop offset="100%" stopColor="#1f9e58" />
        </radialGradient>
      </defs>
      {eyes}
      {/* óculos */}
      <g>
        <circle cx="81" cy="81" r="15.5" fill="rgba(126,212,255,0.08)" stroke={ink} strokeWidth="3.2" />
        <circle cx="119" cy="81" r="15.5" fill="rgba(126,212,255,0.08)" stroke={ink} strokeWidth="3.2" />
        <circle cx="81" cy="81" r="15.5" fill="none" stroke="#6e7bd6" strokeWidth="1" opacity="0.5" />
        <circle cx="119" cy="81" r="15.5" fill="none" stroke="#6e7bd6" strokeWidth="1" opacity="0.5" />
        <path d="M96.5 78 q3.5 -3 7 0" stroke={ink} strokeWidth="3.2" fill="none" strokeLinecap="round" />
        <path d="M65.5 78 l-12 -5" stroke={ink} strokeWidth="3.2" strokeLinecap="round" />
        <path d="M134.5 78 l12 -5" stroke={ink} strokeWidth="3.2" strokeLinecap="round" />
        <path d="M71 72 q7 -5 14 -3" stroke="#fff" strokeWidth="1.6" fill="none" strokeLinecap="round" opacity="0.55" />
        <path d="M109 72 q7 -5 14 -3" stroke="#fff" strokeWidth="1.6" fill="none" strokeLinecap="round" opacity="0.55" />
      </g>
      {/* nariz */}
      <path d="M93 87 q7 -3.5 14 0 q-1.5 6 -7 8.5 q-5.5 -2.5 -7 -8.5 Z" fill={f.nose} />
      <ellipse cx="97" cy="89" rx="2" ry="1.4" fill="#fff" opacity="0.6" />
      {mouth}
    </svg>
  )
}

function AccessorySvg({ slug, z }: { slug: string; z: number }) {
  return (
    <svg viewBox="0 0 200 210" fill="none" style={svgLayerStyle(z)}>
      {slug === "capa" && (
        <g>
          <path d="M58 118 q42 28 84 0 l16 74 q-58 24 -116 0 Z" fill="#d8324d" />
          <path d="M58 118 q42 28 84 0 l16 74 q-58 24 -116 0 Z" fill="#ff5b73" opacity="0.35" />
          <path d="M100 130 v60" stroke="#a31f38" strokeWidth="2" opacity="0.5" />
        </g>
      )}
      {slug === "jaleco" && (
        <g>
          <path d="M64 152 q0 -30 36 -30 q36 0 36 30 l3 38 q-39 15 -78 0 Z" fill="#f6f4fc" />
          <path d="M64 152 q0 -30 36 -30 q14 0 24 7 l-4 59 q-30 6 -53 -8 Z" fill="#fff" opacity="0.7" />
          <path d="M100 122 l-13 9 l13 65 l13 -65 Z" fill="#e2dded" />
          <path d="M100 122 v74" stroke="#c4bfd6" strokeWidth="1.5" />
          <circle cx="100" cy="150" r="2.2" fill="#a59fc8" /><circle cx="100" cy="167" r="2.2" fill="#a59fc8" />
        </g>
      )}
      {slug === "mochila" && (
        <g>
          <path d="M82 124 l-4 58 q5 3 9 0 l2 -56 Z" fill="#2fc77e" />
          <path d="M118 124 l4 58 q-5 3 -9 0 l-2 -56 Z" fill="#2fc77e" />
          <path d="M82 124 l-4 58 q3 2 5 0.6 l2 -56 Z" fill="#5be3a3" opacity="0.6" />
        </g>
      )}
      {slug === "gravata" && (
        <g>
          <path d="M100 120 l-17 -9 v18 Z" fill="#ed54f2" /><path d="M100 120 l17 -9 v18 Z" fill="#ed54f2" />
          <path d="M100 120 l-17 -9 v9 Z" fill="#f98bfa" opacity="0.6" /><path d="M100 120 l17 -9 v9 Z" fill="#f98bfa" opacity="0.6" />
          <circle cx="100" cy="120" r="4.2" fill="#c23fce" />
        </g>
      )}
    </svg>
  )
}

function HatSvg({ slug, z }: { slug: string; z: number }) {
  return (
    <svg viewBox="0 0 200 210" fill="none" style={svgLayerStyle(z)}>
      {slug === "cartola" && (
        <g>
          <ellipse cx="100" cy="36" rx="45" ry="9.5" fill="#0c0917" />
          <ellipse cx="100" cy="34" rx="45" ry="8" fill="#1a1430" />
          <rect x="72" y="-4" width="56" height="42" rx="6" fill="#181226" />
          <rect x="74" y="-4" width="10" height="42" rx="5" fill="#2a2444" opacity="0.7" />
          <rect x="72" y="24" width="56" height="9" rx="2" fill="#a78bfa" />
          <rect x="72" y="24" width="56" height="3" fill="#c4b1ff" opacity="0.7" />
        </g>
      )}
      {slug === "bone" && (
        <g>
          <path d="M57 41 q43 -44 86 0 q-43 -15 -86 0 Z" fill="#a78bfa" />
          <path d="M57 41 q43 -30 86 0 q-43 -10 -86 0 Z" fill="#c4b1ff" opacity="0.55" />
          <path d="M143 41 q23 -2 31 9 l-2 8 q-17 -8 -31 -8 Z" fill="#8b6ef0" />
          <circle cx="100" cy="16" r="3" fill="#7a52b6" />
          <text x="100" y="27" textAnchor="middle" fontSize="14" fontFamily="monospace" fontWeight="700" fill="#0e0a1e">{"</>"}</text>
        </g>
      )}
      {slug === "headset" && (
        <g>
          <path d="M55 70 q0 -66 90 0" stroke="#241f3c" strokeWidth="10" fill="none" strokeLinecap="round" />
          <path d="M55 70 q0 -66 90 0" stroke="#3a3358" strokeWidth="3" fill="none" strokeLinecap="round" opacity="0.6" />
          <rect x="43" y="60" width="19" height="33" rx="8" fill="#2fc77e" /><rect x="138" y="60" width="19" height="33" rx="8" fill="#2fc77e" />
          <rect x="46" y="63" width="6" height="27" rx="3" fill="#7df0b8" opacity="0.6" />
          <path d="M43 90 q-13 5 -11 24" stroke="#241f3c" strokeWidth="6" fill="none" strokeLinecap="round" />
          <circle cx="32" cy="116" r="5.5" fill="#ed54f2" /><circle cx="30" cy="114" r="2" fill="#fff" opacity="0.7" />
        </g>
      )}
      {slug === "antenas" && (
        <g>
          <path d="M82 44 q-8 -30 -20 -38" stroke="#2fc77e" strokeWidth="5" strokeLinecap="round" fill="none" />
          <path d="M118 44 q8 -30 20 -38" stroke="#2fc77e" strokeWidth="5" strokeLinecap="round" fill="none" />
          <circle cx="61" cy="4" r="7.5" fill="#a78bfa" /><circle cx="139" cy="4" r="7.5" fill="#a78bfa" />
          <circle cx="59" cy="2" r="2.6" fill="#fff" opacity="0.8" /><circle cx="137" cy="2" r="2.6" fill="#fff" opacity="0.8" />
        </g>
      )}
      {slug === "coroa" && (
        <g>
          <path d="M65 43 L65 17 L82 31 L100 10 L118 31 L135 17 L135 43 Z" fill="#fbbf24" stroke="#dd9f12" strokeWidth="2" strokeLinejoin="round" />
          <path d="M65 43 L65 17 L82 31 L100 10 L118 31 L135 17 L135 43 Z" fill="#ffe9a0" opacity="0.4" />
          <rect x="65" y="40" width="70" height="6" rx="2" fill="#dd9f12" />
          <circle cx="65" cy="17" r="4" fill="#ed54f2" /><circle cx="63.5" cy="15.5" r="1.4" fill="#fff" opacity="0.8" />
          <circle cx="100" cy="10" r="4.8" fill="#ed54f2" /><circle cx="98" cy="8" r="1.6" fill="#fff" opacity="0.8" />
          <circle cx="135" cy="17" r="4" fill="#ed54f2" /><circle cx="133.5" cy="15.5" r="1.4" fill="#fff" opacity="0.8" />
          <circle cx="100" cy="42.5" r="2.6" fill="#38db8c" />
        </g>
      )}
    </svg>
  )
}

// ── renderer paper-doll ──────────────────────────────────────────────

export type NaviProps = {
  size?:      number
  fur?:       NaviFur | string | null
  hat?:       string | null
  accessory?: string | null
  mood?:      NaviMood
  style?:     React.CSSProperties
}

type Layer = { id: string; z: number; node: React.ReactNode }

export function Navi({ size = 160, fur = "preto", hat = null, accessory = null, mood = "neutral", style }: NaviProps) {
  const furKey = (FUR[(fur ?? "preto") as NaviFur] ? fur : "preto") as NaviFur
  const layers: Layer[] = []

  // base (pelagem)
  {
    const url = assetForSlug(furKey)
    layers.push({ id: `fur:${furKey}`, z: Z.fur, node: url
      ? <RasterLayer url={url} z={Z.fur} alt={`NAVI ${furKey}`} />
      : <FurBaseSvg fur={furKey} z={Z.fur} /> })
  }

  // acessório (capa atrás; demais sobre o tronco/pescoço)
  if (accessory) {
    const z = zForAccessory(accessory)
    const url = assetForSlug(accessory)
    layers.push({ id: `acc:${accessory}`, z, node: url
      ? <RasterLayer url={url} z={z} alt={accessory} />
      : <AccessorySvg slug={accessory} z={z} /> })
  }

  // rosto (mood + óculos + nariz)
  {
    const moodUrl = assetForSlug(mood)
    layers.push({ id: `mood:${mood}`, z: Z.face, node: moodUrl
      ? <RasterLayer url={moodUrl} z={Z.face} alt={mood} />
      : <FaceSvg fur={furKey} mood={mood} z={Z.face} /> })
  }

  // chapéu
  if (hat) {
    const url = assetForSlug(hat)
    layers.push({ id: `hat:${hat}`, z: Z.hat, node: url
      ? <RasterLayer url={url} z={Z.hat} alt={hat} />
      : <HatSvg slug={hat} z={Z.hat} /> })
  }

  layers.sort((a, b) => a.z - b.z)

  return (
    <div style={{ position: "relative", width: size, height: size, ...style }}>
      {layers.map((l) => <div key={l.id} style={{ position: "absolute", inset: 0 }}>{l.node}</div>)}
    </div>
  )
}
