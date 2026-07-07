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
 * O shell tem altura fixa de viewport (`h-dvh` + `overflow-hidden`): o
 * documento nunca rola. Quem rola é o `<main>` (`overflow-y-auto`) — assim
 * o header fica sempre visível e páginas "app-like" (ex: Loja) podem usar
 * `h-full` e rolar só internamente, sem arrastar a página inteira.
 *
 * A BottomNav (mobile) é um item de fluxo (não `fixed`): o `<main>` termina
 * exatamente no topo dela, sem gap nem padding de compensação. O safe-area
 * inset vem do próprio componente.
 */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="h-dvh flex flex-col bg-background overflow-hidden">
      <Sidebar />
      <div className="flex-1 lg:pl-[72px] flex flex-col min-w-0 min-h-0">
        <TopHeader />
        <main className="flex-1 min-h-0 overflow-y-auto">
          {children}
        </main>
      </div>
      <BottomNav />
    </div>
  )
}
