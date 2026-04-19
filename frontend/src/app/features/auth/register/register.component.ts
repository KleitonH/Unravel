import { Component, inject, signal, computed } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly selectedInterest = signal('web');

  readonly interestPills = [
    { key: 'web', emoji: '🔥', label: 'Dev Web' },
    { key: 'bd',  emoji: '🗄️', label: 'Banco de Dados' },
    { key: 'sec', emoji: '🔒', label: 'Segurança' },
    { key: 'ai',  emoji: '🤖', label: 'IA' },
  ];

  readonly form = this.fb.group({
    name:            ['', Validators.required],
    email:           ['', [Validators.required, Validators.email]],
    password:        ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
    terms:           [false, Validators.requiredTrue]
  });

  readonly passwordMismatch = computed(() => {
    const p = this.form.get('password')?.value;
    const c = this.form.get('confirmPassword')?.value;
    return !!(c && p !== c);
  });

  passwordStrength(): { pct: number; label: string; color: string } {
    const val = this.form.get('password')?.value ?? '';
    const has = (re: RegExp) => re.test(val);
    let score = 0;
    if (val.length >= 8) score++;
    if (val.length >= 12) score++;
    if (has(/[A-Z]/)) score++;
    if (has(/[0-9]/)) score++;
    if (has(/[^A-Za-z0-9]/)) score++;
    if (score <= 1) return { pct: 20, label: 'Força: Fraca',  color: 'var(--red)' };
    if (score <= 3) return { pct: 55, label: 'Força: Média',  color: 'var(--gold)' };
    return              { pct: 100, label: 'Força: Forte',  color: 'var(--green)' };
  }

  updateStrength() { /* triggers computed */ }

  showError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  onSubmit(): void {
    if (this.form.invalid || this.passwordMismatch()) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.errorMessage.set('');
    const { name, email, password } = this.form.getRawValue();
    this.authService.register({ name: name!, email: email!, password: password! }).subscribe({
      next: () => this.router.navigate(['/auth/login']),
      error: (err) => {
        this.errorMessage.set(err?.error?.error ?? 'Erro ao criar conta. Tente novamente.');
        this.loading.set(false);
      }
    });
  }
}
