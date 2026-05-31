import type { ReactNode } from "react"
import { Sidebar } from "./sidebar"
import { BottomNav } from "./bottom-nav"

/**
 * Shell autenticado. Em ≥ lg renderiza a Sidebar lateral compacta;
 * em < lg, a BottomNav fixa. O `<main>` recebe padding lateral
 * correspondente à sidebar via `lg:pl-[72px]` (compacta por default —
 * o estado expandido aumenta sobre, sem reflow do conteúdo).
 *
 * Padding-bottom em mobile pra compensar a altura da bottom-nav
 * + safe-area-inset (iPhones com home indicator).
 */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-full flex flex-col bg-background">
      <Sidebar />
      <main className="flex-1 lg:pl-[72px] pb-20 lg:pb-0">
        {children}
      </main>
      <BottomNav />
    </div>
  )
}
