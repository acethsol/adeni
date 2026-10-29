import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import type { TenantSubscriptionCharge } from "@adeni/shared";
import { formatBillingChargeStatus, formatSubscriptionSku } from "@adeni/shared";
import {
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  injectAdeniTranslate,
} from "@adeni/ui";
import {
  chargeMatchesSearch,
  chargeStatusTone,
  formatChargeAmount,
} from "./business-charge-grid";
import { BusinessDetailFacade } from "./business-detail.facade";

@Component({
  selector: "app-admin-business-billing",
  standalone: true,
  imports: [
    DatePipe,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniPillComponent,
  ],
  templateUrl: "./business-billing.component.html",
  styleUrl: "./business-billing.component.scss",
})
export class BusinessBillingComponent {
  protected readonly tr = injectAdeniTranslate();
  readonly facade = inject(BusinessDetailFacade);

  readonly formatSubscriptionSku = formatSubscriptionSku;
  readonly formatBillingChargeStatus = formatBillingChargeStatus;
  readonly formatChargeAmount = formatChargeAmount;
  readonly chargeStatusTone = chargeStatusTone;

  readonly searchQuery = signal("");
  readonly chargeSortColumnId = signal<string | null>("when");
  readonly chargeSortDirection = signal<AdeniDataTableSortDirection>("desc");

  readonly chargeColumns = computed<AdeniDataTableColumn<TenantSubscriptionCharge>[]>(() => [
    {
      id: "when",
      header: this.tr("businessApp.plan.date"),
      cell: (row) => row.occurredAt,
      sortValue: (row) => row.occurredAt,
    },
    {
      id: "amount",
      header: this.tr("businessApp.quotes.amount"),
      align: "right",
      cell: (row) => formatChargeAmount(row),
      sortValue: (row) => row.amountMinor ?? 0,
    },
    {
      id: "status",
      header: this.tr("common.status"),
      cell: () => "",
      sortValue: (row) => row.status,
    },
    {
      id: "plan",
      header: this.tr("admin.businesses.plan"),
      cell: (row) =>
        row.subscriptionSku ? formatSubscriptionSku(row.subscriptionSku) : "—",
      sortValue: (row) => row.subscriptionSku ?? "",
    },
    {
      id: "reference",
      header: this.tr("admin.trustBadges.reference"),
      cell: (row) => row.providerReference ?? "—",
      sortValue: (row) => row.providerReference ?? "",
    },
  ]);

  readonly trackChargeRow = (row: TenantSubscriptionCharge): string => row.id;

  readonly filteredCharges = computed(() => {
    const q = this.searchQuery();
    return this.facade.charges().filter((row) => chargeMatchesSearch(row, q));
  });

  readonly isEmptyCatalog = computed(
    () => !this.facade.loading() && this.facade.charges().length === 0,
  );

  readonly isEmptySearch = computed(
    () =>
      this.facade.charges().length > 0 &&
      this.filteredCharges().length === 0 &&
      this.searchQuery().trim().length > 0,
  );
}
