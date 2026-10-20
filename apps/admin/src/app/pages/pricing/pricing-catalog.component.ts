import { DatePipe } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import type { PricingCatalogVersionSummary } from "@adeni/shared";
import { formatPrice, formatSubscriptionSku, type SubscriptionSku } from "@adeni/shared";
import {
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";

type PricingCatalogGridRow = {
  rowKey: string;
  catalogVersionCode: string;
  countryCode: string;
  subscriptionSku: SubscriptionSku;
  currencyCode: string;
  monthlyAmountMajor: number | null | undefined;
  platformFeePercent: number;
  isSelfServeEnabled: boolean;
};

@Component({
  selector: "app-admin-pricing-catalog",
  standalone: true,
  imports: [
    PortalPageComponent,
    DatePipe,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniPillComponent,
  ],
  templateUrl: "./pricing-catalog.component.html",
  styleUrl: "./pricing-catalog.component.scss",
})
export class AdminPricingCatalogComponent implements OnInit {
  private readonly api = inject(AdminApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly activeVersion = signal<PricingCatalogVersionSummary | null>(null);
  readonly rows = signal<PricingCatalogGridRow[]>([]);
  readonly searchText = signal("");

  readonly filteredRows = computed(() => {
    const query = this.searchText().trim().toLowerCase();
    const items = this.rows();
    if (!query) {
      return items;
    }
    return items.filter((row) => {
      const skuLabel = formatSubscriptionSku(row.subscriptionSku).toLowerCase();
      return (
        row.countryCode.toLowerCase().includes(query) ||
        row.subscriptionSku.toLowerCase().includes(query) ||
        skuLabel.includes(query) ||
        row.catalogVersionCode.toLowerCase().includes(query) ||
        row.currencyCode.toLowerCase().includes(query)
      );
    });
  });

  readonly isEmptyCatalog = computed(
    () => !this.loading() && this.rows().length === 0 && !this.error(),
  );

  readonly columns: AdeniDataTableColumn<PricingCatalogGridRow>[] = [
    { id: "version", header: "Catalog version", cell: (row) => row.catalogVersionCode },
    { id: "country", header: "Country", cell: (row) => row.countryCode },
    { id: "sku", header: "Plan (SKU)", cell: (row) => formatSubscriptionSku(row.subscriptionSku) },
    { id: "price", header: "List price", cell: () => "" },
    { id: "gmv", header: "Platform fee", cell: (row) => `${row.platformFeePercent}%` },
    { id: "selfServe", header: "Self-serve", cell: () => "" },
  ];

  readonly trackRow = (row: PricingCatalogGridRow): string => row.rowKey;

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const versions = await this.api.withAuthorizedClient((client) =>
        client.listAdminPricingCatalogVersions(),
      );
      const active = versions.find((v) => v.isActive) ?? null;
      this.activeVersion.set(active);

      if (!active) {
        this.rows.set([]);
        return;
      }

      const detail = await this.api.withAuthorizedClient((client) =>
        client.getAdminPricingCatalogVersion(active.code),
      );

      this.rows.set(
        detail.entries.map((entry) => ({
          rowKey: `${detail.code}:${entry.countryCode}:${entry.subscriptionSku}`,
          catalogVersionCode: detail.code,
          countryCode: entry.countryCode,
          subscriptionSku: entry.subscriptionSku,
          currencyCode: entry.currencyCode,
          monthlyAmountMajor: entry.monthlyAmountMajor,
          platformFeePercent: entry.platformFeePercent,
          isSelfServeEnabled: entry.isSelfServeEnabled,
        })),
      );
    } catch (err) {
      this.rows.set([]);
      this.activeVersion.set(null);
      this.error.set(adminLoadErrorMessage("Could not load pricing catalog.", err));
    } finally {
      this.loading.set(false);
    }
  }

  onToolbarSearch(value: string): void {
    this.searchText.set(value);
  }

  formatSku(sku: SubscriptionSku): string {
    return formatSubscriptionSku(sku);
  }

  formatAmount(currency: string, amount: number | null | undefined): string {
    if (amount == null) {
      return "—";
    }
    return formatPrice(amount, currency);
  }
}
