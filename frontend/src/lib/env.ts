/**
 * Configuração de ambiente — única fonte da verdade pra URLs do backend.
 * Em produção (Vite build), `VITE_API_URL` pode sobrescrever via `.env`;
 * default cobre o dev local (porta 5000).
 */
export const env = {
  apiUrl: import.meta.env.VITE_API_URL ?? "http://localhost:5000",
} as const
