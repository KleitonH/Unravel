import { Component, inject, signal, computed, OnInit } from "@angular/core";
import { Router } from "@angular/router";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { forkJoin } from "rxjs";
import {
  ModeratorService,
  CreateContentDto,
  UpdateContentDto,
} from "../../../core/services/moderator.service";
import { TrailService } from "../../../core/services/trail.service";
import { ContentResponse, TrailResponse } from "../../../core/models/trail.model";

interface ContentForm {
  trailId: number;
  title: string;
  body: string;
  externalUrl: string;
  type: number;
  level: number;
  order: number;
}

const BLANK_FORM: ContentForm = {
  trailId: 0,
  title: "",
  body: "",
  externalUrl: "",
  type: 1,
  level: 1,
  order: 1,
};

@Component({
  selector: "app-moderator-contents",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./moderator-contents.component.html",
  styleUrl: "./moderator-contents.component.scss",
})
export class ModeratorContentsComponent implements OnInit {
  private readonly moderatorService = inject(ModeratorService);
  private readonly trailService = inject(TrailService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal("");
  readonly contents = signal<ContentResponse[]>([]);
  readonly trails = signal<TrailResponse[]>([]);
  readonly selectedTrailId = signal<number | null>(null);

  readonly filteredContents = computed(() => {
    const tid = this.selectedTrailId();
    const all = this.contents();
    return tid !== null ? all.filter((c) => c.trailId === tid) : all;
  });

  readonly drawerOpen = signal(false);
  readonly drawerMode = signal<"create" | "edit">("create");
  readonly editingId = signal<number | null>(null);
  readonly deleteConfirmId = signal<number | null>(null);

  form: ContentForm = { ...BLANK_FORM };

  readonly typeOptions = [
    { value: 1, label: "Artigo" },
    { value: 2, label: "Vídeo" },
    { value: 3, label: "Exercício" },
  ];

  readonly levelOptions = [
    { value: 1, label: "Iniciante" },
    { value: 2, label: "Intermediário" },
    { value: 3, label: "Avançado" },
  ];

  private readonly typeMap: Record<string, number> = {
    Article: 1,
    Video: 2,
    Exercise: 3,
  };

  private readonly levelMap: Record<string, number> = {
    Beginner: 1,
    Intermediate: 2,
    Advanced: 3,
  };

  ngOnInit(): void {
    forkJoin({
      contents: this.moderatorService.getContents(),
      trails: this.trailService.getAll(),
    }).subscribe({
      next: ({ contents, trails }) => {
        this.contents.set(contents);
        this.trails.set(trails);
        this.loading.set(false);
      },
      error: () => {
        this.error.set("Erro ao carregar conteúdos.");
        this.loading.set(false);
      },
    });
  }

  selectTrail(id: number | null): void {
    this.selectedTrailId.set(id);
  }

  openCreate(): void {
    this.form = {
      ...BLANK_FORM,
      trailId: this.selectedTrailId() ?? (this.trails()[0]?.id ?? 0),
    };
    this.editingId.set(null);
    this.drawerMode.set("create");
    this.drawerOpen.set(true);
  }

  openEdit(content: ContentResponse): void {
    this.form = {
      trailId: content.trailId,
      title: content.title,
      body: content.body,
      externalUrl: content.externalUrl ?? "",
      type: this.typeMap[content.type] ?? 1,
      level: this.levelMap[content.level] ?? 1,
      order: content.order,
    };
    this.editingId.set(content.id);
    this.drawerMode.set("edit");
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  saveContent(): void {
    if (!this.form.title.trim() || !this.form.body.trim()) return;

    this.saving.set(true);
    const id = this.editingId();

    if (id !== null) {
      const dto: UpdateContentDto = {
        title: this.form.title,
        body: this.form.body,
        externalUrl: this.form.externalUrl || null,
        type: Number(this.form.type),
        level: Number(this.form.level),
        order: Number(this.form.order),
      };
      this.moderatorService.updateContent(id, dto).subscribe({
        next: (updated) => {
          this.contents.update((list) =>
            list.map((c) => (c.id === id ? updated : c)),
          );
          this.saving.set(false);
          this.closeDrawer();
        },
        error: () => this.saving.set(false),
      });
    } else {
      const dto: CreateContentDto = {
        trailId: Number(this.form.trailId),
        title: this.form.title,
        body: this.form.body,
        externalUrl: this.form.externalUrl || null,
        type: Number(this.form.type),
        level: Number(this.form.level),
        order: Number(this.form.order),
      };
      this.moderatorService.createContent(dto).subscribe({
        next: (created) => {
          this.contents.update((list) => [...list, created]);
          this.saving.set(false);
          this.closeDrawer();
        },
        error: () => this.saving.set(false),
      });
    }
  }

  confirmDelete(id: number): void {
    this.deleteConfirmId.set(id);
  }

  cancelDelete(): void {
    this.deleteConfirmId.set(null);
  }

  deleteContent(id: number): void {
    this.moderatorService.deleteContent(id).subscribe({
      next: () => {
        this.contents.update((list) => list.filter((c) => c.id !== id));
        this.deleteConfirmId.set(null);
      },
    });
  }

  trailNameFor(trailId: number): string {
    return this.trails().find((t) => t.id === trailId)?.name ?? "—";
  }

  typeLabelFor(type: string): string {
    const map: Record<string, string> = {
      Article: "Artigo",
      Video: "Vídeo",
      Exercise: "Exercício",
    };
    return map[type] ?? type;
  }

  levelLabelFor(level: string): string {
    const map: Record<string, string> = {
      Beginner: "Iniciante",
      Intermediate: "Intermediário",
      Advanced: "Avançado",
    };
    return map[level] ?? level;
  }

  goBack(): void {
    this.router.navigate(["/profile"]);
  }
}
