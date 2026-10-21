import { Component, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
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
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniTablePersonCellComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniPillTone,
} from "@adeni/ui";
import { AdeniApiError } from "@adeni/api-client";
import { BusinessApiService } from "../../core/services/business-api.service";

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
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniTablePersonCellComponent,
    AdeniPillComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    RouterLink,
  ],
  templateUrl: "./customer-detail.component.html",
  styleUrl: "./customer-detail.component.scss",
})
export class CustomerDetailComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly customer = signal<TenantCustomerDetail | null>(null);

  readonly formatSlotTime = formatSlotTime;

  readonly bookingColumns: AdeniDataTableColumn<TenantCustomerRecentBooking>[] = [
    {
      id: "when",
      header: "When",
      cell: (row) => formatSlotTime(row.startAt),
      sortValue: (row) => row.startAt,
    },
    {
      id: "service",
      header: "Service",
      cell: (row) => row.serviceName,
      sortValue: (row) => row.serviceName.toLowerCase(),
    },
    {
      id: "status",
      header: "Status",
      cell: () => "",
      sortValue: (row) => row.status,
    },
  ];

  readonly queueColumns: AdeniDataTableColumn<TenantCustomerRecentQueueVisit>[] = [
    {
      id: "when",
      header: "Joined",
      cell: (row) => formatSlotTime(row.joinedAt),
      sortValue: (row) => row.joinedAt,
    },
    {
      id: "service",
      header: "Service",
      cell: (row) => row.serviceName ?? "Walk-in",
      sortValue: (row) => (row.serviceName ?? "Walk-in").toLowerCase(),
    },
    {
      id: "status",
      header: "Status",
      cell: () => "",
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
}

function statusIsTerminal(status: number): boolean {
  return status === COMPLETED_STATUS || status === PAID_STATUS || status === 2 || status === 3;
}
