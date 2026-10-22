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
  readonly facade = inject(BusinessDetailFacade);

  readonly formatSubscriptionSku = formatSubscriptionSku;
  readonly formatBillingChargeStatus = formatBillingChargeStatus;
  readonly formatChargeAmount = formatChargeAmount;
  readonly chargeStatusTone = chargeStatusTone;

  readonly searchQuery = signal("");
  readonly chargeSortColumnId = signal<string | null>("when");
  readonly chargeSortDirection = signal<AdeniDataTableSortDirection>("desc");

  readonly chargeColumns: AdeniDataTableColumn<TenantSubscriptionCharge>[] = [
    {
      id: "when",
      header: "Date",
      cell: (row) => row.occurredAt,
      sortValue: (row) => row.occurredAt,
    },
    {
      id: "amount",
      header: "Amount",
      align: "right",
      cell: (row) => formatChargeAmount(row),
      sortValue: (row) => row.amountMinor ?? 0,
    },
    {
      id: "status",
      header: "Status",
      cell: () => "",
      sortValue: (row) => row.status,
    },
    {
      id: "plan",
      header: "Plan",
      cell: (row) =>
        row.subscriptionSku ? formatSubscriptionSku(row.subscriptionSku) : "—",
      sortValue: (row) => row.subscriptionSku ?? "",
    },
    {
      id: "reference",
      header: "Reference",
      cell: (row) => row.providerReference ?? "—",
      sortValue: (row) => row.providerReference ?? "",
    },
  ];

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
