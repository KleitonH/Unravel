// PR 63c — Loja Cosmética "Toca do NAVI" (port fiel do protótipo, wired ao
// backend). Cena com NAVI mercador + NAVI cliente provando itens ao vivo +
// prateleira lateral. Estado de catálogo/saldo vem do servidor; estado de UI
// (seleção, preview, abas, filtros) é local.
import { useMemo, useRef, useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { toast } from "sonner"
import { shopApi } from "@/api/shop"
import { Navi } from "@/components/navi/navi"
import { NaviMerchant } from "@/components/navi/navi-merchant"
import { ItemIcon } from "@/components/navi/item-icon"
import type { ShopItem } from "@/types/api"
import { T, RARITY, SANS, DISP, CAT_TYPE, CATEGORIES, priceLabel } from "./shop-theme"
import { ShopScene, Counter, Particles, RewardModal, CoinBurst } from "./shop-scene"

const MERCHANT_IDLE = [
  "Bem-vindo à Toca! Dá uma olhada nos novos itens, viu? 🐾",
  "Sem pressa pra escolher — fica à vontade!",
  "Esses chegaram fresquinhos hoje. ✨",
]

// ── primitivos inline (port do ui.jsx) ──────────────────────────
function Badge({ children, color = T.primary, style = {} }: { children: React.ReactNode; color?: string; style?: React.CSSProperties }) {
  return <span style={{ display: "inline-flex", alignItems: "center", gap: 4, fontFamily: SANS, fontSize: 11, fontWeight: 700, lineHeight: 1, padding: "4px 9px", borderRadius: 9999, background: `${color}1f`, color, border: `1px solid ${color}66`, whiteSpace: "nowrap", ...style }}>{children}</span>
}
function FilterPill({ children, active, onClick }: { children: React.ReactNode; active: boolean; onClick: () => void }) {
  return <button onClick={onClick} style={{ fontFamily: SANS, fontSize: 12.5, fontWeight: 600, cursor: "pointer", padding: "7px 14px", borderRadius: 9999, whiteSpace: "nowrap", display: "inline-flex", alignItems: "center", gap: 5, border: `1px solid ${active ? T.primary : T.border}`, background: active ? "rgba(167,139,250,0.16)" : "transparent", color: active ? T.primary : T.muted }}>{children}</button>
}
function StatusChip({ icon, value, color }: { icon: string; value: string | number; color: string }) {
  return <div style={{ display: "inline-flex", alignItems: "center", gap: 5, padding: "5px 12px", borderRadius: 9999, background: "rgba(31,24,57,0.65)", border: `1px solid ${T.border}`, fontFamily: SANS, fontWeight: 700, fontSize: 13, color: T.text, whiteSpace: "nowrap" }}><span style={{ fontSize: 14 }}>{icon}</span><span style={{ color }}>{value}</span></div>
}
function SpeechBubble({ text, style = {} }: { text: string; style?: React.CSSProperties }) {
  return (
    <div style={{ background: T.card, border: `1px solid ${T.primary}55`, borderRadius: 14, padding: "10px 13px", maxWidth: 210, position: "relative", boxShadow: "0 8px 24px rgba(0,0,0,0.45)", ...style }}>
      <span style={{ fontFamily: DISP, fontWeight: 600, fontSize: 13, color: T.text, lineHeight: 1.4 }}>{text}</span>
      <div style={{ position: "absolute", left: -8, top: 20, width: 0, height: 0, borderTop: "7px solid transparent", borderBottom: "7px solid transparent", borderRight: `8px solid ${T.card}` }} />
    </div>
  )
}

function RarityFx({ rarity }: { rarity: ShopItem["rarity"] }) {
  if (rarity === "epic") return (
    <div style={{ position: "absolute", inset: 0, pointerEvents: "none", overflow: "hidden", borderRadius: 13 }}>
      {[0, 1, 2].map((i) => <span key={i} style={{ position: "absolute", left: `${20 + i * 28}%`, bottom: 6, width: 3, height: 3, borderRadius: "50%", background: "#c084fc", boxShadow: "0 0 6px #c084fc", animation: `float-up ${3.5 + i}s ${i * 0.7}s ease-in-out infinite` }} />)}
    </div>
  )
  if (rarity === "legendary") return (
    <div style={{ position: "absolute", inset: 0, pointerEvents: "none", overflow: "hidden", borderRadius: 13 }}>
      <div style={{ position: "absolute", top: 0, bottom: 0, width: "36%", background: "linear-gradient(100deg, transparent, rgba(251,191,36,0.28), transparent)", animation: "shimmer 3s ease-in-out infinite" }} />
    </div>
  )
  return null
}

// ── shelf card ───────────────────────────────────────────────────
function ShelfCard({ item, selected, affordable, colMode, iconRef, onEnter, onLeave, onClick, onAction }: {
  item: ShopItem; selected: boolean; affordable: boolean; colMode: boolean
  iconRef: (el: HTMLDivElement | null) => void
  onEnter: (i: ShopItem) => void; onLeave: () => void; onClick: (i: ShopItem) => void; onAction: (i: ShopItem) => void
}) {
  const [hov, setHov] = useState(false)
  const r = RARITY[item.rarity]
  const isLocked = !!item.lockedReason
  const blocked = isLocked || (!affordable && !item.owned)
  const pulse = item.rarity === "legendary" ? "card-pulse-gold 3s ease-in-out infinite"
    : item.rarity === "exclusive" ? "exclusive-breathe 4s ease-in-out infinite" : "none"
  return (
    <div onMouseEnter={() => { setHov(true); onEnter(item) }} onMouseLeave={() => { setHov(false); onLeave() }} onClick={() => onClick(item)}
      style={{
        position: "relative", display: "flex", alignItems: "center", gap: 12, flexShrink: 0, padding: "10px 12px", borderRadius: 13,
        cursor: blocked && !item.owned ? "default" : "pointer", background: T.card,
        border: selected ? `2px solid ${T.primary}` : `1px solid ${T.border}`, borderLeft: `3px solid ${selected ? T.primary : r.c}`,
        boxShadow: selected ? `0 0 20px ${T.primary}55` : hov && !blocked ? `0 4px 18px ${r.c}33` : "none",
        transform: hov && !blocked ? "scale(1.02)" : "none", opacity: blocked && !item.owned ? 0.55 : 1,
        transition: "transform .15s, box-shadow .15s, border-color .15s", animation: pulse, overflow: "hidden",
      }}>
      <RarityFx rarity={item.rarity} />
      {selected && <div style={{ position: "absolute", top: 6, right: 8, color: T.primary, fontSize: 14, fontWeight: 800, zIndex: 4 }}>✓</div>}
      {item.owned && !selected && (
        <div style={{ position: "absolute", top: 6, left: 0, zIndex: 4 }}>
          <span style={{ fontSize: 9, fontWeight: 800, color: T.primaryFg, background: T.accent, padding: "2px 7px", borderRadius: "0 7px 7px 0" }}>✓ ADQUIRIDO</span>
        </div>
      )}
      <div ref={iconRef} style={{ width: 46, height: 46, flexShrink: 0, borderRadius: 11, position: "relative", background: `radial-gradient(circle at 50% 40%, ${r.c}26, ${T.popover})`, border: `1px solid ${r.c}44`, display: "flex", alignItems: "center", justifyContent: "center" }}>
        <ItemIcon slug={item.assetSlug} size={32} />
        {blocked && <div style={{ position: "absolute", inset: 0, display: "flex", alignItems: "center", justifyContent: "center", fontSize: 18, background: "rgba(14,10,30,0.45)", borderRadius: 11 }}>🔒</div>}
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontFamily: DISP, fontWeight: 700, fontSize: 13.5, color: T.text, lineHeight: 1.2, marginTop: item.owned && !selected ? 8 : 0, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{item.name}</div>
        <div style={{ fontFamily: SANS, fontSize: 11, color: T.muted, margin: "1px 0 5px" }}>{CAT_TYPE[item.category] ?? item.category}</div>
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          {colMode ? (
            <span style={{ fontFamily: SANS, fontSize: 11.5, fontWeight: 700, color: item.equipped ? T.accent : T.muted }}>{item.equipped ? "⚡ Equipado" : "Guardado"}</span>
          ) : (
            <span style={{ fontFamily: SANS, fontWeight: 800, fontSize: 12.5, color: item.owned ? T.muted : item.currency === "stars" ? T.accent : T.warning }}>{item.owned ? "✓ Você tem" : priceLabel(item.currency, item.price)}</span>
          )}
          <span style={{ marginLeft: "auto" }}><Badge color={r.c}>{r.label}</Badge></span>
        </div>
      </div>
      {colMode && (
        <button onClick={(e) => { e.stopPropagation(); onAction(item) }} style={{ height: 32, padding: "0 12px", borderRadius: 8, border: "none", cursor: "pointer", fontFamily: SANS, fontWeight: 700, fontSize: 13, background: item.equipped ? T.popover : T.accent, color: item.equipped ? T.text : T.primaryFg }}>
          {item.equipped ? "Tirar" : "Equipar"}
        </button>
      )}
    </div>
  )
}

// ════════ PAGE ════════
export function ShopPage() {
  const qc = useQueryClient()
  const catalogQuery = useQuery({ queryKey: ["shop", "catalog"], queryFn: shopApi.catalog })

  const items = catalogQuery.data?.items ?? []
  const balance = catalogQuery.data?.balance ?? { coins: 0, stars: 0 }

  const [tab, setTab] = useState<"loja" | "colecao">("loja")
  const [cat, setCat] = useState("tudo")
  const [colFilter, setColFilter] = useState<"todos" | "equipados" | "nao">("todos")
  const [hoverId, setHoverId] = useState<number | null>(null)
  const [selected, setSelected] = useState<number | null>(null)
  const [ba, setBa] = useState<"antes" | "depois">("depois")
  const [flashMood, setFlashMood] = useState<"neutral" | "happy" | "sad" | "excited">("neutral")
  const [say, setSay] = useState(MERCHANT_IDLE[0])
  const [reward, setReward] = useState<ShopItem | null>(null)
  const [burst, setBurst] = useState<{ x: number; y: number } | null>(null)
  const [mFab, setMFab] = useState(false)
  const stageRef = useRef<HTMLDivElement>(null)

  const selItem = items.find((i) => i.id === selected) ?? null
  const hovItem = items.find((i) => i.id === hoverId) ?? null

  const equippedItems = items.filter((i) => i.owned && i.equipped)
  const naviLook = useMemo(() => {
    const base: { fur: string; hat: string | null; accessory: string | null } = { fur: "preto", hat: null, accessory: null }
    let mood = "neutral"
    const apply = (it: ShopItem) => { if (it.slot === "mood") mood = it.assetSlug; else (base as Record<string, string | null>)[it.slot] = it.assetSlug }
    equippedItems.forEach(apply)
    if (selItem && ba === "depois") apply(selItem)
    if (hovItem) apply(hovItem)
    return { ...base, mood }
  }, [equippedItems, selItem, hovItem, ba])
  const effMood = (flashMood !== "neutral" ? flashMood : naviLook.mood) as "neutral" | "happy" | "sad" | "excited"

  const flash = (m: typeof flashMood, dur = 1400) => { setFlashMood(m); setTimeout(() => setFlashMood("neutral"), dur) }
  const afford = (i: ShopItem) => i.price != null && (i.currency === "stars" ? balance.stars : balance.coins) >= i.price

  const invalidate = () => { qc.invalidateQueries({ queryKey: ["shop", "catalog"] }); qc.invalidateQueries({ queryKey: ["profile"] }) }

  const buyMut = useMutation({
    mutationFn: (id: number) => shopApi.buy(id),
    onSuccess: (_res, id) => {
      const it = items.find((i) => i.id === id) ?? null
      setReward(it); setSay("Boa escolha! Combinou demais com você! 🎉"); flash("excited", 2600)
      invalidate()
    },
    onError: (err: unknown) => {
      const status = (err as { response?: { status?: number } })?.response?.status
      if (status === 402) { setSay("Hmm, falta um pouquinho de moeda... 🪙"); flash("sad", 900) }
      else toast.error("Não deu pra comprar agora.")
    },
  })
  const equipMut = useMutation({ mutationFn: (id: number) => shopApi.equip(id), onSuccess: invalidate })
  const unequipMut = useMutation({ mutationFn: (id: number) => shopApi.unequip(id), onSuccess: invalidate })

  const onCardClick = (item: ShopItem) => {
    if (item.lockedReason) { setSay("Esse é recompensa de evento — fica de olho! 🎁"); return }
    setSelected(item.id); setBa("depois"); setHoverId(null)
    setSay(["epic", "legendary", "exclusive", "rare"].includes(item.rarity) ? "Oh! Esse aí é especial mesmo! ✨" : "Boa escolha! Experimenta aí. 😺")
  }
  const buy = (item: ShopItem, e?: React.MouseEvent) => {
    if (!afford(item)) { flash("sad", 900); setSay("Hmm, falta um pouquinho de moeda... 🪙"); return }
    if (e && stageRef.current) {
      const host = stageRef.current.getBoundingClientRect()
      const bb = (e.currentTarget as HTMLElement).getBoundingClientRect()
      setBurst({ x: bb.left - host.left + bb.width / 2, y: bb.top - host.top }); setTimeout(() => setBurst(null), 750)
    }
    buyMut.mutate(item.id)
  }

  // listas
  let shopList = items.filter((i) => (cat === "tudo" ? true : i.category === cat))
  shopList = [...shopList] // estável
  const colList = items.filter((i) => i.owned).filter((i) => colFilter === "equipados" ? i.equipped : colFilter === "nao" ? !i.equipped : true)
  const iconRefs = useRef<Record<number, HTMLDivElement | null>>({})

  // ── shelf ──
  const shelf = () => {
    const list = tab === "loja" ? shopList : colList
    const empty = tab === "colecao" && list.length === 0
    return (
      <div style={{ display: "flex", flexDirection: "column", height: "100%", minHeight: 0 }}>
        <div style={{ padding: "16px 16px 12px", display: "flex", flexDirection: "column", gap: 10, flexShrink: 0, borderBottom: `1px solid ${T.border}` }}>
          <div style={{ display: "inline-flex", background: T.popover, border: `1px solid ${T.border}`, borderRadius: 12, padding: 4, gap: 4, alignSelf: "flex-start" }}>
            {([["loja", "🛍️ Loja Base"], ["colecao", "🎒 Coleção"]] as const).map(([v, label]) => (
              <button key={v} onClick={() => { setTab(v); setSelected(null); setSay(v === "colecao" ? "Sua coleção tá ficando bonita! 😻" : MERCHANT_IDLE[0]) }}
                style={{ border: "none", cursor: "pointer", fontFamily: SANS, fontSize: 13, fontWeight: 700, padding: "8px 16px", borderRadius: 9, background: tab === v ? T.primary : "transparent", color: tab === v ? T.primaryFg : T.muted }}>{label}</button>
            ))}
          </div>
          <div style={{ display: "flex", gap: 7, overflowX: "auto", paddingBottom: 2 }}>
            {tab === "loja"
              ? CATEGORIES.map((c) => <FilterPill key={c.key} active={cat === c.key} onClick={() => setCat(c.key)}>{c.icon} {c.label}</FilterPill>)
              : ([["todos", "Todos"], ["equipados", "Equipados"], ["nao", "Não equip."]] as const).map(([k, l]) => <FilterPill key={k} active={colFilter === k} onClick={() => setColFilter(k)}>{l}</FilterPill>)}
          </div>
        </div>
        <div style={{ flex: 1, overflowY: "auto", padding: "12px 16px 18px", display: "flex", flexDirection: "column", gap: 9 }}>
          {empty ? (
            <div style={{ textAlign: "center", padding: "24px 8px" }}>
              <Navi size={96} mood="sad" />
              <p style={{ fontFamily: DISP, fontWeight: 700, fontSize: 15, color: T.text, margin: "8px 0 4px" }}>Hmm, vazio por aqui...</p>
              <p style={{ fontFamily: SANS, fontSize: 12.5, color: T.muted, margin: "0 0 14px" }}>Que tal dar uma cara nova pro NAVI?</p>
            </div>
          ) : list.map((it) => (
            <ShelfCard key={it.id} item={it} selected={selected === it.id} affordable={afford(it)} colMode={tab === "colecao"}
              iconRef={(el) => { iconRefs.current[it.id] = el }}
              onEnter={(i) => { if (!i.lockedReason) setHoverId(i.id) }} onLeave={() => setHoverId(null)}
              onClick={onCardClick} onAction={(i) => i.equipped ? unequipMut.mutate(i.id) : equipMut.mutate(i.id)} />
          ))}
        </div>
      </div>
    )
  }

  // ── CTA ──
  const cta = () => {
    if (!selItem) return null
    if (!selItem.owned) {
      const ok = afford(selItem)
      const lacking = selItem.price != null ? selItem.price - (selItem.currency === "stars" ? balance.stars : balance.coins) : 0
      return (
        <button onClick={(e) => buy(selItem, e)} disabled={buyMut.isPending} style={{
          width: "100%", height: 52, borderRadius: 14, border: "none", cursor: ok ? "pointer" : "not-allowed",
          fontFamily: SANS, fontWeight: 800, fontSize: 15, color: ok ? "#1a1206" : T.muted,
          display: "flex", alignItems: "center", justifyContent: "center", gap: 10,
          background: ok ? "linear-gradient(95deg, #fbbf24, #a78bfa)" : T.popover,
          boxShadow: ok ? "0 0 26px rgba(251,191,36,0.45)" : "none", animation: ok ? "soft-pulse-gold 1.8s ease-in-out infinite" : "none",
        }}>
          {ok ? `✨ Comprar ${selItem.name} · ${priceLabel(selItem.currency, selItem.price)}`
            : `🔒 Faltam ${lacking} ${selItem.currency === "coins" ? "moedas" : "estrelas"} — continue estudando!`}
        </button>
      )
    }
    return (
      <div style={{ display: "flex", gap: 8 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 6, padding: "0 14px", height: 48, borderRadius: 12, background: "rgba(56,219,140,0.12)", border: `1px solid ${T.accent}55`, color: T.accent, fontFamily: SANS, fontWeight: 700, fontSize: 13 }}>✓ Você tem</div>
        <button onClick={() => selItem.equipped ? unequipMut.mutate(selItem.id) : equipMut.mutate(selItem.id)} style={{
          flex: 1, height: 48, borderRadius: 12, border: "none", cursor: "pointer", fontFamily: SANS, fontWeight: 700, fontSize: 15,
          background: selItem.equipped ? T.popover : T.accent, color: selItem.equipped ? T.text : T.primaryFg,
        }}>{selItem.equipped ? "Desequipar" : "Equipar agora"}</button>
      </div>
    )
  }

  // ── toolbar ──
  const toolbar = () => (
    <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap", animation: "slide-up .25s ease" }}>
      {selItem && (
        <div style={{ display: "inline-flex", background: T.popover, border: `1px solid ${T.border}`, borderRadius: 10, padding: 3 }}>
          {(["antes", "depois"] as const).map((k) => (
            <button key={k} onClick={() => setBa(k)} style={{ border: "none", cursor: "pointer", fontFamily: SANS, fontSize: 12, fontWeight: 700, padding: "6px 12px", borderRadius: 7, textTransform: "capitalize", background: ba === k ? T.primary : "transparent", color: ba === k ? T.primaryFg : T.muted }}>{k}</button>
          ))}
        </div>
      )}
      {selItem && <button onClick={() => { setSelected(null); setHoverId(null) }} style={{ height: 32, padding: "0 12px", borderRadius: 8, border: "none", background: "transparent", color: T.muted, fontFamily: SANS, fontWeight: 700, fontSize: 13, cursor: "pointer" }}>↻ Reset</button>}
      <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
        <span style={{ fontFamily: SANS, fontSize: 11, color: T.muted, fontWeight: 600 }}>Equipado:</span>
        {equippedItems.length === 0 && <span style={{ fontFamily: SANS, fontSize: 12, color: T.muted }}>nada ainda</span>}
        {equippedItems.map((it) => (
          <span key={it.id} onClick={() => unequipMut.mutate(it.id)} style={{ display: "inline-flex", alignItems: "center", gap: 5, cursor: "pointer", background: T.popover, border: `1px solid ${T.border}`, borderRadius: 9999, padding: "3px 7px 3px 9px", fontFamily: SANS, fontSize: 11, fontWeight: 600, color: T.text }}>
            {it.name}<span style={{ color: T.muted }}>✕</span>
          </span>
        ))}
      </div>
    </div>
  )

  const clientStage = (big: number) => (
    <div style={{ position: "relative", display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "flex-end" }}>
      <div style={{ position: "absolute", bottom: 2, width: big * 0.78, height: big * 0.2, borderRadius: "50%", background: "radial-gradient(ellipse, rgba(255,200,100,0.3), transparent 70%)", filter: "blur(3px)", animation: selItem ? "spotlight-pulse 2s ease-in-out infinite" : "none" }} />
      <div style={{ animation: "breathe 4s ease-in-out infinite", transform: "rotate(-3deg)" }}>
        <Navi size={big} fur={naviLook.fur} hat={naviLook.hat} accessory={naviLook.accessory} mood={effMood} />
      </div>
    </div>
  )

  if (catalogQuery.isLoading) {
    return <div className="flex items-center justify-center h-[60dvh] text-muted-foreground">Abrindo a Toca do NAVI…</div>
  }

  return (
    <div style={{ fontFamily: SANS, color: T.text }}
      className="flex flex-col lg:flex-row h-full overflow-hidden">
      {/* stage */}
      <div ref={stageRef} className="relative flex flex-col overflow-hidden flex-1 lg:flex-[1.55]" style={{ background: T.bg, minHeight: 0 }}>
        <ShopScene />
        <Particles count={6} />
        {/* header */}
        <div style={{ position: "relative", zIndex: 10, display: "flex", alignItems: "flex-start", justifyContent: "space-between", padding: "18px 22px 0" }}>
          <div>
            <h1 style={{ fontFamily: DISP, fontWeight: 800, fontSize: 22, margin: 0, display: "flex", alignItems: "center", gap: 9, textShadow: "0 2px 10px rgba(0,0,0,0.6)" }}>🛍️ Toca do NAVI</h1>
            <p style={{ fontFamily: SANS, fontSize: 12, color: T.muted, margin: "3px 0 0", textShadow: "0 1px 6px rgba(0,0,0,0.7)" }}>Personalize seu mascote</p>
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            <StatusChip icon="🪙" value={balance.coins.toLocaleString("pt-BR")} color={T.warning} />
            <StatusChip icon="💎" value={balance.stars} color={T.accent} />
          </div>
        </div>
        {/* merchant zone (desktop) */}
        <div className="hidden lg:block" style={{ position: "relative", zIndex: 5, height: 168, flexShrink: 0 }}>
          <div style={{ position: "absolute", left: "12%", bottom: -2, zIndex: 1, animation: "breathe 4.5s ease-in-out infinite" }}><NaviMerchant size={132} /></div>
          <SpeechBubble text={say} style={{ position: "absolute", left: "34%", top: 26, zIndex: 6 }} />
          <Counter style={{ bottom: 0, height: 60, zIndex: 3 }} />
        </div>
        {/* merchant FAB (mobile) */}
        <div className="lg:hidden" style={{ position: "absolute", top: 64, right: 14, zIndex: 12 }}>
          <div onClick={() => setMFab((v) => !v)} style={{ width: 48, height: 48, borderRadius: "50%", background: "rgba(212,160,69,0.2)", border: "2px solid #d4a045", display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer", overflow: "hidden" }}>
            <NaviMerchant size={52} style={{ marginTop: 10 }} />
          </div>
          {mFab && <SpeechBubble text={say} style={{ position: "absolute", top: 4, right: 56, width: 180 }} />}
        </div>
        {/* client */}
        <div style={{ position: "relative", zIndex: 6, flex: 1, display: "flex", alignItems: "flex-end", justifyContent: "center", paddingBottom: 6, minHeight: 0 }}>
          {clientStage(210)}
        </div>
        {/* toolbar + CTA */}
        <div style={{ position: "relative", zIndex: 8, flexShrink: 0, padding: "12px 22px 18px", background: "linear-gradient(180deg, transparent, rgba(14,10,30,0.85) 40%)", display: "flex", flexDirection: "column", gap: 10 }}>
          {toolbar()}
          {cta()}
        </div>
        <CoinBurst burst={burst} />
      </div>
      {/* shelf */}
      <div className="flex-1 lg:flex-none border-t lg:border-t-0 lg:border-l" style={{ background: T.card, borderColor: T.border, minHeight: 0 }}>
        <div className="lg:w-[372px] h-full">{shelf()}</div>
      </div>

      <RewardModal item={reward} onClose={() => setReward(null)} />
    </div>
  )
}
