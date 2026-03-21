import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-container">
      <div class="auth-card">
        <h1>Create Account</h1>

        @if (errorMessage()) {
          <div class="error-banner">{{ errorMessage() }}</div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label for="name">Full Name</label>
            <input
              id="name"
              type="text"
              formControlName="name"
              placeholder="John Doe"
              autocomplete="name"
            />
            @if (form.get('name')?.invalid && form.get('name')?.touched) {
              <span class="field-error">Name is required.</span>
            }
          </div>

          <div class="form-group">
            <label for="email">Email</label>
            <input
              id="email"
              type="email"
              formControlName="email"
              placeholder="you@example.com"
              autocomplete="email"
            />
            @if (form.get('email')?.invalid && form.get('email')?.touched) {
              <span class="field-error">Valid email is required.</span>
            }
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <input
              id="password"
              type="password"
              formControlName="password"
              placeholder="At least 8 characters"
              autocomplete="new-password"
            />
            @if (form.get('password')?.invalid && form.get('password')?.touched) {
              <span class="field-error">Password must be at least 8 characters.</span>
            }
          </div>

          <button type="submit" [disabled]="loading() || form.invalid">
            {{ loading() ? 'Creating account...' : 'Create Account' }}
          </button>
        </form>

        <p class="auth-link">
          Already have an account? <a routerLink="/auth/login">Sign in</a>
        </p>
      </div>
    </div>
  `,
  styles: [`
    .auth-container {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
    }
    .auth-card {
      background: white;
      padding: 2rem;
      border-radius: 8px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.1);
      width: 100%;
      max-width: 400px;
    }
    h1 { margin-bottom: 1.5rem; font-size: 1.5rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; margin-bottom: 1rem; }
    label { font-size: 0.875rem; font-weight: 500; }
    input { padding: 0.5rem 0.75rem; border: 1px solid #ddd; border-radius: 4px; font-size: 1rem; }
    button { width: 100%; padding: 0.75rem; background: #4f46e5; color: white; border: none; border-radius: 4px; font-size: 1rem; cursor: pointer; margin-top: 0.5rem; }
    button:disabled { opacity: 0.6; cursor: not-allowed; }
    .error-banner { background: #fee2e2; color: #dc2626; padding: 0.75rem; border-radius: 4px; margin-bottom: 1rem; font-size: 0.875rem; }
    .field-error { color: #dc2626; font-size: 0.75rem; }
    .auth-link { margin-top: 1rem; text-align: center; font-size: 0.875rem; }
    .auth-link a { color: #4f46e5; text-decoration: none; }
  `]
})
export class RegisterComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly errorMessage = signal('');

  readonly form = this.fb.group({
    name: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]]
  });

  onSubmit(): void {
    if (this.form.invalid) return;

    this.loading.set(true);
    this.errorMessage.set('');

    const { name, email, password } = this.form.getRawValue();

    this.authService.register({ name: name!, email: email!, password: password! }).subscribe({
      next: () => this.router.navigate(['/auth/login']),
      error: (err) => {
        this.errorMessage.set(err?.error?.error ?? 'Registration failed. Please try again.');
        this.loading.set(false);
      }
    });
  }
}
