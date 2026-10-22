import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { type PendingBadgeRequest, VERIFICATION_BADGE_LABELS } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  AdeniListPaginationComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import {
  ADMIN_LIST_PAGE_SIZE,
  adminListRangeLabel,
  adminListTotalPages,
  LIST_PAGE_SIZE_OPTIONS,
} from "../../shared/admin-list-paging";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { AdminPillComponent } from "../../shared/admin-pill.component";
import { badgeTypePill } from "../../shared/admin-pill-maps";

@Component({
  selector: "app-admin-trust-badges",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniCarbonIconComponent,
    AdeniStaffButtonComponent,
    AdeniListPaginationComponent,
    AdeniPortalEmptyStateComponent,
    DatePipe,
    RouterLink,
    AdminPillComponent,
  ],
  templateUrl: "./trust-badges.component.html",
  styleUrl: "./trust-badges.component.scss",
})
export class AdminTrustBadgesComponent {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<PendingBadgeRequest[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(ADMIN_LIST_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly searchText = signal("");
  readonly busyKey = signal<string | null>(null);

  readonly totalPages = computed(() => adminListTotalPages(this.totalCount(), this.pageSize()));
  readonly showingRange = computed(() =>
    adminListRangeLabel(this.page(), this.pageSize(), this.totalCount()),
  );
  readonly isEmptyQueue = computed(() => !this.loading() && this.totalCount() === 0);

  readonly badgeLabel = (type: PendingBadgeRequest["badgeType"]) => VERIFICATION_BADGE_LABELS[type];
  readonly badgeTypePill = badgeTypePill;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.searchText.set(params.get("q") ?? "");
      const page = Number(params.get("page") ?? "1");
      this.page.set(Number.isFinite(page) && page >= 1 ? page : 1);
      void this.load();
    });
  }

  rowKey(item: PendingBadgeRequest): string {
    return `${item.tenantId}:${item.badgeType}`;
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const result = await this.api.withAuthorizedClient((c) =>
          c.listAdminPendingBadges({
            q: this.searchText().trim() || undefined,
            page: this.page(),
            pageSize: this.pageSize(),
          }),
        );
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
      }, "Loading badge queue…", "Trust badge requests");
    } catch (err) {
      const message = adminLoadErrorMessage("Could not load pending trust badges.", err);
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

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: null },
      queryParamsHandling: "merge",
    });
  }

  async grant(item: PendingBadgeRequest): Promise<void> {
    const key = this.rowKey(item);
    this.busyKey.set(key);
    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.grantAdminVerificationBadge(item.tenantId, item.badgeType),
          ),
        "Granting…",
        item.businessName,
      );
      await this.load();
      this.feedback.success(`${this.badgeLabel(item.badgeType)} granted for ${item.businessName}.`, "Granted");
    } catch (caught) {
      this.error.set(this.apiErr(caught, "admin.trustBadges.grantFailed"));
      this.feedback.error(this.apiErr(caught, "admin.trustBadges.grantFailed"));
    } finally {
      this.busyKey.set(null);
    }
  }

  async deny(item: PendingBadgeRequest): Promise<void> {
    const key = this.rowKey(item);
    this.busyKey.set(key);
    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.denyAdminVerificationBadge(item.tenantId, item.badgeType),
          ),
        "Denying…",
        item.businessName,
      );
      await this.load();
      this.feedback.success(`Request denied for ${item.businessName}.`, this.tr("admin.trustBadges.denied"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "admin.trustBadges.denyFailed"));
      this.feedback.error(this.apiErr(caught, "admin.trustBadges.denyFailed"));
    } finally {
      this.busyKey.set(null);
    }
  }
}
