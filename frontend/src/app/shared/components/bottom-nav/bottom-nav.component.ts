import { Component, input } from "@angular/core";
import { Router } from "@angular/router";

const NAV_ITEMS = [
  { key: "home", icon: "🏠", label: "Início", route: "/dashboard" },
  { key: "trails", icon: "🗺️", label: "Trilhas", route: "/trails" },
  { key: "challenges", icon: "⚔️", label: "Desafios", route: "/desafio" },
  { key: "ranking", icon: "🏆", label: "Ranking", route: "/dashboard" },
  { key: "profile", icon: "👤", label: "Perfil", route: "/dashboard" },
];

@Component({
  selector: "app-bottom-nav",
  standalone: true,
  template: `
    <nav class="bottom-nav" aria-label="Navegação principal">
      @for (item of navItems; track item.key) {
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
  readonly active = input<string>("home");
  readonly navItems = NAV_ITEMS;

  constructor(private readonly router: Router) {}

  navigate(route: string): void {
    this.router.navigate([route]);
  }
}
