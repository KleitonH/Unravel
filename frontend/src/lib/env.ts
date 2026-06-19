/**
 * Configuração de ambiente — única fonte da verdade pra URLs do backend.
 * Em produção (Vite build), `VITE_API_URL` pode sobrescrever via `.env`;
 * default cobre o dev local (porta 5000).
 */
export const env = {
  apiUrl: import.meta.env.VITE_API_URL ?? "http://localhost:5000",
  /**
   * Base URL opcional pros assets raster do NAVI (paper-doll). Vazio = usa
   * os arquivos bundlados (`import.meta.glob`). Setado = serve de CDN/storage
   * externo (ex.: Cloudflare R2/jsDelivr) — útil quando o front é servido pela
   * própria VM e queremos poupar banda. Trailing slash é normalizado.
   */
  naviCdn: (import.meta.env.VITE_NAVI_CDN ?? "").trim().replace(/\/+$/, ""),
} as const
