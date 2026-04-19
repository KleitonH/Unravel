import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  readonly authService = inject(AuthService);
  readonly activeNav = signal('home');

  readonly trails = [
    { key: 'bd',  emoji: '🗄️', name: 'Banco de Dados',      meta: '12 módulos · Iniciante',      color: '#2CE086' },
    { key: 'sec', emoji: '🔒',  name: 'Segurança da Info',   meta: '10 módulos · Intermediário',  color: '#E144F5' },
    { key: 'ai',  emoji: '🤖',  name: 'Inteligência Artif.', meta: '15 módulos · Avançado',        color: '#FFCC00' },
  ];
}
