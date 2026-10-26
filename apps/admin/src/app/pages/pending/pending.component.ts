import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import {
  buildAdminReportExportMenuItems,
  listSortParamsFromColumn,
  REPORT_KEYS,
  VERIFICATION_DOCUMENT_LABELS,
  type PendingBusiness,
  type ReportExportFormat,
} from "@adeni/shared";
import {
  AdeniConfirmService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniExportMenuComponent,
  AdeniFeedbackService,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniModalComponent,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniExportMenuItem,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { AdminOperationsRealtimeService } from "@adeni/ui";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import {
  adminListExportFileName,
  downloadAdminListExport,
  triggerBrowserDownload,
} from "../../shared/admin-report-export";
import { AdminPillComponent } from "../../shared/admin-pill.component";
import { docCountPill, tenantStatusPill } from "../../shared/admin-pill-maps";
import {
  ADMIN_LIST_PAGE_SIZE,
  adminListRangeLabel,
  adminListTotalPages,
  LIST_PAGE_SIZE_OPTIONS,
} from "../../shared/admin-list-paging";

@Component({
  selector: "app-admin-pending",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniListToolbarComponent,
    AdeniExportMenuComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniListPaginationComponent,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    AdeniModalComponent,
    DatePipe,
    RouterLink,
    AdminPillComponent,
  ],
  templateUrl: "./pending.component.html",
  styleUrl: "./pending.component.scss",
})
export class AdminPendingComponent {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly operationsRealtime = inject(AdminOperationsRealtimeService);
  private readonly confirm = inject(AdeniConfirmService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<PendingBusiness[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(ADMIN_LIST_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly searchText = signal("");
  readonly sortColumnId = signal<string | null>(null);
  readonly sortDirection = signal<AdeniDataTableSortDirection>("asc");
  readonly busyId = signal<string | null>(null);
  readonly expandedRowId = signal<string | null>(null);
  readonly rejectModalOpen = signal(false);
  readonly rejectTarget = signal<PendingBusiness | null>(null);
  rejectReasonDraft = "";

  readonly totalPages = computed(() => adminListTotalPages(this.totalCount(), this.pageSize()));
  readonly showingRange = computed(() =>
    adminListRangeLabel(this.page(), this.pageSize(), this.totalCount()),
  );
  readonly isEmptyQueue = computed(() => !this.loading() && this.totalCount() === 0);

  readonly exportItems: AdeniExportMenuItem[] = buildAdminReportExportMenuItems({
    xlsx: true,
  });
  readonly exporting = signal(false);

  readonly columns = computed<AdeniDataTableColumn<PendingBusiness>[]>(() => [
    {
      id: "business",
      header: this.tr("common.business"),
      cell: (item) => item.name,
      sortValue: (item) => item.name.toLowerCase(),
    },
    {
      id: "market",
      header: this.tr("common.market"),
      cell: (item) => item.marketId,
      sortValue: (item) => item.marketId,
    },
    {
      id: "submitted",
      header: this.tr("admin.pending.submitted"),
      cell: () => "",
      sortValue: (item) => item.submittedAt ?? item.createdAt,
    },
    {
      id: "docs",
      header: this.tr("admin.pending.docs"),
      cell: () => "",
      sortValue: (item) => item.documents?.length ?? 0,
    },
  ]);

  readonly trackRow = (item: PendingBusiness): string => item.id;

  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly tenantStatusPill = tenantStatusPill;
  readonly docCountPill = docCountPill;

  docTypeLabel(documentType: string): string {
    const map: Record<string, string> = {
      cac: VERIFICATION_DOCUMENT_LABELS[0],
      nationalid: VERIFICATION_DOCUMENT_LABELS[1],
      addressproof: VERIFICATION_DOCUMENT_LABELS[2],
      tradelicense: VERIFICATION_DOCUMENT_LABELS[3],
    };
    return map[documentType.toLowerCase()] ?? documentType;
  }

  submittedAt(item: PendingBusiness): string {
    return item.submittedAt ?? item.createdAt;
  }

  toggleExpanded(item: PendingBusiness): void {
    this.expandedRowId.update((current) => (current === item.id ? null : item.id));
  }

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

  async onExport(format: ReportExportFormat): Promise<void> {
    this.exporting.set(true);
    try {
      const blob = await this.api.withAuthorizedClient((client) =>
        downloadAdminListExport(client, {
          reportKey: REPORT_KEYS.adminPendingVerifications,
          format,
          q: this.searchText().trim() || undefined,
        }),
      );
      triggerBrowserDownload(
        blob,
        adminListExportFileName(REPORT_KEYS.adminPendingVerifications, format),
      );
      this.feedback.success(this.tr("admin.pending.pendingQueueDownloaded"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "common.exportFailed"));
    } finally {
      this.exporting.set(false);
    }
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
            pageSize: this.pageSize(),
            sort: listSortParamsFromColumn(this.sortColumnId(), this.sortDirection()),
          }),
        );
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
      }, this.tr("common.loadingQueue"), this.tr("admin.pending.fetchingBusinessesAwaitingVerification"));
    } catch (err) {
      const message = adminLoadErrorMessage(this.tr("admin.pending.couldNotLoadPendingBusinesses"), err);
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

  onSortColumnChange(columnId: string | null): void {
    this.sortColumnId.set(columnId);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: null },
      queryParamsHandling: "merge",
    });
  }

  onSortDirectionChange(direction: AdeniDataTableSortDirection): void {
    this.sortDirection.set(direction);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: null },
      queryParamsHandling: "merge",
    });
  }

  async approve(item: PendingBusiness): Promise<void> {
    const confirmed = await this.confirm.confirm({
      title: this.tr("admin.pending.confirmApproveTitle"),
      message: this.tr("admin.pending.confirmApproveBody", {
        name: item.name,
        marketId: item.marketId,
      }),
      confirmLabel: this.tr("admin.pending.approve"),
      cancelLabel: this.tr("common.cancel"),
    });
    if (!confirmed) {
      return;
    }

    this.busyId.set(item.id);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.approvePendingBusiness(item.id)),
        this.tr("admin.pending.approving"),
        item.name,
      );
      await this.load();
      this.feedback.success(`${item.name} is now verified.`, this.tr("admin.pending.approved"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "admin.pending.approveFailed"));
    } finally {
      this.busyId.set(null);
    }
  }

  openRejectModal(item: PendingBusiness): void {
    this.rejectTarget.set(item);
    this.rejectReasonDraft = "";
    this.rejectModalOpen.set(true);
  }

  closeRejectModal(): void {
    this.rejectModalOpen.set(false);
    this.rejectTarget.set(null);
    this.rejectReasonDraft = "";
  }

  async confirmReject(): Promise<void> {
    const item = this.rejectTarget();
    const reason = this.rejectReasonDraft.trim();
    if (!item) {
      return;
    }
    if (reason.length < 10) {
      this.feedback.error(this.tr("admin.pending.rejectReasonTooShort"));
      return;
    }

    this.busyId.set(item.id);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.rejectPendingBusiness(item.id, reason)),
        this.tr("admin.pending.rejecting"),
        item.name,
      );
      this.closeRejectModal();
      await this.load();
      this.feedback.success(`${item.name} was rejected.`, this.tr("admin.pending.rejected"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "admin.pending.rejectFailed"));
    } finally {
      this.busyId.set(null);
    }
  }

  async viewUploadedFile(item: PendingBusiness, documentId: string): Promise<void> {
    try {
      const payload = await this.api.withAuthorizedClient((c) =>
        c.getAdminVerificationDocumentDownloadUrl(item.id, documentId),
      );
      window.open(payload.downloadUrl, "_blank", "noopener,noreferrer");
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "common.exportFailed"));
    }
  }
}
