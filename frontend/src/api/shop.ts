import { api } from "./client"
import type { ShopCatalog, PurchaseResponse } from "@/types/api"

/**
 * PR 63 — cliente da Loja cosmética (Toca do NAVI). Espelha o ShopController.
 * O backend trata 402 (saldo) / 409 (já possui) / 400 (locked); o caller
 * captura via TanStack Query onError.
 */
export const shopApi = {
  catalog: () =>
    api.get<ShopCatalog>("/api/shop").then((r) => r.data),

  buy: (cosmeticId: number) =>
    api.post<PurchaseResponse>(`/api/shop/${cosmeticId}/buy`, {}).then((r) => r.data),

  equip: (cosmeticId: number) =>
    api.put<{ message: string }>(`/api/shop/${cosmeticId}/equip`, {}).then((r) => r.data),

  unequip: (cosmeticId: number) =>
    api.put<{ message: string }>(`/api/shop/${cosmeticId}/unequip`, {}).then((r) => r.data),
}
