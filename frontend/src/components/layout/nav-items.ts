import { BookPlus, Boxes, Home, Map, ShieldCheck, ShoppingBag, Users } from "lucide-react"
import type { LucideIcon } from "lucide-react"

export type NavItem = {
  to:    string
  label: string
  Icon:  LucideIcon
  /** Filtro de role; ausente = todos. */
  requires?: "Moderator"
}

/** Fonte única dos itens de navegação — usada por sidebar + bottom-nav.
 *
 *  Decisões:
 *  - Removido item "Jornada" (apontava pra /onboarding mas confundia com
 *    o conceito "jornada da trilha" do mapa SMW). Hoje "Reformular jornada"
 *    fica como botão na /trails — entrada contextual, sem ocupar nav fixo.
 *  - "Desafios" mantido como placeholder pra modo futuro
 *    (caixinhas/novelo de lã — NAVI themed).
 *  - "Trilhas" admin renomeado pra "Curadoria" pra não duplicar label. */
export const navItems: NavItem[] = [
  { to: "/dashboard",  label: "Início",    Icon: Home },
  { to: "/trails",     label: "Trilhas",   Icon: Map },
  { to: "/loja",       label: "Loja",      Icon: ShoppingBag },
  { to: "/amigos",     label: "Amigos",    Icon: Users },
  { to: "/caixinha",   label: "Caixinha",  Icon: Boxes },
  { to: "/admin",        label: "Admin",     Icon: ShieldCheck, requires: "Moderator" },
  { to: "/admin/trails", label: "Curadoria", Icon: BookPlus,    requires: "Moderator" },
]
