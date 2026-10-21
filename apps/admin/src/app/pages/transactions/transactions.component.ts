import { DatePipe } from "@angular/common";
import { Component, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import type { AdminTransactionSearchItem } from "@adeni/shared";
import {
  formatAmountMinor,
  formatPrice,
  formatSubscriptionSku,
  formatBillingChargeStatus,
  listSortParamsFromColumn,
} from "@adeni/shared";
import {
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniExportMenuComponent,
  AdeniFeedbackService,
  AdeniFilterSelectComponent,
  AdeniListFilterRowComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniExportMenuItem,
  type AdeniFilterSelectOption,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { ADMIN_LIST_PAGE_SIZE, LIST_PAGE_SIZE_OPTIONS } from "../../shared/admin-list-paging";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { AdminPillComponent } from "../../shared/admin-pill.component";

type KindFilter = "all" | "commerce" | "saas";

@Component({
  selector: "app-admin-transactions",
  standalone: true,
  imports: [
    PortalPageComponent,
    RouterLink,
    DatePipe,
    AdeniListToolbarComponent,
    AdeniListFilterRowComponent,
    AdeniFilterSelectComponent,
    AdeniExportMenuComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniListPaginationComponent,
    AdeniPortalEmptyStateComponent,
    AdminPillComponent,
  ],
  templateUrl: "./transactions.component.html",
  styleUrl: "./transactions.component.scss",
})
export class AdminTransactionsComponent {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly searchQuery = signal("");
  readonly kindFilter = signal<KindFilter>("all");
  readonly searching = signal(false);
  readonly loaded = signal(false);
  readonly browseMode = signal(true);
  readonly error = signal<string | null>(null);
  readonly rows = signal<AdminTransactionSearchItem[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(ADMIN_LIST_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly sortColumnId = signal<string | null>("when");
  readonly sortDirection = signal<AdeniDataTableSortDirection>("desc");

  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly kindFilterOptions: AdeniFilterSelectOption[] = [
    { value: "all", label: "All types" },
    { value: "commerce", label: "Commerce" },
    { value: "saas", label: "SaaS" },
  ];

  readonly exportItems: AdeniExportMenuItem[] = [
    {
      format: "csv",
      label: "CSV (.csv)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
    {
      format: "xlsx",
      label: "Excel (.xlsx)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
    {
      format: "pdf",
      label: "PDF (.pdf)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
  ];

  readonly columns: AdeniDataTableColumn<AdminTransactionSearchItem>[] = [
    {
      id: "when",
      header: "When",
      cell: () => "",
      sortValue: (row) => row.occurredAt,
    },
    {
      id: "type",
      header: "Type",
      cell: () => "",
      sortValue: (row) => row.kind,
    },
    {
      id: "business",
      header: "Business",
      cell: (row) => row.tenantName,
      sortValue: (row) => row.tenantName.toLowerCase(),
    },
    {
      id: "reference",
      header: "Reference",
      cell: (row) => row.providerReference ?? row.id,
      sortValue: (row) => (row.providerReference ?? row.id).toLowerCase(),
    },
    {
      id: "amount",
      header: "Amount",
      align: "right",
      cell: () => "",
      sortValue: (row) => row.amountMinor ?? 0,
    },
    {
      id: "status",
      header: "Status",
      cell: (row) => row.status,
      sortValue: (row) => row.status,
    },
  ];

  readonly trackRow = (row: AdminTransactionSearchItem): string => row.id;

  readonly formatSubscriptionSku = formatSubscriptionSku;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const q = (params.get("q") ?? "").trim();
      const kind = params.get("kind");
      const page = Number(params.get("page") ?? "1");
      this.searchQuery.set(q);
      this.kindFilter.set(kind === "commerce" || kind === "saas" ? kind : "all");
      this.page.set(Number.isFinite(page) && page >= 1 ? page : 1);
      this.browseMode.set(q.length === 0);
      void this.loadTransactions(q.length > 0 ? q : null);
    });
  }

  onSearchInput(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounce) {
      clearTimeout(this.searchDebounce);
    }
    this.searchDebounce = setTimeout(() => {
      const q = value.trim();
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: {
          q: q || null,
          kind: this.kindFilter() === "all" ? null : this.kindFilter(),
          page: null,
        },
        queryParamsHandling: "merge",
      });
    }, 400);
  }

  onKindChange(value: string): void {
    const kind = value === "commerce" || value === "saas" ? value : "all";
    this.kindFilter.set(kind);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        q: this.searchQuery().trim() || null,
        kind: kind === "all" ? null : kind,
        page: null,
      },
      queryParamsHandling: "merge",
    });
  }

  goToPage(next: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: next <= 1 ? null : next },
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

  kindLabel(row: AdminTransactionSearchItem): string {
    return row.kind === "commerce" ? "Commerce" : "SaaS";
  }

  kindVariant(row: AdminTransactionSearchItem): "info" | "neutral" {
    return row.kind === "commerce" ? "info" : "neutral";
  }

  formatAmount(row: AdminTransactionSearchItem): string {
    if (row.kind === "saas") {
      if (row.amountMinor != null) {
        return formatAmountMinor(row.amountMinor, row.currency);
      }
      return formatBillingChargeStatus(row.status);
    }
    if (row.amount != null) {
      const base = formatPrice(row.amount, row.currency);
      if (row.platformFeeAmount != null && row.platformFeeAmount > 0) {
        return `${base} · fee ${formatPrice(row.platformFeeAmount, row.currency)}`;
      }
      return base;
    }
    return row.status;
  }

  referenceLabel(row: AdminTransactionSearchItem): string {
    return row.providerReference?.trim() || row.id;
  }

  private async loadTransactions(queryText: string | null): Promise<void> {
    this.searching.set(true);
    this.error.set(null);

    try {
      const result = await this.api.withAuthorizedClient((client) =>
        client.searchAdminTransactions(queryText, {
          kind: this.kindFilter(),
          page: this.page(),
          pageSize: this.pageSize(),
          sort: listSortParamsFromColumn(this.sortColumnId(), this.sortDirection()),
        }),
      );
      this.rows.set(result.items);
      this.totalCount.set(result.totalCount);
    } catch (err) {
      this.error.set(adminLoadErrorMessage("Could not load transactions.", err));
      this.feedback.error(this.error() ?? "Could not load transactions.");
      this.rows.set([]);
      this.totalCount.set(0);
    } finally {
      this.searching.set(false);
      this.loaded.set(true);
    }
  }
}
