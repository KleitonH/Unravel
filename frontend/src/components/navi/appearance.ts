import type { NaviFur, NaviMood } from "./navi"

/** Item equipado, na forma mínima pra compor o NAVI. */
export type EquippedSlot = { slot: string; assetSlug: string }

export type NaviAppearance = {
  fur:       NaviFur
  hat:       string | null
  accessory: string | null
  mood:      NaviMood
}

/**
 * PR 63 — deriva a aparência do NAVI a partir dos cosméticos equipados
 * (map slot → prop). Reutilizável na loja, no perfil e no dashboard.
 */
export function appearanceFromEquipped(equipped: EquippedSlot[]): NaviAppearance {
  const a: NaviAppearance = { fur: "preto", hat: null, accessory: null, mood: "neutral" }
  for (const e of equipped) {
    if (e.slot === "fur") a.fur = e.assetSlug as NaviFur
    else if (e.slot === "hat") a.hat = e.assetSlug
    else if (e.slot === "accessory") a.accessory = e.assetSlug
    else if (e.slot === "mood") a.mood = e.assetSlug as NaviMood
  }
  return a
}
