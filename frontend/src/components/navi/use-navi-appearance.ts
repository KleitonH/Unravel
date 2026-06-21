import { useQuery } from "@tanstack/react-query"
import { shopApi } from "@/api/shop"
import { appearanceFromEquipped, type NaviAppearance } from "./appearance"

/**
 * Aparência ATIVA do NAVI do usuário (cosméticos equipados).
 *
 * Fonte: catálogo da Loja (`["shop","catalog"]`) — é o único shape que traz
 * `slot` + `assetSlug` + `equipped` por usuário. Sem nada equipado (ou
 * carregando) → aparência default (preto/neutral), então o NAVI sempre
 * renderiza. Reutilizável em perfil, batalhas, dashboard, etc.
 */
export function useNaviAppearance(): { appearance: NaviAppearance; isLoading: boolean } {
  const q = useQuery({ queryKey: ["shop", "catalog"], queryFn: shopApi.catalog, staleTime: 60_000 })
  const equipped = (q.data?.items ?? [])
    .filter((i) => i.owned && i.equipped)
    .map((i) => ({ slot: i.slot, assetSlug: i.assetSlug }))
  return { appearance: appearanceFromEquipped(equipped), isLoading: q.isLoading }
}
