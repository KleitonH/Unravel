import { Link, useRouterState } from "@tanstack/react-router"
import { navItems } from "./nav-items"
import { cn } from "@/lib/utils"
import { useAuth } from "@/stores/auth"

/**
 * Nav fixa inferior pra viewports < lg. Aparece em mobile/tablet portrait;
 * é escondida pelo Tailwind (`lg:hidden`) quando a sidebar entra.
 */
export function BottomNav() {
  const { pathname } = useRouterState({ select: (s) => s.location })
  const isModerator = useAuth((s) => s.isModerator())
  const visible = navItems.filter((i) => !i.requires || (i.requires === "Moderator" && isModerator))

  return (
    <nav
      className="fixed bottom-0 inset-x-0 z-40 lg:hidden flex items-stretch border-t border-border bg-card/95 backdrop-blur"
      style={{ paddingBottom: "env(safe-area-inset-bottom)" }}
      aria-label="Navegação principal"
    >
      {visible.map((item) => {
        const active = pathname.startsWith(item.to)
        return (
          <Link
            key={item.to}
            to={item.to}
            className={cn(
              "group flex-1 flex flex-col items-center justify-center gap-1 py-2 text-[10px] font-medium uppercase tracking-wider",
              active ? "text-primary" : "text-muted-foreground",
            )}
          >
            <item.Icon className={cn("h-5 w-5 transition-transform", active && "scale-110")} />
            <span className="truncate max-w-[64px]">{item.label}</span>
          </Link>
        )
      })}
    </nav>
  )
}
