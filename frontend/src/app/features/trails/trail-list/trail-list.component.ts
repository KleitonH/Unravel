import { Component, inject, signal, computed, OnInit } from "@angular/core";
import { Router } from "@angular/router";
import { CommonModule } from "@angular/common";
import { TrailService } from "../../../core/services/trail.service";
import { TrailResponse } from "../../../core/models/trail.model";
import { BottomNavComponent } from "../../../shared/components/bottom-nav/bottom-nav.component";

type Filter = "todas" | "em curso" | "novas";

@Component({
  selector: "app-trail-list",
  standalone: true,
  imports: [CommonModule, BottomNavComponent],
  templateUrl: "./trail-list.component.html",
  styleUrl: "./trail-list.component.scss",
})
export class TrailListComponent implements OnInit {
  private readonly trailService = inject(TrailService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal("");
  readonly trails = signal<TrailResponse[]>([]);
  readonly filter = signal<Filter>("todas");
  readonly filters: Filter[] = ["todas", "em curso", "novas"];

  readonly filteredTrails = computed(() => {
    const f = this.filter();
    const all = this.trails();
    if (f === "em curso") return all.filter((t) => t.userProgress >= 0);
    if (f === "novas") return all.filter((t) => t.userProgress < 0);
    return all;
  });

  ngOnInit(): void {
    this.trailService.getAll().subscribe({
      next: (trails) => {
        this.trails.set(trails);
        this.loading.set(false);
      },
      error: () => {
        this.error.set("Erro ao carregar trilhas.");
        this.loading.set(false);
      },
    });
  }

  goToTrail(id: number): void {
    this.router.navigate(["/trails", id]);
  }
  goBack(): void {
    this.router.navigate(["/dashboard"]);
  }

  badgeColor(level: string): string {
    const map: Record<string, string> = {
      Iniciante: "#7038f2",
      Intermediário: "#38db8c",
      Avançado: "#ed54f2",
    };
    return map[level] ?? "#7038f2";
  }

  capitalize(s: string): string {
    return s.charAt(0).toUpperCase() + s.slice(1);
  }
}
