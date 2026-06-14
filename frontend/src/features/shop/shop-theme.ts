// PR 63 — constantes de tema da Loja (port do protótipo). Hex explícitos
// (a cena/SVG usam cores próprias); alinhados ao DS do app.
import type { ShopRarity } from "@/types/api"

export const T = {
  bg: "#0e0a1e", card: "#181230", popover: "#1f1839", border: "#2a2444",
  text: "#f6f4ff", muted: "#a59fc8",
  primary: "#a78bfa", primaryFg: "#0e0a1e",
  accent: "#38db8c", warning: "#facc15", danger: "#f97373", naviSad: "#ed54f2",
}

export const SANS = "'IBM Plex Sans', system-ui, sans-serif"
export const DISP = "'Syne', 'IBM Plex Sans', system-ui, sans-serif"

export type RarityInfo = { c: string; label: string; glow: boolean; shimmer?: boolean }
export const RARITY: Record<ShopRarity, RarityInfo> = {
  common:    { c: "#9ca3af", label: "Comum",     glow: false },
  rare:      { c: "#60a5fa", label: "Raro",      glow: false },
  epic:      { c: "#c084fc", label: "Épico",     glow: true },
  legendary: { c: "#fbbf24", label: "Lendário",  glow: true, shimmer: true },
  exclusive: { c: "#f472b6", label: "Exclusivo", glow: true },
}

export const CAT_TYPE: Record<string, string> =
  { chapeu: "Cabeça", acessorio: "Acessório", pelagem: "Pelagem", expressao: "Expressão" }

export const CATEGORIES = [
  { key: "tudo",      label: "Tudo",       icon: "✨" },
  { key: "chapeu",    label: "Chapéus",    icon: "🎩" },
  { key: "acessorio", label: "Acessórios", icon: "🎀" },
  { key: "pelagem",   label: "Pelagens",   icon: "🐾" },
  { key: "expressao", label: "Expressões", icon: "😸" },
]

export const priceLabel = (currency: string | null, price: number | null) =>
  currency && price != null ? (currency === "coins" ? `🪙 ${price}` : `💎 ${price}`) : "🔒"
