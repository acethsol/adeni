import { Component, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import type { TenantCustomerListItem } from "@adeni/shared";
import { formatSlotTime } from "@adeni/shared";
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
  readonly pageSize = 25;
  readonly searchQuery = signal("");
  readonly viewMode = signal<AdeniListViewMode>("table");
  readonly accountFilter = signal<CustomerAccountFilter>("all");
  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly accountFilterOptions: AdeniFilterSelectOption[] = [
    { value: "all", label: "All profiles" },
    { value: "discover", label: "Discover account" },
    { value: "walk_in", label: "Walk-in / queue" },
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

  readonly columns: AdeniDataTableColumn<TenantCustomerListItem>[] = [
    { id: "name", header: "Customer", cell: (row) => row.displayName },
    { id: "email", header: "Email", cell: (row) => row.email || "—" },
    {
      id: "account",
      header: "Profile type",
      cell: (row) => this.accountLabel(row),
    },
    {
      id: "lastSeen",
      header: "Last seen",
      cell: (row) => formatSlotTime(row.lastSeenAt),
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
        const result = await this.api.withAuthorizedClient((client) =>
          client.listTenantCustomers({
            search: term.length > 0 ? term : undefined,
            page: this.page(),
            pageSize: this.pageSize,
            discover: filter === "all" ? undefined : filter,
          }),
        );
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
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
}
