import { Routes } from "@angular/router";
import { authGuard } from "./core/guards/auth.guard";

export const routes: Routes = [
  {
    path: "",
    redirectTo: "/dashboard",
    pathMatch: "full",
  },
  {
    path: "auth",
    loadChildren: () =>
      import("./features/auth/auth.routes").then((m) => m.AUTH_ROUTES),
  },
  {
    path: "dashboard",
    loadComponent: () =>
      import("./features/dashboard/dashboard.component").then(
        (m) => m.DashboardComponent,
      ),
    canActivate: [authGuard],
  },
  {
    path: "trails",
    loadComponent: () =>
      import("./features/trails/trail-list/trail-list.component").then(
        (m) => m.TrailListComponent,
      ),
    canActivate: [authGuard],
  },
  {
    path: "trails/:id",
    loadComponent: () =>
      import("./features/trails/trail-detail/trail-detail.component").then(
        (m) => m.TrailDetailComponent,
      ),
    canActivate: [authGuard],
  },
  {
    path: "desafio",
    loadComponent: () =>
      import("./features/desafio/desafio.component").then(
        (m) => m.DesafioComponent,
      ),
    canActivate: [authGuard],
  },
  {
    path: "profile",
    loadComponent: () =>
      import("./features/profile/profile.component").then(
        (m) => m.ProfileComponent,
      ),
    canActivate: [authGuard],
  },
  {
    path: "moderator/contents",
    loadComponent: () =>
      import(
        "./features/moderator/contents/moderator-contents.component"
      ).then((m) => m.ModeratorContentsComponent),
    canActivate: [authGuard],
  },
  {
    path: "**",
    redirectTo: "/auth/login",
  },
];
