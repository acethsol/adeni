import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { type PendingBadgeRequest, VERIFICATION_BADGE_LABELS } from "@adeni/shared";
import {
  AdminOperationsRealtimeService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniConfirmService,
  AdeniFeedbackService,
  AdeniIconBtnComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
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
import { badgeTypePill } from "../../shared/admin-pill-maps";

@Component({
  selector: "app-admin-trust-badges",
  standalone: true,
  imports: [
    PortalPageComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniListPaginationComponent,
    AdeniPortalEmptyStateComponent,
    AdeniPillComponent,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    DatePipe,
    RouterLink,
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
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly operationsRealtime = inject(AdminOperationsRealtimeService);

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
  /** True empty queue only — an empty search result keeps the toolbar visible. */
  readonly isEmptyQueue = computed(
    () => !this.loading() && this.totalCount() === 0 && !this.searchText().trim(),
  );

  readonly badgeLabel = (type: PendingBadgeRequest["badgeType"]) => VERIFICATION_BADGE_LABELS[type];
  readonly badgeTypePill = badgeTypePill;

  readonly columns = computed<AdeniDataTableColumn<PendingBadgeRequest>[]>(() => [
    {
      id: "business",
      header: this.tr("common.business"),
      cell: (item) => item.businessName,
      sortValue: (item) => item.businessName.toLowerCase(),
    },
    {
      id: "badge",
      header: this.tr("admin.trustBadges.badge"),
      cell: (item) => this.badgeLabel(item.badgeType),
      sortValue: (item) => item.badgeType,
    },
    {
      id: "reference",
      header: this.tr("admin.trustBadges.reference"),
      cell: (item) => item.referenceNumber || "—",
      sortValue: (item) => item.referenceNumber ?? "",
    },
    {
      id: "requested",
      header: this.tr("admin.trustBadges.requested"),
      cell: () => "",
      sortValue: (item) => item.requestedAt,
    },
  ]);

  readonly trackRow = (item: PendingBadgeRequest): string => this.rowKey(item);

  /** Client-side sort of the current page (the API has no sort param for this queue). */
  readonly sortColumnId = signal<string | null>(null);
  readonly sortDirection = signal<AdeniDataTableSortDirection>("asc");

  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.searchText.set(params.get("q") ?? "");
      const page = Number(params.get("page") ?? "1");
      this.page.set(Number.isFinite(page) && page >= 1 ? page : 1);
      void this.load();
    });
    this.operationsRealtime.adminBadgesChanged$.pipe(takeUntilDestroyed()).subscribe(() => {
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
      }, this.tr("admin.trustBadges.loadingBadgeQueue"), this.tr("admin.trustBadges.trustBadgeRequests"));
    } catch (err) {
      const message = adminLoadErrorMessage(this.tr("admin.trustBadges.couldNotLoadPendingTrust"), err);
      this.error.set(message);
      this.feedback.error(message);
      this.items.set([]);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  onToolbarSearch(value: string): void {
    this.searchText.set(value);
    if (this.searchDebounce) {
      clearTimeout(this.searchDebounce);
    }
    this.searchDebounce = setTimeout(() => {
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { q: value.trim() || null, page: null },
        queryParamsHandling: "merge",
      });
    }, 350);
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
    const ok = await this.confirmDialog.confirmKind("grant", { subject: this.badgeSubject(item) });
    if (!ok) {
      return;
    }
    const key = this.rowKey(item);
    this.busyKey.set(key);
    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.grantAdminVerificationBadge(item.tenantId, item.badgeType),
          ),
        this.tr("admin.trustBadges.granting"),
        item.businessName,
      );
      await this.load();
      this.feedback.success(`${this.badgeLabel(item.badgeType)} granted for ${item.businessName}.`, this.tr("admin.trustBadges.granted"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "admin.trustBadges.grantFailed"));
      this.feedback.error(this.apiErr(caught, "admin.trustBadges.grantFailed"));
    } finally {
      this.busyKey.set(null);
    }
  }

  private badgeSubject(item: PendingBadgeRequest): string {
    return this.tr("admin.trustBadges.badgeSubject", {
      badge: this.badgeLabel(item.badgeType),
      businessName: item.businessName,
    });
  }

  async deny(item: PendingBadgeRequest): Promise<void> {
    const ok = await this.confirmDialog.confirmKind("deny", { subject: this.badgeSubject(item) });
    if (!ok) {
      return;
    }
    const key = this.rowKey(item);
    this.busyKey.set(key);
    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.denyAdminVerificationBadge(item.tenantId, item.badgeType),
          ),
        this.tr("admin.trustBadges.denying"),
        item.businessName,
      );
      await this.load();
      this.feedback.success(this.tr("admin.trustBadges.requestDenied", { businessName: item.businessName }), this.tr("admin.trustBadges.denied"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "admin.trustBadges.denyFailed"));
      this.feedback.error(this.apiErr(caught, "admin.trustBadges.denyFailed"));
    } finally {
      this.busyKey.set(null);
    }
  }
}
