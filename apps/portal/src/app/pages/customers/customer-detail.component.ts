import { IntakeBookingStatusService } from "../intake/intake-booking-status.service";
import { Component, computed, effect, DestroyRef, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed, toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { distinctUntilChanged, map } from "rxjs";
import type {
  TenantCustomerDetail,
  TenantCustomerRecentBooking,
  TenantCustomerRecentQueueVisit,
  TenantBookingListTab,
} from "@adeni/shared";
import { formatQueueTicketStatus, formatSlotTime, mergeTenantEntitlements } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
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
  AdeniTabsComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniPillTone,
  type AdeniTab,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { AdeniApiError } from "@adeni/api-client";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
import { Subscription } from "rxjs";
import { CustomerProfilePanelComponent } from "./customer-profile-panel.component";
import { PortalSessionService } from "../../core/services/portal-session.service";

const CUSTOMER_DETAIL_TABS = new Set(["overview", "bookings", "queue", "activity"]);

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
  providers: [IntakeBookingStatusService],
  imports: [
    PortalPageComponent,
    AdeniTabsComponent,
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
    CustomerProfilePanelComponent,
  ],
  templateUrl: "./customer-detail.component.html",
  styleUrl: "./customer-detail.component.scss",
})
export class CustomerDetailComponent implements OnInit, OnDestroy {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly activeLocation = inject(PortalActiveLocationService);
  private readonly destroyRef = inject(DestroyRef);
  readonly session = inject(PortalSessionService);
  readonly intakeStatuses = inject(IntakeBookingStatusService);
  readonly profileLocationId = signal<string | undefined>(undefined);
  private loadVersion = 0;
  private branchSub: Subscription | null = null;

  readonly tab = toSignal(
    this.route.queryParamMap.pipe(map((params) => normalizeCustomerDetailTab(params.get("tab")))),
    {
      initialValue: normalizeCustomerDetailTab(this.route.snapshot.queryParamMap.get("tab")),
    },
  );

  readonly tabs = computed((): AdeniTab[] => {
    const c = this.customer();
    const bookingCount = c?.recentBookings.length ?? 0;
    const queueCount = c?.recentQueueVisits.length ?? 0;
    return [
      { id: "overview", label: this.tr("portal.customers.overview") },
      {
        id: "bookings",
        label: bookingCount > 0 ? this.tr("portal.customers.bookings", { bookingCount: bookingCount }) : this.tr("common.bookings"),
      },
      {
        id: "queue",
        label: queueCount > 0 ? this.tr("portal.customers.queue", { queueCount: queueCount }) : this.tr("common.queue"),
      },
      ...(this.session.can("portal.bookings") ? [{ id: "activity", label: this.tr("customerProfile.activity") }] : []),
    ];
  });

  readonly error = signal<string | null>(null);
  readonly customer = signal<TenantCustomerDetail | null>(null);
  readonly statementDownloading = signal(false);

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

  readonly bookingColumns = computed<AdeniDataTableColumn<TenantCustomerRecentBooking>[]>(() => [
    {
      id: "when",
      header: this.tr("portal.bookings.when"),
      cell: (row) => formatSlotTime(row.startAt),
      sortable: true,
      sortValue: (row) => row.startAt,
    },
    {
      id: "service",
      header: this.tr("common.service"),
      cell: (row) => row.serviceName,
      sortable: true,
      sortValue: (row) => row.serviceName.toLowerCase(),
    },
    {
      id: "status",
      header: this.tr("common.status"),
      cell: () => "",
      sortable: true,
      sortValue: (row) => row.status,
    },
  ]);

  readonly queueColumns = computed<AdeniDataTableColumn<TenantCustomerRecentQueueVisit>[]>(() => [
    {
      id: "when",
      header: this.tr("portal.customers.joined"),
      cell: (row) => formatSlotTime(row.joinedAt),
      sortable: true,
      sortValue: (row) => row.joinedAt,
    },
    {
      id: "service",
      header: this.tr("common.service"),
      cell: (row) => row.serviceName ?? this.tr("common.walk"),
      sortable: true,
      sortValue: (row) => (row.serviceName ?? this.tr("common.walk")).toLowerCase(),
    },
    {
      id: "status",
      header: this.tr("common.status"),
      cell: () => "",
      sortable: true,
      sortValue: (row) => row.status,
    },
  ]);

