import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import type { TenantCustomerListItem, TenantEntitlements } from "@adeni/shared";
import {
  buildReportExportMenuItems,
  DEFAULT_PAGE_SIZE,
  formatSlotTime,
  LIST_PAGE_SIZE_OPTIONS,
  listSortParamsFromColumn,
  mergeTenantEntitlements,
  REPORT_KEYS,
  type ReportExportFormat,
} from "@adeni/shared";
import {
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniExportMenuComponent,
  AdeniFeedbackService,
  AdeniFilterSelectComponent,
  AdeniIconBtnComponent,
  AdeniListFilterRowComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniTablePersonCellComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniExportMenuItem,
  type AdeniFilterSelectOption,
  type AdeniListViewMode,
  type AdeniPillTone,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

export type CustomerAccountFilter = "all" | "discover" | "walk_in";

@Component({
  selector: "app-customers",
  standalone: true,
  imports: [
    PortalPageComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniListFilterRowComponent,
    AdeniFilterSelectComponent,
    AdeniExportMenuComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    AdeniListPaginationComponent,
    AdeniTablePersonCellComponent,
    AdeniPillComponent,
    RouterLink,
  ],
  templateUrl: "./customers.component.html",
  styleUrl: "./customers.component.scss",
})
export class CustomersComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<TenantCustomerListItem[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly searchQuery = signal("");
  readonly viewMode = signal<AdeniListViewMode>("table");
  readonly accountFilter = signal<CustomerAccountFilter>("all");
  readonly sortColumnId = signal<string | null>(null);
  readonly sortDirection = signal<AdeniDataTableSortDirection>("asc");
  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly accountFilterOptions: AdeniFilterSelectOption[] = [
    { value: "all", label: "All profiles" },
    { value: "discover", label: "Discover account" },
    { value: "walk_in", label: "Walk-in / queue" },
  ];

  readonly entitlements = signal<TenantEntitlements | null>(null);
  readonly exporting = signal(false);

  readonly exportItems = computed((): AdeniExportMenuItem[] =>
    buildReportExportMenuItems({
      entitlements: this.entitlements(),
      xlsx: true,
      pdf: { disabled: true, hint: "Customer list is CSV or Excel only" },
    }),
  );

  readonly columns: AdeniDataTableColumn<TenantCustomerListItem>[] = [
    {
      id: "name",
      header: "Customer",
      cell: (row) => row.displayName,
      sortValue: (row) => row.displayName.toLowerCase(),
    },
    {
      id: "email",
      header: "Email",
      cell: (row) => row.email || "—",
      sortValue: (row) => (row.email || "").toLowerCase(),
    },
    {
      id: "account",
      header: "Profile type",
      cell: (row) => this.accountLabel(row),
      sortValue: (row) => (row.platformCustomerId ? 1 : 0),
    },
    {
      id: "lastSeen",
      header: "Last seen",
      cell: (row) => formatSlotTime(row.lastSeenAt),
      sortValue: (row) => row.lastSeenAt,
    },
  ];

  readonly trackRow = (row: TenantCustomerListItem): string => row.id;
  readonly formatSlotTime = formatSlotTime;

  ngOnInit(): void {
    void this.load();
  }

  onSearchChange(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounce) {
      clearTimeout(this.searchDebounce);
    }
    this.searchDebounce = setTimeout(() => {
      this.page.set(1);
      void this.load();
    }, 300);
  }

  onAccountFilterChange(value: CustomerAccountFilter): void {
    if (this.accountFilter() === value) {
      return;
    }
    this.accountFilter.set(value);
    this.page.set(1);
    void this.load();
  }

  goToPage(next: number): void {
    this.page.set(next);
    void this.load();
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
    void this.load();
  }

  onSortColumnChange(columnId: string | null): void {
    this.sortColumnId.set(columnId);
    this.page.set(1);
    void this.load();
  }

  onSortDirectionChange(direction: AdeniDataTableSortDirection): void {
    this.sortDirection.set(direction);
    this.page.set(1);
    void this.load();
  }

  customerInitials(row: TenantCustomerListItem): string {
    const parts = row.displayName.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0]!.charAt(0) + parts[1]!.charAt(0)).toUpperCase();
    }
    const word = parts[0] ?? "?";
    return word.slice(0, 2).toUpperCase();
  }

  accountLabel(row: TenantCustomerListItem): string {
    return row.platformCustomerId ? "Discover account" : "Walk-in / queue";
  }

  accountPillTone(row: TenantCustomerListItem): AdeniPillTone {
    return row.platformCustomerId ? "info" : "neutral";
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const term = this.searchQuery().trim();
        const filter = this.accountFilter();
        await this.api.withAuthorizedClient(async (client) => {
          const [result, profile, usage, planEntitlements] = await Promise.all([
            client.listTenantCustomers({
              search: term.length > 0 ? term : undefined,
              page: this.page(),
              pageSize: this.pageSize(),
              discover: filter === "all" ? undefined : filter,
              sort: listSortParamsFromColumn(this.sortColumnId(), this.sortDirection()),
            }),
            client.getTenantProfile().catch(() => null),
            client.getTenantSubscriptionUsage().catch(() => null),
            client.getTenantEntitlements().catch(() => null),
          ]);
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.entitlements.set(
            mergeTenantEntitlements(planEntitlements, usage?.entitlements, profile?.entitlements),
          );
        });
      }, "Loading customers", "Fetching profiles");
    } catch {
      this.error.set("Could not load customers. Try again.");
      this.feedback.error("Could not load customers.");
      this.items.set([]);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  async downloadCustomersExport(format: ReportExportFormat): Promise<void> {
    this.exporting.set(true);
    try {
      const to = new Date();
      const from = new Date(to);
      from.setFullYear(from.getFullYear() - 1);
      const blob = await this.api.withAuthorizedClient((client) =>
        client.downloadTenantReportExport({
          reportKey: REPORT_KEYS.tenantCustomers,
          from: from.toISOString(),
          to: to.toISOString(),
          format,
        }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `tenant-customers-${to.toISOString().slice(0, 10)}.${format === "xlsx" ? "xlsx" : "csv"}`;
      anchor.click();
      URL.revokeObjectURL(url);
      this.feedback.success("Customer list downloaded.");
    } catch {
      this.feedback.error("Could not download customer list.");
    } finally {
      this.exporting.set(false);
    }
  }
}
