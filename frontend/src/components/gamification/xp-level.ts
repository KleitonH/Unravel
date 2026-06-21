/**
 * Nível derivado do XP (não existe campo de nível no backend; é puramente
 * uma leitura do XP acumulado). Cada nível custa ~18% mais que o anterior,
 * começando em 100 XP — curva suave que dá progressão visível cedo e
 * desacelera depois. Determinístico e barato.
 */
export type XpLevel = {
  level: number
  /** XP já acumulado dentro do nível atual. */
  intoLevel: number
  /** XP total que o nível atual exige pra avançar. */
  levelSpan: number
  /** XP que falta pro próximo nível. */
  toNext: number
  /** Progresso 0..1 dentro do nível atual. */
  progress: number
}

export function xpLevel(xp: number): XpLevel {
  let level = 1
  let need = 100
  let acc = 0
  while (xp >= acc + need) {
    acc += need
    level++
    need = Math.round(need * 1.18)
  }
  const intoLevel = Math.max(0, xp - acc)
  return {
    level,
    intoLevel,
    levelSpan: need,
    toNext: Math.max(0, need - intoLevel),
    progress: Math.min(1, intoLevel / need),
  }
}
