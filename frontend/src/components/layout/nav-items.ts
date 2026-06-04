import { BookPlus, Compass, Home, Map, ShieldCheck, Swords, User } from "lucide-react"
import type { LucideIcon } from "lucide-react"

export type NavItem = {
  to:    string
  label: string
  Icon:  LucideIcon
  /** Filtro de role; ausente = todos. */
  requires?: "Moderator"
}

/** Fonte única dos itens de navegação — usada por sidebar + bottom-nav. */
export const navItems: NavItem[] = [
  { to: "/dashboard",  label: "Início",   Icon: Home },
  { to: "/trails",     label: "Trilhas",  Icon: Map },
  { to: "/onboarding", label: "Jornada",  Icon: Compass },
  { to: "/desafio",    label: "Desafios", Icon: Swords },
  { to: "/profile",    label: "Perfil",   Icon: User },
  { to: "/admin",        label: "Admin",    Icon: ShieldCheck, requires: "Moderator" },
  { to: "/admin/trails", label: "Trilhas",  Icon: BookPlus,    requires: "Moderator" },
]
