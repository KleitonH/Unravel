// ItemIcon — glyph geométrico do item pra card da prateleira (port do
// protótipo). Keyed pelo AssetSlug do cosmético. PR 63b.

const FUR_COLOR: Record<string, string> = {
  cinza: "#6b7280", laranja: "#f59e0b", branco: "#e9e6f2", dourado: "#fbbf24",
}

export function ItemIcon({ slug, size = 40 }: { slug: string; size?: number }) {
  const s: React.CSSProperties = { width: size, height: size, display: "block" }
  switch (slug) {
    case "cartola": return (
      <svg viewBox="0 0 40 40" style={s}><ellipse cx="20" cy="31" rx="16" ry="4" fill="#181226" />
        <rect x="9" y="8" width="22" height="22" rx="3" fill="#26203f" /><rect x="9" y="23" width="22" height="5" fill="#a78bfa" /></svg>
    )
    case "bone": return (
      <svg viewBox="0 0 40 40" style={s}><path d="M6 24 q14 -20 28 0 q-14 -7 -28 0 Z" fill="#a78bfa" />
        <path d="M34 24 q5 -1 7 3 l-1 3 q-4 -2 -8 -2 Z" fill="#8b6ef0" />
        <text x="20" y="20" textAnchor="middle" fontSize="9" fontFamily="monospace" fontWeight="700" fill="#0e0a1e">{"</>"}</text></svg>
    )
    case "headset": return (
      <svg viewBox="0 0 40 40" style={s}><path d="M8 24 q0 -18 24 0" stroke="#2a2444" strokeWidth="3.5" fill="none" strokeLinecap="round" />
        <rect x="5" y="20" width="7" height="13" rx="3" fill="#38db8c" /><rect x="28" y="20" width="7" height="13" rx="3" fill="#38db8c" />
        <path d="M5 31 q-3 1 -3 6" stroke="#2a2444" strokeWidth="2.4" fill="none" /><circle cx="2" cy="37" r="2.4" fill="#ed54f2" /></svg>
    )
    case "coroa": return (
      <svg viewBox="0 0 40 40" style={s}><path d="M7 30 L7 13 L15 21 L20 9 L25 21 L33 13 L33 30 Z" fill="#fbbf24" stroke="#e0a615" strokeWidth="1.5" strokeLinejoin="round" />
        <circle cx="20" cy="9" r="2.6" fill="#ed54f2" /><rect x="7" y="29" width="26" height="3.5" fill="#e0a615" /></svg>
    )
    case "antenas": return (
      <svg viewBox="0 0 40 40" style={s}><path d="M14 34 q-4 -16 -7 -22" stroke="#38db8c" strokeWidth="3" fill="none" strokeLinecap="round" />
        <path d="M26 34 q4 -16 7 -22" stroke="#38db8c" strokeWidth="3" fill="none" strokeLinecap="round" />
        <circle cx="6" cy="9" r="4.5" fill="#a78bfa" /><circle cx="34" cy="9" r="4.5" fill="#a78bfa" /></svg>
    )
    case "gravata": return (
      <svg viewBox="0 0 40 40" style={s}><path d="M20 20 L7 12 v16 Z" fill="#ed54f2" /><path d="M20 20 L33 12 v16 Z" fill="#ed54f2" />
        <circle cx="20" cy="20" r="4" fill="#c23fce" /></svg>
    )
    case "jaleco": return (
      <svg viewBox="0 0 40 40" style={s}><path d="M10 8 l10 4 l10 -4 l3 28 l-26 0 Z" fill="#f4f2fb" />
        <path d="M20 12 l-6 4 l6 20 l6 -20 Z" fill="#dcd8ea" /><circle cx="20" cy="22" r="1.4" fill="#a59fc8" /><circle cx="20" cy="29" r="1.4" fill="#a59fc8" /></svg>
    )
    case "mochila": return (
      <svg viewBox="0 0 40 40" style={s}><rect x="9" y="12" width="22" height="24" rx="6" fill="#38db8c" />
        <rect x="14" y="18" width="12" height="10" rx="3" fill="#0e0a1e" opacity="0.25" />
        <path d="M15 12 q5 -7 10 0" stroke="#2a8f5e" strokeWidth="3" fill="none" /></svg>
    )
    case "capa": return (
      <svg viewBox="0 0 40 40" style={s}><path d="M12 9 q8 6 16 0 l5 27 q-13 5 -26 0 Z" fill="#e0405b" />
        <path d="M12 9 q8 6 16 0 l-1 5 q-7 4 -14 0 Z" fill="#b8313f" /></svg>
    )
    case "happy": return (
      <svg viewBox="0 0 40 40" style={s}><circle cx="20" cy="20" r="15" fill="#38db8c" opacity="0.18" />
        <path d="M12 17 q4 -5 8 0 M20 17 q4 -5 8 0" stroke="#38db8c" strokeWidth="2.6" fill="none" strokeLinecap="round" />
        <path d="M13 24 q7 7 14 0" stroke="#38db8c" strokeWidth="2.6" fill="none" strokeLinecap="round" /></svg>
    )
    case "excited": return (
      <svg viewBox="0 0 40 40" style={s}><circle cx="20" cy="20" r="15" fill="#60a5fa" opacity="0.18" />
        <path d="M14 14 l1.5 3 3 .5 -2.2 2 .6 3 -2.9 -1.6 -2.9 1.6 .6 -3 -2.2 -2 3 -.5 Z" fill="#60a5fa" />
        <path d="M26 14 l1.5 3 3 .5 -2.2 2 .6 3 -2.9 -1.6 -2.9 1.6 .6 -3 -2.2 -2 3 -.5 Z" fill="#60a5fa" />
        <path d="M14 27 q6 5 12 0" stroke="#60a5fa" strokeWidth="2.4" fill="none" strokeLinecap="round" /></svg>
    )
    default: {
      const c = FUR_COLOR[slug] || "#a78bfa"
      return (
        <svg viewBox="0 0 40 40" style={s}><circle cx="20" cy="20" r="15" fill={c} opacity="0.92" />
          <ellipse cx="20" cy="24" rx="6" ry="5" fill="#fff" opacity="0.92" />
          <circle cx="14" cy="16" r="2.4" fill="#fff" opacity="0.92" /><circle cx="20" cy="14" r="2.4" fill="#fff" opacity="0.92" /><circle cx="26" cy="16" r="2.4" fill="#fff" opacity="0.92" /></svg>
      )
    }
  }
}
