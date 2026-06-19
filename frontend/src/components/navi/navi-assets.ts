// PR 63e — registro data-driven dos assets do NAVI (paper-doll por camadas).
//
// O visual deixou de ser hardcoded em switch(slug): cada cosmético é uma
// CAMADA empilhada por z-index. Se existir um arquivo de imagem cujo nome é
// exatamente o `slug` do cosmético (ex.: `cartola.webp`), o renderer usa a
// imagem; senão, cai no desenho SVG de placeholder (idêntico ao de hoje).
//
// → Para ativar arte nova: solte `src/components/navi/assets/<slug>.webp`
//   (fundo transparente, canvas canônico — ver assets/README.md). Sem tocar
//   em código. O Vite descobre o arquivo automaticamente via import.meta.glob.

import { env } from "@/lib/env"

const files = import.meta.glob("./assets/*.{webp,png}", {
  eager: true,
  query: "?url",
  import: "default",
}) as Record<string, string>

/** slug → URL bundlada (Vite); e slug → extensão. A pasta `assets/` é o
 *  manifesto de "quais slugs têm arte raster" — em modo CDN reescrevemos só
 *  a base da URL, mantendo o mesmo conjunto de slugs. */
const BUNDLED: Record<string, string> = {}
const EXT: Record<string, string> = {}
for (const path in files) {
  const m = path.split("/").pop()!.match(/^(.*)\.(webp|png)$/i)
  if (!m) continue
  BUNDLED[m[1]] = files[path]
  EXT[m[1]] = m[2].toLowerCase()
}

/**
 * Retorna a URL do asset raster do slug, ou null se não há arte (cai no SVG
 * de placeholder).
 *
 * - `VITE_NAVI_CDN` vazio → serve o arquivo bundlado (default).
 * - `VITE_NAVI_CDN` setado → serve de `${cdn}/<slug>.<ext>` (mesmo conjunto
 *   de slugs da pasta). Suba os MESMOS arquivos pro bucket/CDN.
 */
export function assetForSlug(slug?: string | null): string | null {
  if (!slug) return null
  if (env.naviCdn && EXT[slug]) return `${env.naviCdn}/${slug}.${EXT[slug]}`
  return BUNDLED[slug] ?? null
}

/** Z-order canônico das camadas do paper-doll (back → front).
 *  A base (pelagem) inclui corpo/cabeça/orelhas/braços/cauda; o rosto
 *  (mood + óculos + nariz) é overlay pra permitir trocar expressão sem
 *  refazer a base. */
export const Z = {
  cape: 5,   // capa atrás de tudo
  fur:  20,  // base (corpo + cabeça + braços + cauda)
  body: 30,  // acessórios de tronco (jaleco, mochila)
  neck: 45,  // gravata
  face: 70,  // olhos + boca (mood) + óculos + nariz
  hat:  90,  // chapéu por cima
} as const

/** Z específico por acessório (capa atrás; demais sobre o tronco/pescoço). */
export function zForAccessory(slug: string): number {
  if (slug === "capa") return Z.cape
  if (slug === "gravata") return Z.neck
  return Z.body // jaleco, mochila
}
