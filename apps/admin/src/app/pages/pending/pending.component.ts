import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import type { PendingBusiness } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { AdminOperationsRealtimeService } from "@adeni/ui";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { AdminPillComponent } from "../../shared/admin-pill.component";
import { docCountPill, tenantStatusPill } from "../../shared/admin-pill-maps";
import {
  ADMIN_LIST_PAGE_SIZE,
  adminListRangeLabel,
  adminListTotalPages,
} from "../../shared/admin-list-paging";

@Component({
  selector: "app-admin-pending",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniCarbonIconComponent,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    DatePipe,
    RouterLink,
    AdminPillComponent,
  ],
  templateUrl: "./pending.component.html",
  styleUrl: "./pending.component.scss",
})
export class AdminPendingComponent {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly operationsRealtime = inject(AdminOperationsRealtimeService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<PendingBusiness[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = ADMIN_LIST_PAGE_SIZE;
  readonly searchText = signal("");
  readonly busyId = signal<string | null>(null);

  readonly totalPages = computed(() => adminListTotalPages(this.totalCount(), this.pageSize));
  readonly showingRange = computed(() =>
    adminListRangeLabel(this.page(), this.pageSize, this.totalCount()),
  );
  readonly isEmptyQueue = computed(() => !this.loading() && this.totalCount() === 0);

  readonly tenantStatusPill = tenantStatusPill;
  readonly docCountPill = docCountPill;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.searchText.set(params.get("q") ?? "");
      const page = Number(params.get("page") ?? "1");
      this.page.set(Number.isFinite(page) && page >= 1 ? page : 1);
      void this.load();
    });
    this.operationsRealtime.adminVerificationChanged$
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        void this.load();
      });
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const result = await this.api.withAuthorizedClient((c) =>
          c.listPendingBusinesses({
            q: this.searchText().trim() || undefined,
            page: this.page(),
            pageSize: this.pageSize,
          }),
        );
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
      }, "Loading queue…", "Fetching businesses awaiting verification");
    } catch (err) {
      const message = adminLoadErrorMessage("Could not load pending businesses.", err);
      this.error.set(message);
      this.feedback.error(message);
      this.items.set([]);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  applySearch(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { q: this.searchText().trim() || null, page: null },
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

  async approve(item: PendingBusiness): Promise<void> {
    this.busyId.set(item.id);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.approvePendingBusiness(item.id)),
        "Approving…",
        item.name,
      );
      await this.load();
      this.feedback.success(`${item.name} is now verified.`, "Approved");
    } catch {
      this.error.set("Approve failed.");
      this.feedback.error("Approve failed.");
    } finally {
      this.busyId.set(null);
    }
  }

  async reject(item: PendingBusiness): Promise<void> {
    const reason = prompt("Rejection reason (min 10 chars) for " + item.name + "?");
    if (!reason || reason.trim().length < 10) return;
    this.busyId.set(item.id);
    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) => c.rejectPendingBusiness(item.id, reason.trim())),
        "Rejecting…",
        item.name,
      );
      await this.load();
      this.feedback.success(`${item.name} was rejected.`, "Rejected");
    } catch {
      this.error.set("Reject failed.");
      this.feedback.error("Reject failed.");
    } finally {
      this.busyId.set(null);
    }
  }
}
