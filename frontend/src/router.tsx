import {
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
} from "@tanstack/react-router"

/**
 * Router skeleton do PR 21 — só rota raiz com placeholder. Páginas reais
 * (login, dashboard, onboarding, jornada, quiz, admin) entram no PR 22+.
 * Setup já pronto pra suportar protected routes via `beforeLoad` no PR 22.
 */
const rootRoute = createRootRoute({
  component: () => <Outlet />,
})

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  component: () => (
    <div className="flex h-full items-center justify-center p-8">
      <div className="text-center space-y-3">
        <h1 className="text-4xl font-display font-extrabold tracking-tight">
          💎 Unravel
        </h1>
        <p className="text-muted-foreground">
          Bootstrap React + Vite + Tailwind + shadcn-style pronto.
        </p>
        <p className="text-xs text-muted-foreground/70">
          PR 22 adiciona auth + AppShell responsivo; PR 23 as páginas.
        </p>
      </div>
    </div>
  ),
})

const routeTree = rootRoute.addChildren([indexRoute])

export const router = createRouter({ routeTree })

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router
  }
}
