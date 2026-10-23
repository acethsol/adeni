import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router } from "@angular/router";
import type { AdminAuditLogListItem } from "@adeni/shared";
import {
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniFeedbackService,
  AdeniFilterSelectComponent,
  AdeniListFilterRowComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniFilterSelectOption,
  injectAdeniTranslate,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { AdminPillComponent } from "../../shared/admin-pill.component";
import {
  ADMIN_LIST_PAGE_SIZE,
  adminListTotalPages,
  LIST_PAGE_SIZE_OPTIONS,
} from "../../shared/admin-list-paging";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";

/** Preset filters — API matches action exactly (see AdminAuditLogQueryService). */
const AUDIT_ACTION_PRESETS = [
  "",
  "admin.audit_logs.read",
  "admin.reporting.read",
  "admin.metrics.read",
  "admin.transactions.read",
  "business.approved",
  "business.rejected",
  "booking.accepted",
  "tenant.settings_updated",
  "tenant.reporting.exported",
  "security.cross_tenant_denied",
  "subscription.webhook_received",
] as const;

@Component({
  selector: "app-admin-audit-logs",
  standalone: true,
  imports: [
    PortalPageComponent,
    DatePipe,
    AdeniListToolbarComponent,
    AdeniListFilterRowComponent,
    AdeniFilterSelectComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniListPaginationComponent,
    AdeniPortalEmptyStateComponent,
    AdeniStaffButtonComponent,
    AdminPillComponent,
  ],
  templateUrl: "./audit-logs.component.html",
  styleUrl: "./audit-logs.component.scss",
})
export class AdminAuditLogsComponent {
  protected readonly tr = injectAdeniTranslate();
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(false);
  readonly loaded = signal(false);
  readonly error = signal<string | null>(null);
  readonly rows = signal<AdminAuditLogListItem[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(ADMIN_LIST_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly actionFilter = signal("");
  readonly customAction = signal("");

  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly totalPages = computed(() => adminListTotalPages(this.totalCount(), this.pageSize()));

  readonly actionFilterOptions = computed<AdeniFilterSelectOption[]>(() =>
    AUDIT_ACTION_PRESETS.map((value) => ({
      value,
      label: value === "" ? this.tr("admin.auditLogs.allActions") : value,
    })),
  );

  readonly effectiveAction = computed(() => {
    const preset = this.actionFilter().trim();
    if (preset) {
      return preset;
    }
    return this.customAction().trim();
  });

  readonly columns = computed<AdeniDataTableColumn<AdminAuditLogListItem>[]>(() => [
    {
      id: "when",
      header: this.tr("portal.bookings.when"),
      cell: () => "",
      sortValue: (row) => row.occurredAt,
    },
    {
      id: "action",
      header: this.tr("admin.auditLogs.action"),
      cell: (row) => row.action,
      sortValue: (row) => row.action,
    },
    {
      id: "actor",
      header: this.tr("admin.auditLogs.actor"),
      cell: (row) => this.maskActor(row.actorId),
      sortValue: (row) => row.actorId,
    },
    {
      id: "entity",
      header: this.tr("admin.auditLogs.entity"),
      cell: (row) => `${row.entityType}:${row.entityId}`,
      sortValue: (row) => row.entityType,
    },
    {
      id: "correlation",
      header: this.tr("admin.auditLogs.correlation"),
      cell: (row) => row.correlationId,
      sortValue: (row) => row.correlationId,
    },
  ]);

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const action = (params.get("action") ?? "").trim();
      const page = Number(params.get("page") ?? "1");
      if (AUDIT_ACTION_PRESETS.includes(action as (typeof AUDIT_ACTION_PRESETS)[number])) {
        this.actionFilter.set(action);
        this.customAction.set("");
      } else {
        this.actionFilter.set("");
        this.customAction.set(action);
      }
      this.page.set(Number.isFinite(page) && page >= 1 ? page : 1);
      void this.load();
    });
  }

  onPresetActionChange(value: string): void {
    this.actionFilter.set(value);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { action: value.trim() || null, page: null },
    });
  }

  onCustomActionSearch(query: string): void {
    this.customAction.set(query);
    if (this.searchDebounce) {
      clearTimeout(this.searchDebounce);
    }
    this.searchDebounce = setTimeout(() => {
      const trimmed = query.trim();
      if (this.actionFilter()) {
        return;
      }
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { action: trimmed || null, page: null },
      });
    }, 350);
  }

  refresh(): void {
    void this.load();
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

  trackRow = (row: AdminAuditLogListItem) => row.id;

  actionPillVariant(action: string): "neutral" | "info" | "warning" | "danger" {
    if (action.startsWith("security.")) {
      return "danger";
    }
    if (action.includes("webhook") || action.startsWith("admin.")) {
      return "info";
    }
    return "neutral";
  }

  private maskActor(actorId: string): string {
    if (actorId.startsWith("system:")) {
      return actorId;
    }
    const at = actorId.indexOf("@");
    if (at > 1) {
      return `${actorId.slice(0, 2)}…${actorId.slice(at)}`;
    }
    if (actorId.length > 14) {
      return `${actorId.slice(0, 10)}…`;
    }
    return actorId;
  }

  private maskCorrelationId(correlationId: string): string {
    if (correlationId.length <= 16) {
      return correlationId;
    }
    return `${correlationId.slice(0, 8)}…${correlationId.slice(-4)}`;
  }

  displayCorrelation(row: AdminAuditLogListItem): string {
    return this.maskCorrelationId(row.correlationId);
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const action = this.effectiveAction() || undefined;
        const result = await this.api.withAuthorizedClient((c) =>
          c.listAdminAuditLogs({
            action,
            page: this.page(),
            pageSize: this.pageSize(),
          }),
        );
        this.rows.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loaded.set(true);
      }, this.tr("admin.auditLogs.loading"));
    } catch (err) {
      const message = adminLoadErrorMessage(this.tr("admin.auditLogs.loadFailed"), err);
      this.error.set(message);
      this.feedback.error(message);
      this.rows.set([]);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }
}
