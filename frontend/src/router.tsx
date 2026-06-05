import { lazy, Suspense } from "react"
import {
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  redirect,
} from "@tanstack/react-router"
import { AppShell } from "@/components/layout/app-shell"
import { useAuth } from "@/stores/auth"
import { Skeleton } from "@/components/ui/skeleton"

/**
 * PR 25 — code-splitting por rota. Cada feature vira um chunk separado
 * via `React.lazy` + `Suspense`. Bundle inicial cai de ~610KB (gzip 187KB)
 * pra um core menor com login/register/AppShell embutidos, e o restante
 * é baixado on-demand quando o usuário navega.
 *
 * <para>Por que <c>React.lazy</c> e não <c>lazyRouteComponent</c> do
 * TanStack Router: o nosso bundle é pequeno o suficiente pra não ganhar
 * com o pré-fetching do router; React.lazy é mais simples e zero config.</para>
 */
const LoginPage      = lazy(() => import("@/features/auth/login-page").then((m) => ({ default: m.LoginPage })))
const RegisterPage   = lazy(() => import("@/features/auth/register-page").then((m) => ({ default: m.RegisterPage })))
const DashboardPage  = lazy(() => import("@/features/dashboard/dashboard-page").then((m) => ({ default: m.DashboardPage })))
const TrailsPage     = lazy(() => import("@/features/trails/trails-page").then((m) => ({ default: m.TrailsPage })))
const ProfilePage    = lazy(() => import("@/features/profile/profile-page").then((m) => ({ default: m.ProfilePage })))
const OnboardingPage = lazy(() => import("@/features/onboarding/onboarding-page").then((m) => ({ default: m.OnboardingPage })))
const JornadaPage      = lazy(() => import("@/features/trail-map/trail-map-page").then((m) => ({ default: m.TrailMapPage })))
const ContentStudyPage = lazy(() => import("@/features/content-study/content-study-page").then((m) => ({ default: m.ContentStudyPage })))
const QuizPage       = lazy(() => import("@/features/quiz/quiz-page").then((m) => ({ default: m.QuizPage })))
const AdminPage           = lazy(() => import("@/features/admin/admin-page").then((m) => ({ default: m.AdminPage })))
const AdminTrailsPage     = lazy(() => import("@/features/admin-custom/trails-list-page").then((m) => ({ default: m.TrailsListPage })))
const AdminTrailDetail    = lazy(() => import("@/features/admin-custom/trail-detail-page").then((m) => ({ default: m.TrailDetailPage })))
const AdminContentEditor  = lazy(() => import("@/features/admin-custom/content-editor-page").then((m) => ({ default: m.ContentEditorPage })))
const ReinforcePage       = lazy(() => import("@/features/reinforce/reinforce-page").then((m) => ({ default: m.ReinforcePage })))

/** Wrapper que serializa Suspense fallback em cada rota lazy.
 *  Skeleton segue identidade visual; nada de spinner branco.
 *  PR 27: aplica `animate-page-in` no conteúdo (fade + slide up
 *  sutil ~8px). Skeleton fica imóvel — anima só o conteúdo final
 *  pra não criar dupla animação. */
function Lazy({ Component }: { Component: React.ComponentType }) {
  return (
    <Suspense fallback={
      <div className="p-6 lg:p-10 space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-4 w-full max-w-md" />
        <div className="grid gap-4 md:grid-cols-2">
          {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-32" />)}
        </div>
      </div>
    }>
      <div className="animate-page-in">
        <Component />
      </div>
    </Suspense>
  )
}

// ── Rotas ────────────────────────────────────────────────────────────

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
  component: () => <Lazy Component={LoginPage} />,
})

const registerRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/auth/register",
  component: () => <Lazy Component={RegisterPage} />,
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
      <p className="text-muted-foreground">Página em construção.</p>
    </div>
  )
}

const dashboardRoute  = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/dashboard",        component: () => <Lazy Component={DashboardPage} /> })
const trailsRoute     = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/trails",           component: () => <Lazy Component={TrailsPage} /> })
const onboardingRoute = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/onboarding",       component: () => <Lazy Component={OnboardingPage} /> })
const jornadaRoute      = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/jornada/$trailId",   component: () => <Lazy Component={JornadaPage} /> })
const contentStudyRoute = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/contents/$contentId", component: () => <Lazy Component={ContentStudyPage} /> })
const quizRoute         = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/quiz/$contentId",     component: () => <Lazy Component={QuizPage} /> })
const adminRoute              = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/admin",                          component: () => <Lazy Component={AdminPage} /> })
const adminTrailsRoute        = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/admin/trails",                   component: () => <Lazy Component={AdminTrailsPage} /> })
const adminTrailDetailRoute   = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/admin/trails/$trailId",          component: () => <Lazy Component={AdminTrailDetail} /> })
const adminContentEditorRoute = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/admin/contents/$contentId",      component: () => <Lazy Component={AdminContentEditor} /> })
const reinforceRoute          = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/reinforce/$trailId",             component: () => <Lazy Component={ReinforcePage} /> })
const profileRoute            = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/profile",                        component: () => <Lazy Component={ProfilePage} /> })
const desafioRoute            = createRoute({ getParentRoute: () => authedLayoutRoute, path: "/desafio",                        component: () => <Placeholder title="Desafios" /> })

const routeTree = rootRoute.addChildren([
  indexRoute,
  loginRoute,
  registerRoute,
  authedLayoutRoute.addChildren([
    dashboardRoute,
    trailsRoute,
    onboardingRoute,
    jornadaRoute,
    contentStudyRoute,
    quizRoute,
    adminRoute,
    adminTrailsRoute,
    adminTrailDetailRoute,
    adminContentEditorRoute,
    reinforceRoute,
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
