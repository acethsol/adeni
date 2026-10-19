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
  type AdeniExportMenuItem,
  type AdeniFilterSelectOption,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { ADMIN_LIST_PAGE_SIZE } from "../../shared/admin-list-paging";
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
  readonly hasSearched = signal(false);
  readonly error = signal<string | null>(null);
  readonly rows = signal<AdminTransactionSearchItem[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = ADMIN_LIST_PAGE_SIZE;

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
    { id: "when", header: "When", cell: () => "" },
    { id: "type", header: "Type", cell: () => "" },
    { id: "business", header: "Business", cell: (row) => row.tenantName },
    { id: "reference", header: "Reference", cell: (row) => row.providerReference ?? row.id },
    { id: "amount", header: "Amount", cell: () => "" },
    { id: "status", header: "Status", cell: (row) => row.status },
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
      if (q) {
        void this.runSearch(q);
      } else {
        this.hasSearched.set(false);
        this.rows.set([]);
        this.totalCount.set(0);
      }
    });
  }

  onSearchInput(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounce) {
      clearTimeout(this.searchDebounce);
    }
    this.searchDebounce = setTimeout(() => {
      const q = value.trim();
      if (!q) {
        void this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { q: null, page: null },
          queryParamsHandling: "merge",
        });
        return;
      }
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: {
          q,
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
    const q = this.searchQuery().trim();
    if (!q) {
      return;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { kind: kind === "all" ? null : kind, page: null },
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

  private async runSearch(q: string): Promise<void> {
    this.searching.set(true);
    this.hasSearched.set(true);
    this.error.set(null);

    try {
      await this.feedback.runLoading(async () => {
        const result = await this.api.withAuthorizedClient((client) =>
          client.searchAdminTransactions(q, {
            kind: this.kindFilter(),
            page: this.page(),
            pageSize: this.pageSize,
          }),
        );
        this.rows.set(result.items);
        this.totalCount.set(result.totalCount);
      }, "Searching transactions", "Matching Paystack and business records");
    } catch (err) {
      this.error.set(adminLoadErrorMessage("Could not search transactions.", err));
      this.feedback.error(this.error() ?? "Could not search transactions.");
      this.rows.set([]);
      this.totalCount.set(0);
    } finally {
      this.searching.set(false);
    }
  }
}
