import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { map } from "rxjs";
import type {
  TenantCustomerDetail,
  TenantCustomerRecentBooking,
  TenantCustomerRecentQueueVisit,
  TenantBookingListTab,
} from "@adeni/shared";
import { formatQueueTicketStatus, formatSlotTime } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniIconBtnComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniTablePersonCellComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniPillTone,
} from "@adeni/ui";
import { AdeniApiError } from "@adeni/api-client";
import type { PortalTab } from "../../core/portal-nav";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalTabsComponent } from "../../shared/portal-tabs.component";

const CUSTOMER_DETAIL_TABS = new Set(["overview", "bookings", "queue"]);

function normalizeCustomerDetailTab(raw: string | null): string {
  return raw && CUSTOMER_DETAIL_TABS.has(raw) ? raw : "overview";
}

const PENDING_STATUS = 0;
const CONFIRMED_STATUS = 1;
const CHECKED_IN_STATUS = 4;
const COMPLETED_STATUS = 5;
const PAID_STATUS = 6;

@Component({
  selector: "app-customer-detail",
  standalone: true,
  imports: [
    PortalPageComponent,
    PortalTabsComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniTablePersonCellComponent,
    AdeniPillComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniListToolbarComponent,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    RouterLink,
  ],
  templateUrl: "./customer-detail.component.html",
  styleUrl: "./customer-detail.component.scss",
})
export class CustomerDetailComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly route = inject(ActivatedRoute);

  readonly tab = toSignal(
    this.route.queryParamMap.pipe(map((params) => normalizeCustomerDetailTab(params.get("tab")))),
    {
      initialValue: normalizeCustomerDetailTab(this.route.snapshot.queryParamMap.get("tab")),
    },
  );

  readonly tabs = computed((): PortalTab[] => {
    const c = this.customer();
    const bookingCount = c?.recentBookings.length ?? 0;
    const queueCount = c?.recentQueueVisits.length ?? 0;
    return [
      { id: "overview", label: "Overview" },
      {
        id: "bookings",
        label: bookingCount > 0 ? `Bookings (${bookingCount})` : "Bookings",
      },
      {
        id: "queue",
        label: queueCount > 0 ? `Queue (${queueCount})` : "Queue",
      },
    ];
  });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly customer = signal<TenantCustomerDetail | null>(null);

  readonly formatSlotTime = formatSlotTime;

  readonly bookingSearchQuery = signal("");
  readonly queueSearchQuery = signal("");
  readonly bookingSortColumn = signal<string | null>("when");
  readonly bookingSortDirection = signal<AdeniDataTableSortDirection>("desc");
  readonly queueSortColumn = signal<string | null>("when");
  readonly queueSortDirection = signal<AdeniDataTableSortDirection>("desc");

  readonly filteredRecentBookings = computed(() => {
    const rows = this.customer()?.recentBookings ?? [];
    const term = this.bookingSearchQuery().trim().toLowerCase();
    if (!term) {
      return rows;
    }
    return rows.filter((row) => this.bookingRowMatchesSearch(row, term));
  });

  readonly filteredRecentQueueVisits = computed(() => {
    const rows = this.customer()?.recentQueueVisits ?? [];
    const term = this.queueSearchQuery().trim().toLowerCase();
    if (!term) {
      return rows;
    }
    return rows.filter((row) => this.queueRowMatchesSearch(row, term));
  });

  readonly bookingColumns: AdeniDataTableColumn<TenantCustomerRecentBooking>[] = [
    {
      id: "when",
      header: "When",
      cell: (row) => formatSlotTime(row.startAt),
      sortable: true,
      sortValue: (row) => row.startAt,
    },
    {
      id: "service",
      header: "Service",
      cell: (row) => row.serviceName,
      sortable: true,
      sortValue: (row) => row.serviceName.toLowerCase(),
    },
    {
      id: "status",
      header: "Status",
      cell: () => "",
      sortable: true,
      sortValue: (row) => row.status,
    },
  ];

  readonly queueColumns: AdeniDataTableColumn<TenantCustomerRecentQueueVisit>[] = [
    {
      id: "when",
      header: "Joined",
      cell: (row) => formatSlotTime(row.joinedAt),
      sortable: true,
      sortValue: (row) => row.joinedAt,
    },
    {
      id: "service",
      header: "Service",
      cell: (row) => row.serviceName ?? "Walk-in",
      sortable: true,
      sortValue: (row) => (row.serviceName ?? "Walk-in").toLowerCase(),
    },
    {
      id: "status",
      header: "Status",
      cell: () => "",
      sortable: true,
      sortValue: (row) => row.status,
    },
  ];

  readonly trackBookingRow = (row: TenantCustomerRecentBooking): string => row.id;
  readonly trackQueueRow = (row: TenantCustomerRecentQueueVisit): string => row.id;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    const id = this.route.snapshot.paramMap.get("id");
    if (!id) {
      this.error.set("Customer not found.");
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      const detail = await this.api.withAuthorizedClient((client) =>
        client.getTenantCustomer(id),
      );
      this.customer.set(detail);
    } catch (err) {
      if (err instanceof AdeniApiError && err.statusCode === 404) {
        this.error.set(null);
      } else {
        this.error.set("Could not load this customer.");
      }
      this.customer.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  customerInitials(displayName: string): string {
    const parts = displayName.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0]!.charAt(0) + parts[1]!.charAt(0)).toUpperCase();
    }
    const word = parts[0] ?? "?";
    return word.slice(0, 2).toUpperCase();
  }

  accountLabel(c: TenantCustomerDetail): string {
    return c.platformCustomerId ? "Discover account" : "Walk-in / queue";
  }

  accountPillTone(c: TenantCustomerDetail): AdeniPillTone {
    return c.platformCustomerId ? "info" : "neutral";
  }

  bookingStatusLabel(status: number): string {
    switch (status) {
      case PENDING_STATUS:
        return "Awaiting approval";
      case CONFIRMED_STATUS:
        return "Scheduled";
      case CHECKED_IN_STATUS:
        return "On the floor";
      case COMPLETED_STATUS:
        return "Service complete";
      case PAID_STATUS:
        return "Paid";
      case 2:
        return "Declined";
      case 3:
        return "Cancelled";
      default:
        return "Unknown";
    }
  }

  bookingPillTone(status: number): AdeniPillTone {
    switch (status) {
      case PENDING_STATUS:
        return "warning";
      case CONFIRMED_STATUS:
        return "info";
      case CHECKED_IN_STATUS:
        return "brand";
      case COMPLETED_STATUS:
      case PAID_STATUS:
        return "success";
      case 2:
      case 3:
        return "danger";
      default:
        return "neutral";
    }
  }

  queueStatusLabel(status: number): string {
    return formatQueueTicketStatus(status);
  }

  queuePillTone(status: number): AdeniPillTone {
    switch (status) {
      case 0:
        return "warning";
      case 1:
      case 2:
        return "brand";
      case 3:
        return "success";
      case 4:
      case 5:
        return "danger";
      default:
        return "neutral";
    }
  }

  private bookingRowMatchesSearch(row: TenantCustomerRecentBooking, term: string): boolean {
    const haystack = [
      row.serviceName,
      row.staffDisplayName ?? "",
      this.bookingStatusLabel(row.status),
      formatSlotTime(row.startAt),
    ]
      .join(" ")
      .toLowerCase();
    return haystack.includes(term);
  }

  private queueRowMatchesSearch(row: TenantCustomerRecentQueueVisit, term: string): boolean {
    const serviceLabel = row.serviceName ?? "Walk-in";
    const haystack = [
      serviceLabel,
      row.staffDisplayName ?? "",
      this.queueStatusLabel(row.status),
      formatSlotTime(row.joinedAt),
    ]
      .join(" ")
      .toLowerCase();
    return haystack.includes(term);
  }

  bookingQueryParams(row: TenantCustomerRecentBooking): { tab: TenantBookingListTab; focus: string } {
    const startMs = Date.parse(row.startAt);
    const isPast =
      statusIsTerminal(row.status) ||
      (!Number.isNaN(startMs) && startMs < Date.now() - 60 * 60 * 1000);
    return {
      tab: isPast ? "past" : row.status === PENDING_STATUS ? "pending" : "upcoming",
      focus: row.id,
    };
  }

  bookingOpenRouterLink(row: TenantCustomerRecentBooking): string {
    const { tab, focus } = this.bookingQueryParams(row);
    const qs = new URLSearchParams({ tab, focus });
    return `/bookings?${qs.toString()}`;
  }
}

function statusIsTerminal(status: number): boolean {
  return status === COMPLETED_STATUS || status === PAID_STATUS || status === 2 || status === 3;
}
