import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import type { AdminAuditLogListItem } from "@adeni/shared";
import {
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniFeedbackService,
  AdeniListPaginationComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  injectAdeniApiError,
  injectAdeniTranslate,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import {
  ADMIN_LIST_PAGE_SIZE,
  adminListRangeLabel,
  adminListTotalPages,
  LIST_PAGE_SIZE_OPTIONS,
} from "../../shared/admin-list-paging";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";

@Component({
  selector: "app-admin-audit-logs",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    DatePipe,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniListPaginationComponent,
    AdeniPortalEmptyStateComponent,
  ],
  templateUrl: "./audit-logs.component.html",
  styleUrl: "./audit-logs.component.scss",
})
export class AdminAuditLogsComponent {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);

  actionFilterInput = "";
  readonly loading = signal(false);
  readonly loaded = signal(false);
  readonly error = signal<string | null>(null);
  readonly rows = signal<AdminAuditLogListItem[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(ADMIN_LIST_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;

  readonly totalPages = computed(() => adminListTotalPages(this.totalCount(), this.pageSize()));
  readonly showingRange = computed(() =>
    adminListRangeLabel(this.page(), this.pageSize(), this.totalCount()),
  );

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
      if (action !== this.actionFilterInput) {
        this.actionFilterInput = action;
      }
      this.page.set(Number.isFinite(page) && page >= 1 ? page : 1);
      void this.load();
    });
  }

  applyActionFilter(): void {
    const action = this.actionFilterInput.trim();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { action: action || null, page: null },
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

  trackRow = (row: AdminAuditLogListItem) => row.id;

  private maskActor(actorId: string): string {
    const at = actorId.indexOf("@");
    if (at <= 1) {
      return actorId;
    }
    return `${actorId.slice(0, 2)}…${actorId.slice(at)}`;
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const action = this.actionFilterInput.trim() || undefined;
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
