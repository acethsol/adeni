import { DatePipe } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router } from "@angular/router";
import type { AdminReviewListItem } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";

@Component({
  selector: "app-admin-reviews",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    DatePipe,
  ],
  templateUrl: "./reviews.component.html",
  styleUrl: "./reviews.component.scss",
})
export class AdminReviewsComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<AdminReviewListItem[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = 25;
  readonly searchText = signal("");
  readonly includeHidden = signal(false);
  readonly busyId = signal<string | null>(null);

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize)),
  );
  readonly isEmpty = computed(() => !this.loading() && this.totalCount() === 0);
  readonly showingRange = computed(() => {
    if (this.totalCount() === 0) {
      return "0";
    }
    const start = (this.page() - 1) * this.pageSize + 1;
    const end = Math.min(this.page() * this.pageSize, this.totalCount());
    return `${start}–${end} of ${this.totalCount()}`;
  });

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const q = params.get("q") ?? "";
      const page = Number(params.get("page") ?? "1");
      const includeHidden = params.get("includeHidden") === "1";
      this.searchText.set(q);
      this.includeHidden.set(includeHidden);
      if (Number.isFinite(page) && page >= 1) {
        this.page.set(page);
      }
      void this.load();
    });
  }

  ngOnInit(): void {
    // Initial load is driven by queryParamMap subscription.
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const result = await this.api.withAuthorizedClient((c) =>
          c.listAdminReviews({
            q: this.searchText().trim() || undefined,
            page: this.page(),
            pageSize: this.pageSize,
            includeHidden: this.includeHidden(),
          }),
        );
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
      }, "Loading reviews…", "Moderation queue");
    } catch {
      const message = "Could not load reviews.";
      this.error.set(message);
      this.feedback.error(message);
      this.items.set([]);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  applyFilters(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        q: this.searchText().trim() || null,
        page: 1,
        includeHidden: this.includeHidden() ? "1" : null,
      },
      queryParamsHandling: "merge",
    });
  }

  goToPage(next: number): void {
    const page = Math.min(Math.max(1, next), this.totalPages());
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: page === 1 ? null : page },
      queryParamsHandling: "merge",
    });
  }

  async hideReview(item: AdminReviewListItem): Promise<void> {
    if (item.isHidden) {
      return;
    }
    const ok = await this.confirmDialog.confirm({
      title: "Hide review?",
      message: `Remove this review from public profiles for ${item.businessName}?`,
      confirmLabel: "Hide",
      danger: true,
    });
    if (!ok) {
      return;
    }

    this.busyId.set(item.id);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.hideAdminReview(item.id)),
        "Hiding review…",
        item.businessName,
      );
      if (this.includeHidden()) {
        this.items.update((list) =>
          list.map((row) => (row.id === item.id ? { ...row, isHidden: true } : row)),
        );
      } else {
        this.items.update((list) => list.filter((row) => row.id !== item.id));
        this.totalCount.update((count) => Math.max(0, count - 1));
      }
      this.feedback.success("Review hidden from discovery.", "Hidden");
    } catch {
      this.error.set("Hide failed.");
      this.feedback.error("Hide failed.");
    } finally {
      this.busyId.set(null);
    }
  }
}
