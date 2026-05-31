import {
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  redirect,
} from "@tanstack/react-router"
import { AppShell } from "@/components/layout/app-shell"
import { LoginPage } from "@/features/auth/login-page"
import { RegisterPage } from "@/features/auth/register-page"
import { DashboardPage } from "@/features/dashboard/dashboard-page"
import { TrailsPage } from "@/features/trails/trails-page"
import { ProfilePage } from "@/features/profile/profile-page"
import { useAuth } from "@/stores/auth"

const rootRoute = createRootRoute({ component: () => <Outlet /> })

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  beforeLoad: () => {
    if (useAuth.getState().isAuthenticated()) throw redirect({ to: "/dashboard" })
    throw redirect({ to: "/auth/login" })
  },
})

const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/auth/login",
  component: LoginPage,
})

const registerRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/auth/register",
  component: RegisterPage,
})

const authedLayoutRoute = createRoute({
  getParentRoute: () => rootRoute,
  id: "authed",
  beforeLoad: ({ location }) => {
    if (!useAuth.getState().isAuthenticated()) {
      throw redirect({
        to: "/auth/login",
        search: { redirect: location.pathname },
      })
    }
  },
  component: () => (
    <AppShell>
      <Outlet />
    </AppShell>
  ),
})

function Placeholder({ title }: { title: string }) {
  return (
    <div className="p-6 lg:p-10 space-y-2">
      <h1 className="text-3xl font-display font-extrabold tracking-tight">{title}</h1>
      <p className="text-muted-foreground">
        Página placeholder — conteúdo real chega no PR 23.
      </p>
    </div>
  )
}

const dashboardRoute  = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/dashboard",        component: DashboardPage })
const trailsRoute     = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/trails",           component: TrailsPage })
const onboardingRoute = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/onboarding",       component: () => <Placeholder title="Onboarding" /> })
const jornadaRoute    = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/jornada/$trailId", component: () => <Placeholder title="Jornada" /> })
const quizRoute       = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/quiz/$contentId",  component: () => <Placeholder title="Quiz" /> })
const adminRoute      = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/admin",            component: () => <Placeholder title="Admin" /> })
const profileRoute    = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/profile",          component: ProfilePage })
const desafioRoute    = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/desafio",          component: () => <Placeholder title="Desafios" /> })

const routeTree = rootRoute.addChildren([
  indexRoute,
  loginRoute,
  registerRoute,
  authedLayoutRoute.addChildren([
    dashboardRoute,
    trailsRoute,
    onboardingRoute,
    jornadaRoute,
    quizRoute,
    adminRoute,
    profileRoute,
    desafioRoute,
  ]),
])

export const router = createRouter({ routeTree })

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router
  }
}
