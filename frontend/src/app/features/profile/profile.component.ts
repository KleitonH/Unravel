import { Component, inject, signal, computed, OnInit } from "@angular/core";
import { Router } from "@angular/router";
import { CommonModule } from "@angular/common";
import { ProfileService } from "../../core/services/profile.service";
import { AuthService } from "../../core/services/auth.service";
import {
  ProfileResponse,
  StudentProfile,
  ModeratorProfile,
  isStudentProfile,
  isModeratorProfile,
} from "../../core/models/profile.model";
import { BottomNavComponent } from "../../shared/components/bottom-nav/bottom-nav.component";

@Component({
  selector: "app-profile",
  standalone: true,
  imports: [CommonModule, BottomNavComponent],
  templateUrl: "./profile.component.html",
  styleUrl: "./profile.component.scss",
})
export class ProfileComponent implements OnInit {
  private readonly profileService = inject(ProfileService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal("");
  readonly profile = signal<ProfileResponse | null>(null);

  readonly studentProfile = computed(() => {
    const p = this.profile();
    return p && isStudentProfile(p) ? p : null;
  });

  readonly moderatorProfile = computed(() => {
    const p = this.profile();
    return p && isModeratorProfile(p) ? p : null;
  });

  readonly level = computed(() => {
    const sp = this.studentProfile();
    return sp ? Math.floor(sp.xp / 500) + 1 : 1;
  });

  readonly xpInLevel = computed(() => {
    const sp = this.studentProfile();
    return sp ? sp.xp % 500 : 0;
  });

  readonly xpPercent = computed(() => Math.min((this.xpInLevel() / 500) * 100, 100));

  ngOnInit(): void {
    this.profileService.getProfile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.loading.set(false);
      },
      error: () => {
        this.error.set("Erro ao carregar perfil.");
        this.loading.set(false);
      },
    });
  }

  initials(name: string): string {
    return name
      .split(" ")
      .slice(0, 2)
      .map((n) => n[0])
      .join("")
      .toUpperCase();
  }

  goToContents(): void {
    this.router.navigate(["/moderator/contents"]);
  }

  goBack(): void {
    this.router.navigate(["/dashboard"]);
  }

  logout(): void {
    this.authService.logout();
  }

  progressColor(progress: number): string {
    if (progress >= 100) return "#38db8c";
    if (progress >= 50) return "#7038f2";
    return "#bc9cfe";
  }
}
