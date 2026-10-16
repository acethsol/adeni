import { Component, computed, inject, signal } from "@angular/core";
import { DatePipe } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import type { AdminTransactionSearchItem } from "@adeni/shared";
import {
  formatAmountMinor,
  formatBillingChargeStatus,
  formatPrice,
  formatSubscriptionSku,
} from "@adeni/shared";
import { AdeniCarbonIconComponent, AdeniStaffButtonComponent, PortalPageComponent } from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import {
  ADMIN_LIST_PAGE_SIZE,
  adminListRangeLabel,
  adminListTotalPages,
} from "../../shared/admin-list-paging";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { AdminPillComponent } from "../../shared/admin-pill.component";

type KindFilter = "all" | "commerce" | "saas";

@Component({
  selector: "app-admin-transactions",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    RouterLink,
    DatePipe,
    AdeniCarbonIconComponent,
    AdeniStaffButtonComponent,
    AdminPillComponent,
  ],
  templateUrl: "./transactions.component.html",
  styleUrl: "./transactions.component.scss",
})
export class AdminTransactionsComponent {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  searchInput = "";
  readonly kindFilter = signal<KindFilter>("all");
  readonly searching = signal(false);
  readonly hasSearched = signal(false);
  readonly error = signal<string | null>(null);
  readonly rows = signal<AdminTransactionSearchItem[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = ADMIN_LIST_PAGE_SIZE;

  readonly totalPages = computed(() => adminListTotalPages(this.totalCount(), this.pageSize));
  readonly showingRange = computed(() =>
    adminListRangeLabel(this.page(), this.pageSize, this.totalCount()),
  );

  readonly formatSubscriptionSku = formatSubscriptionSku;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const q = (params.get("q") ?? "").trim();
      const kind = params.get("kind");
      const page = Number(params.get("page") ?? "1");
      if (q && q !== this.searchInput) {
        this.searchInput = q;
      }
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

  setKind(kind: KindFilter): void {
    this.kindFilter.set(kind);
    const q = this.searchInput.trim();
    if (!q) {
      return;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { q, kind: kind === "all" ? null : kind, page: null },
    });
  }

  search(): void {
    const q = this.searchInput.trim();
    if (!q) {
      return;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        q,
        kind: this.kindFilter() === "all" ? null : this.kindFilter(),
        page: null,
      },
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
      const result = await this.api.withAuthorizedClient((client) =>
        client.searchAdminTransactions(q, {
          kind: this.kindFilter(),
          page: this.page(),
          pageSize: this.pageSize,
        }),
      );
      this.rows.set(result.items);
      this.totalCount.set(result.totalCount);
    } catch (err) {
      this.error.set(adminLoadErrorMessage("Could not search transactions.", err));
      this.rows.set([]);
      this.totalCount.set(0);
    } finally {
      this.searching.set(false);
    }
  }
}
