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

const files = import.meta.glob("./assets/*.{webp,png}", {
  eager: true,
  query: "?url",
  import: "default",
}) as Record<string, string>

/** slug → URL do asset raster (quando existe um arquivo correspondente). */
const ASSET_BY_SLUG: Record<string, string> = {}
for (const path in files) {
  const slug = path.split("/").pop()!.replace(/\.(webp|png)$/i, "")
  ASSET_BY_SLUG[slug] = files[path]
}

/** Retorna a URL do asset raster do slug, ou null se ainda não há arte
 *  (cai no SVG de placeholder). */
export function assetForSlug(slug?: string | null): string | null {
  return slug ? ASSET_BY_SLUG[slug] ?? null : null
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
