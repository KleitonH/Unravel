import { Component, inject, signal, computed, OnInit } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { CommonModule } from "@angular/common";
import { forkJoin } from "rxjs";
import { TrailService } from "../../../core/services/trail.service";
import {
  TrailResponse,
  ContentResponse,
} from "../../../core/models/trail.model";

type NodeState = "done" | "current" | "locked";

interface ContentNode extends ContentResponse {
  state: NodeState;
}

interface ContentGroup {
  label: string;
  nodes: ContentNode[];
}

const LEVEL_ORDER = ["Iniciante", "Intermediário", "Avançado"];

@Component({
  selector: "app-trail-detail",
  standalone: true,
  imports: [CommonModule],
  templateUrl: "./trail-detail.component.html",
  styleUrl: "./trail-detail.component.scss",
})
export class TrailDetailComponent implements OnInit {
  private readonly trailService = inject(TrailService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly trail = signal<TrailResponse | null>(null);
  readonly contents = signal<ContentResponse[]>([]);
  readonly loading = signal(true);
  readonly expandedId = signal<number | null>(null);
  readonly enrolling = signal(false);
  readonly completing = signal<number | null>(null);

  readonly trailId = computed(() =>
    Number(this.route.snapshot.paramMap.get("id")),
  );

  readonly isEnrolled = computed(() => (this.trail()?.userProgress ?? -1) >= 0);
  readonly progress = computed(() =>
    Math.max(0, this.trail()?.userProgress ?? 0),
  );
  readonly doneCount = computed(
    () => this.contents().filter((c) => c.isCompleted).length,
  );

  readonly groups = computed((): ContentGroup[] => {
    const all = this.contents();
    const firstIncomplete = all.findIndex((c) => !c.isCompleted);

    const withState: ContentNode[] = all.map((c, i) => ({
      ...c,
      state: c.isCompleted
        ? "done"
        : i === firstIncomplete
          ? "current"
          : "locked",
    }));

    return LEVEL_ORDER.reduce((acc, level) => {
      const nodes = withState.filter((c) => c.level === level);
      if (nodes.length) acc.push({ label: level, nodes });
      return acc;
    }, [] as ContentGroup[]);
  });

  ngOnInit(): void {
    const id = this.trailId();
    forkJoin({
      trail: this.trailService.getById(id),
      contents: this.trailService.getSequence(id),
    }).subscribe({
      next: ({ trail, contents }) => {
        this.trail.set(trail);
        this.contents.set(contents);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  goBack(): void {
    this.router.navigate(["/trails"]);
  }

  toggleExpand(id: number, locked: boolean): void {
    if (locked) return;
    this.expandedId.set(this.expandedId() === id ? null : id);
  }

  enroll(): void {
    this.enrolling.set(true);
    this.trailService.enroll(this.trailId()).subscribe({
      next: (p) => {
        this.trail.update((t) => (t ? { ...t, userProgress: p.progress } : t));
        this.enrolling.set(false);
      },
      error: () => this.enrolling.set(false),
    });
  }

  complete(contentId: number): void {
    this.completing.set(contentId);
    this.trailService.completeContent(contentId).subscribe({
      next: (p) => {
        this.contents.update((list) =>
          list.map((c) =>
            c.id === contentId ? { ...c, isCompleted: true } : c,
          ),
        );
        this.trail.update((t) => (t ? { ...t, userProgress: p.progress } : t));
        this.expandedId.set(null);
        this.completing.set(null);
      },
      error: () => this.completing.set(null),
    });
  }
}
