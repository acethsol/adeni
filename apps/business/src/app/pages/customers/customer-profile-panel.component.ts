import { CustomerSegmentsPanelComponent } from "./customer-segments-panel.component";
import { Component, computed, inject, input, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import { formatPrice, formatSlotTime, type CustomerActivityItem } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniIconBtnComponent,
  AdeniListToolbarComponent,
  AdeniLocaleService,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniStaffButtonComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniPillTone,
} from "@adeni/ui";
import { BusinessSessionService } from "../../core/services/business-session.service";
import { CustomerProfileDataService } from "./customer-profile-data.service";

@Component({
  selector: "app-customer-profile-panel",
  standalone: true,
  imports: [
    RouterLink,
    CustomerSegmentsPanelComponent,
    AdeniCarbonIconComponent,
    AdeniPillComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    AdeniStaffButtonComponent,
  ],
  templateUrl: "./customer-profile-panel.component.html",
  styleUrl: "./customer-profile-panel.component.scss",
})
export class CustomerProfilePanelComponent {
  readonly customerId = input.required<string>();
  readonly locationId = input<string>();
  readonly activityVisible = input(false);
  readonly overviewVisible = input(false);
  readonly data = inject(CustomerProfileDataService);
  readonly session = inject(BusinessSessionService);
  readonly tr = injectAdeniTranslate();
  readonly apiErr = injectAdeniApiError();
  private readonly locale = inject(AdeniLocaleService);
  readonly formatSlotTime = (value: string): string => formatSlotTime(value, this.locale.locale());
  readonly formatPrice = (amount: number, currency: string): string => formatPrice(amount, currency, this.locale.locale());

  readonly activitySearchQuery = signal("");
  readonly activitySortColumn = signal<string | null>("when");
  readonly activitySortDirection = signal<AdeniDataTableSortDirection>("desc");

  readonly activityColumns = computed((): AdeniDataTableColumn<CustomerActivityItem>[] => [
    {
      id: "when",
      header: this.tr("businessApp.bookings.when"),
      cell: (row) => this.formatSlotTime(row.occurredAt),
      sortable: true,
      sortValue: (row) => row.occurredAt,
    },
    {
      id: "event",
      header: this.tr("customerProfile.activityEvent"),
      cell: (row) => this.activityKindLabel(row),
      sortable: true,
      sortValue: (row) => this.activityKindLabel(row).toLowerCase(),
    },
    {
      id: "details",
      header: this.tr("customerProfile.activityDetails"),
      cell: (row) => row.serviceName ?? "—",
      sortable: true,
      sortValue: (row) => (row.serviceName ?? "").toLowerCase(),
    },
    {
      id: "status",
      header: this.tr("common.status"),
      cell: () => "",
      sortable: true,
      sortValue: (row) => row.currentStatus ?? "",
    },
    {
      id: "amount",
      header: this.tr("customerProfile.activityAmount"),
      cell: (row) =>
        row.amount !== null && row.currency
          ? this.formatPrice(row.amount, row.currency)
          : "—",
      sortable: true,
      sortValue: (row) => row.amount ?? -1,
    },
  ]);

  readonly filteredActivityItems = computed(() => {
    const items = this.data.activity()?.items ?? [];
    const term = this.activitySearchQuery().trim().toLowerCase();
    if (!term) {
      return items;
    }
    return items.filter((item) => this.activityRowMatchesSearch(item, term));
  });

  readonly trackActivityRow = (row: CustomerActivityItem): string => row.id;

  refresh(): void { void this.data.load(this.customerId(), this.locationId()); }

  activityKindLabel(item: CustomerActivityItem): string {
    return this.tr(`customerProfile.kinds.${item.kind}`);
  }

  activityRowMatchesSearch(item: CustomerActivityItem, term: string): boolean {
    const haystack = [
      this.activityKindLabel(item),
      item.serviceName ?? "",
      item.currentStatus ?? "",
      item.source,
      item.amount !== null && item.currency ? this.formatPrice(item.amount, item.currency) : "",
      this.formatSlotTime(item.occurredAt),
    ]
      .join(" ")
      .toLowerCase();
    return haystack.includes(term);
  }

  bookingQueryParams(item: CustomerActivityItem): { focus: string; tab: string } {
    return { focus: item.sourceId, tab: "past" };
  }

  activityStatusTone(status: string): AdeniPillTone {
    switch (status) {
      case "Pending":
        return "warning";
      case "Confirmed":
      case "CheckedIn":
      case "Waiting":
      case "Called":
      case "InService":
        return "info";
      case "Completed":
      case "Paid":
      case "Done":
        return "success";
      case "Rejected":
      case "Cancelled":
      case "NoShow":
        return "danger";
      default:
        return "neutral";
    }
  }
}
