import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="dashboard">
      <header>
        <h1>Dashboard</h1>
        <button (click)="authService.logout()">Sign Out</button>
      </header>
      @if (authService.currentUser(); as user) {
        <p>Welcome, <strong>{{ user.name }}</strong>!</p>
        <p>{{ user.email }}</p>
      }
    </div>
  `,
  styles: [`
    .dashboard { padding: 2rem; max-width: 1200px; margin: 0 auto; }
    header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; }
    button { padding: 0.5rem 1rem; background: #ef4444; color: white; border: none; border-radius: 4px; cursor: pointer; }
  `]
})
export class DashboardComponent {
  readonly authService = inject(AuthService);
}
