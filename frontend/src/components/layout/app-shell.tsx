import type { ReactNode } from "react"
import { Sidebar } from "./sidebar"
import { BottomNav } from "./bottom-nav"
import { TopHeader } from "./top-header"

/**
 * Shell autenticado. Em ≥ lg renderiza a Sidebar lateral compacta;
 * em < lg, a BottomNav fixa. A coluna de conteúdo recebe padding lateral
 * correspondente à sidebar via `lg:pl-[72px]` (compacta por default —
 * o estado expandido aumenta sobre, sem reflow do conteúdo).
 *
 * No topo da coluna fica a <TopHeader> global (vidas/nível/ofensiva/moedas
 * + notificações + perfil com o rosto do NAVI), sticky — sempre visível.
 *
 * Padding-bottom em mobile pra compensar a altura da bottom-nav
 * + safe-area-inset (iPhones com home indicator).
 */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-full flex flex-col bg-background">
      <Sidebar />
      <div className="flex-1 lg:pl-[72px] flex flex-col min-w-0">
        <TopHeader />
        <main className="flex-1 pb-20 lg:pb-0">
          {children}
        </main>
      </div>
      <BottomNav />
    </div>
  )
}
