import { Component, computed, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { Subscription } from "rxjs";
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
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { BusinessActiveLocationService } from "../../core/services/business-active-location.service";
import { BusinessPresenceVerifyService } from "../../core/services/business-presence-verify.service";
import { PresenceRequiredPanelComponent } from "../attendance/presence-required-panel.component";

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
    PresenceRequiredPanelComponent,
  ],
  templateUrl: "./customers.component.html",
  styleUrl: "./customers.component.scss",
})
export class CustomersComponent implements OnInit, OnDestroy {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly activeLocation = inject(BusinessActiveLocationService);
  private readonly presenceVerify = inject(BusinessPresenceVerifyService);
  private loadGeneration = 0;
  private branchSub: Subscription | null = null;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly presenceBlocked = signal(false);
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

  readonly accountFilterOptions = computed<AdeniFilterSelectOption[]>(() => [
    { value: "all", label: this.tr("businessApp.customers.allProfiles") },
    { value: "discover", label: this.tr("businessApp.customers.discoverAccount") },
    { value: "walk_in", label: this.tr("businessApp.customers.walkQueue") },
  ]);

  readonly entitlements = signal<TenantEntitlements | null>(null);
  readonly exporting = signal(false);

  /** Registry supports csv/xlsx only for tenant-customers (no PDF builder). */
  readonly exportItems = computed((): AdeniExportMenuItem[] =>
    buildReportExportMenuItems({
      entitlements: this.entitlements(),
      xlsx: true,
    }),
  );

  readonly columns = computed<AdeniDataTableColumn<TenantCustomerListItem>[]>(() => [
    {
      id: "name",
      header: this.tr("common.customer"),
      cell: (row) => row.displayName,
      sortValue: (row) => row.displayName.toLowerCase(),
    },
    {
      id: "email",
      header: this.tr("common.email"),
      cell: (row) => row.email || "—",
      sortValue: (row) => (row.email || "").toLowerCase(),
    },
    {
      id: "account",
      header: this.tr("businessApp.customers.profileType"),
      cell: (row) => this.accountLabel(row),
      sortValue: (row) => (row.platformCustomerId ? 1 : 0),
    },
    {
      id: "lastSeen",
      header: this.tr("businessApp.customers.lastSeen"),
      cell: (row) => formatSlotTime(row.lastSeenAt),
      sortValue: (row) => row.lastSeenAt,
    },
  ]);

  readonly trackRow = (row: TenantCustomerListItem): string => row.id;
  readonly formatSlotTime = formatSlotTime;

  ngOnInit(): void {
    void this.load();
    this.branchSub = this.activeLocation.selectionChanged$.subscribe(() => {
      this.page.set(1);
      void this.activeLocation.runOnBranchSwitch(() => this.load());
    });
  }

  ngOnDestroy(): void {
    this.branchSub?.unsubscribe();
    this.loadGeneration++;
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
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
    return row.platformCustomerId ? this.tr("businessApp.customers.discoverAccount") : this.tr("businessApp.customers.walkQueue");
  }

  accountPillTone(row: TenantCustomerListItem): AdeniPillTone {
    return row.platformCustomerId ? "info" : "neutral";
  }

  async load(): Promise<void> {
    const generation = ++this.loadGeneration;
    this.loading.set(true);
    this.error.set(null);
    this.presenceBlocked.set(false);
    try {
      await this.feedback.runLoading(async () => {
        const term = this.searchQuery().trim();
        const filter = this.accountFilter();
        await this.api.withAuthorizedClient(async (client) => {
          const profile = await client.getTenantProfile().catch(() => null);
          const planEntitlements = await client.getTenantEntitlements().catch(() => null);
          const entitlementsForLocation = mergeTenantEntitlements(
            planEntitlements,
            profile?.entitlements,
            profile?.entitlements,
          );
          const locationQueryId = this.activeLocation.tenantApiLocationId(entitlementsForLocation);
          const usage = await client.getTenantSubscriptionUsage(locationQueryId).catch(() => null);
          const entitlements = mergeTenantEntitlements(
            planEntitlements,
            usage?.entitlements,
            profile?.entitlements,
          );
          const result = await client.listTenantCustomers({
            search: term.length > 0 ? term : undefined,
            page: this.page(),
            pageSize: this.pageSize(),
            discover: filter === "all" ? undefined : filter,
            sort: listSortParamsFromColumn(this.sortColumnId(), this.sortDirection()),
            locationId: this.activeLocation.tenantApiLocationId(entitlements),
          });
          if (generation !== this.loadGeneration) return;
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.entitlements.set(entitlements);
        });
      });
    } catch (caught) {
      if (generation !== this.loadGeneration) return;
      if (this.presenceVerify.isPresenceGateError(caught)) {
        this.presenceBlocked.set(true);
        this.error.set(null);
        return;
      }
      this.error.set(this.apiErr(caught, "businessApp.customers.couldNotLoadCustomersTry"));
      this.feedback.error(this.apiErr(caught, "businessApp.customers.couldNotLoadCustomers"));
      this.items.set([]);
      this.totalCount.set(0);
    } finally {
      if (generation === this.loadGeneration) this.loading.set(false);
    }
  }

  async verifyPresenceForCustomers(): Promise<void> {
    try {
      await this.presenceVerify.verifyAtActiveLocation();
      await this.load();
    } catch {
      this.feedback.error(this.tr("businessApp.attendance.presenceVerifyFailed"));
    }
  }

  async downloadCustomersExport(format: ReportExportFormat): Promise<void> {
    this.exporting.set(true);
    try {
      const to = new Date();
      const from = new Date(to);
      from.setFullYear(from.getFullYear() - 1);
      const blob = await this.api.withAuthorizedClient(async (client) => {
        const profile = await client.getTenantProfile().catch(() => null);
        return client.downloadTenantReportExport({
          reportKey: REPORT_KEYS.tenantCustomers,
          from: from.toISOString(),
          to: to.toISOString(),
          format,
          locationId: this.activeLocation.tenantApiLocationId(profile?.entitlements ?? null),
        });
      });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `tenant-customers-${to.toISOString().slice(0, 10)}.${format === "xlsx" ? "xlsx" : "csv"}`;
      anchor.click();
      URL.revokeObjectURL(url);
      this.feedback.success(this.tr("businessApp.customers.customerListDownloaded"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.customers.couldNotDownloadCustomerList"));
    } finally {
      this.exporting.set(false);
    }
  }
}
