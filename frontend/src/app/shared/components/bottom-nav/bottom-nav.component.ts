import { Component, computed, inject, input } from "@angular/core";
import { Router } from "@angular/router";
import { AuthService } from "../../../core/services/auth.service";

type NavItem = {
  key: string;
  icon: string;
  label: string;
  route: string;
  /** Filtro de role; ausente = visível para todos. */
  requires?: "Moderator";
};

const NAV_ITEMS: NavItem[] = [
  { key: "home", icon: "🏠", label: "Início", route: "/dashboard" },
  { key: "trails", icon: "🗺️", label: "Trilhas", route: "/trails" },
  // PR 9 — entrada pro algoritmo de jornada. Sem trilha selecionada, manda
  // para o onboarding (que escolhe trilhas + faz nivelamento).
  { key: "journey", icon: "🐾", label: "Jornada", route: "/onboarding" },
  { key: "challenges", icon: "⚔️", label: "Desafios", route: "/desafio" },
  { key: "profile", icon: "👤", label: "Perfil", route: "/profile" },
  // PR 10 — visível somente para Moderator (filtro UX; backend valida role).
  { key: "admin", icon: "🛠️", label: "Admin", route: "/admin", requires: "Moderator" },
];

@Component({
  selector: "app-bottom-nav",
  standalone: true,
  template: `
    <nav class="bottom-nav" aria-label="Navegação principal">
      @for (item of navItems(); track item.key) {
        <button
          class="nav-item"
          [class.nav-item--active]="active() === item.key"
          (click)="navigate(item.route)"
        >
          <span class="nav-item__icon">{{ item.icon }}</span>
          <span class="nav-item__label">{{ item.label }}</span>
        </button>
      }
    </nav>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .bottom-nav {
        display: flex;
        align-items: center;
        justify-content: space-around;
        background: #181230;
        border-top: 1px solid #4a387d;
        padding: 8px 0 calc(8px + env(safe-area-inset-bottom));
        flex-shrink: 0;
      }
      .nav-item {
        background: none;
        border: none;
        cursor: pointer;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 2px;
        padding: 4px 12px;
        border-radius: 12px;
        transition: background 0.15s;
      }
      .nav-item__icon {
        font-size: 20px;
        line-height: 1;
      }
      .nav-item__label {
        font-size: 9px;
        font-weight: 500;
        color: #b0a7cc;
        font-family: "DM Sans", sans-serif;
      }
      .nav-item--active {
        background: rgba(110, 54, 244, 0.2);
      }
      .nav-item--active .nav-item__label {
        color: #bc9cfe;
        font-weight: 700;
      }
    `,
  ],
})
export class BottomNavComponent {
  private readonly auth = inject(AuthService);

  readonly active = input<string>("home");
  /** Filtra itens condicionais por role atual; recomputado quando o user muda. */
  readonly navItems = computed(() =>
    NAV_ITEMS.filter(
      (i) => !i.requires || this.auth.currentRole() === i.requires,
    ),
  );

  constructor(private readonly router: Router) {}

  navigate(route: string): void {
    this.router.navigate([route]);
  }
}