  readonly trackBookingRow = (row: TenantCustomerRecentBooking): string => row.id;
  readonly trackQueueRow = (row: TenantCustomerRecentQueueVisit): string => row.id;

  constructor() {
    effect(() => { void this.intakeStatuses.load(this.session.can("portal.bookings") ? this.customer()?.recentBookings.map(x => x.id) ?? [] : []); });
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(map(params => params.get("id")), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => { void this.load(); });
    this.branchSub = this.activeLocation.selectionChanged$.subscribe(() => {
      void this.activeLocation.runOnBranchSwitch(() => this.load());
    });
  }

  ngOnDestroy(): void {
    this.loadVersion++;
    this.branchSub?.unsubscribe();
  }

  async load(): Promise<void> {
    const version = ++this.loadVersion;
    const id = this.route.snapshot.paramMap.get("id");
    if (!id) {
      this.error.set(this.tr("portal.customers.customerNotFound2"));
      return;
    }

    this.error.set(null);
    this.customer.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const detail = await this.api.withAuthorizedClient(async (client) => {
          const profile = await client.getTenantProfile().catch(() => null);
          const planEntitlements = await client.getTenantEntitlements().catch(() => null);
          const merged = mergeTenantEntitlements(
            planEntitlements,
            profile?.entitlements,
            profile?.entitlements,
          );
          const locationId = this.activeLocation.tenantApiLocationId(merged);
          return { detail: await client.getTenantCustomer(id, locationId), locationId };
        });
        if (version !== this.loadVersion) return;
        this.profileLocationId.set(detail.locationId);
        this.customer.set(detail.detail);
      });
    } catch (err) {
      if (version !== this.loadVersion) return;
      if (err instanceof AdeniApiError && err.statusCode === 404) {
        this.error.set(null);
      } else {
        this.error.set(this.apiErr(err, "portal.customers.couldNotLoadCustomer"));
      }
      this.customer.set(null);
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
    return c.platformCustomerId ? this.tr("portal.customers.discoverAccount") : this.tr("portal.customers.walkQueue");
  }

  accountPillTone(c: TenantCustomerDetail): AdeniPillTone {
    return c.platformCustomerId ? "info" : "neutral";
  }

  bookingStatusLabel(status: number): string {
    switch (status) {
      case PENDING_STATUS:
        return this.tr("portal.bookings.awaitingApproval");
      case CONFIRMED_STATUS:
        return this.tr("portal.bookings.scheduled");
      case CHECKED_IN_STATUS:
        return this.tr("portal.bookings.onFloor");
      case COMPLETED_STATUS:
        return this.tr("portal.bookings.serviceComplete");
      case PAID_STATUS:
        return this.tr("portal.bookings.paid");
      case 2:
        return this.tr("portal.bookings.declined");
      case 3:
        return this.tr("portal.bookings.cancelled");
      default:
        return this.tr("portal.bookings.unknown");
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
    const serviceLabel = row.serviceName ?? this.tr("common.walk");
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

  async downloadStatement(): Promise<void> {
    const id = this.route.snapshot.paramMap.get("id");
    if (!id) {
      return;
    }
    const to = new Date();
    const from = new Date(to);
    from.setDate(from.getDate() - 90);
    this.statementDownloading.set(true);
    this.error.set(null);
    try {
      const blob = await this.api.withAuthorizedClient((client) =>
        client.downloadTenantCustomerStatementPdf({
          tenantCustomerId: id,
          from: from.toISOString(),
          to: to.toISOString(),
        }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `statement-${id.slice(0, 8)}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.customers.couldNotDownloadCustomerStatement"));
    } finally {
      this.statementDownloading.set(false);
    }
  }
}

function statusIsTerminal(status: number): boolean {
  return status === COMPLETED_STATUS || status === PAID_STATUS || status === 2 || status === 3;
}
