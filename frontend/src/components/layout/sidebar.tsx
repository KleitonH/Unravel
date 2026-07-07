import { useState } from "react"
import { Link, useRouterState } from "@tanstack/react-router"
import { ChevronRight } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Separator } from "@/components/ui/separator"
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"
import { navItems } from "./nav-items"
import { useAuth } from "@/stores/auth"

/**
 * Sidebar lateral pra viewports ≥ lg. Compacta colapsável: 72px de
 * largura por default mostrando só ícones + labels miúdas; abre pra
 * 240px no clique do toggle. Tooltips aparecem só no estado compacto
 * (no expandido, o label já está visível).
 */
export function Sidebar() {
  const [expanded, setExpanded] = useState(false)
  const { pathname } = useRouterState({ select: (s) => s.location })
  const isModerator = useAuth((s) => s.isModerator())

  const visible = navItems.filter((i) => !i.requires || (i.requires === "Moderator" && isModerator))

  return (
    <TooltipProvider delayDuration={120}>
      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-40 hidden lg:flex flex-col border-r border-border bg-card/70 backdrop-blur",
          "transition-[width] duration-200",
          expanded ? "w-60" : "w-[72px]",
        )}
      >
        {/* Brand + toggle */}
        <div className="flex items-center justify-between px-3 py-4">
          <Link to="/dashboard" className="flex items-center gap-2">
            <img src="/logo-novelo.svg" alt="Unravel" className="h-7 w-7 shrink-0" />
            {expanded && (
              <span className="font-display font-extrabold tracking-wider text-sm">
                UNRAVEL
              </span>
            )}
          </Link>
          <Button
            variant="ghost"
            size="icon"
            className={cn("h-8 w-8 transition-transform", expanded && "rotate-180")}
            onClick={() => setExpanded((v) => !v)}
            aria-label={expanded ? "Recolher menu" : "Expandir menu"}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>

        <Separator />

        <nav className="flex-1 overflow-y-auto py-3">
          <ul className="flex flex-col gap-1 px-2">
            {visible.map((item) => {
              const active = pathname.startsWith(item.to)
              const link = (
                <Link
                  to={item.to}
                  className={cn(
                    "group flex items-center gap-3 rounded-md py-2 text-sm font-medium",
                    "transition-colors hover:bg-accent/10",
                    // Recolhido: ícone centralizado no rail. Expandido: alinhado
                    // à esquerda com padding pro label respirar.
                    expanded ? "px-3" : "justify-center px-0",
                    active
                      ? "bg-primary/15 text-primary"
                      : "text-muted-foreground hover:text-foreground",
                  )}
                >
                  <item.Icon className="h-5 w-5 shrink-0" />
                  {expanded && <span className="truncate">{item.label}</span>}
                </Link>
              )
              if (expanded) return <li key={item.to}>{link}</li>
              return (
                <li key={item.to}>
                  <Tooltip>
                    <TooltipTrigger asChild>{link}</TooltipTrigger>
                    <TooltipContent side="right">{item.label}</TooltipContent>
                  </Tooltip>
                </li>
              )
            })}
          </ul>
        </nav>
      </aside>
    </TooltipProvider>
  )
}
