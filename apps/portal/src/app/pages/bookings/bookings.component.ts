import { Component, computed, inject, signal } from "@angular/core";
import { takeUntilDestroyed, toSignal } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import { AdeniApiError } from "@adeni/api-client";
import type {
  BookingResponse,
  KioskStatusResponse,
  TenantBookingListTab,
  TenantEntitlements,
} from "@adeni/shared";
import {
  DEFAULT_PAGE_SIZE,
  formatSlotTime,
  LIST_PAGE_SIZE_OPTIONS,
  listSortParamsFromColumn,
  pageRangeLabel,
  resolvePaymentCheckoutUrl,
  REPORT_KEYS,
  buildReportExportMenuItems,
  mergeTenantEntitlements,
  tenantReportExportErrorMessage,
  totalPages as computeTotalPages,
  type ReportExportFormat,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniExportMenuComponent,
  AdeniIconBtnComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniTablePersonCellComponent,
  PortalPageComponent,
  AdeniTabsComponent,
  readPersistedListViewMode,
  type AdeniCarbonIconName,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniExportMenuItem,
  type AdeniListViewMode,
  type AdeniPillTone,
} from "@adeni/ui";
import { map } from "rxjs";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
import { PortalSessionService } from "../../core/services/portal-session.service";

const PENDING_STATUS = 0;
const CONFIRMED_STATUS = 1;
const CHECKED_IN_STATUS = 4;
const COMPLETED_STATUS = 5;
const PAID_STATUS = 6;

@Component({
  selector: "app-bookings",
  standalone: true,
  imports: [
    PortalPageComponent,
    AdeniTabsComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniExportMenuComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    AdeniPillComponent,
    AdeniTablePersonCellComponent,
    AdeniListPaginationComponent,
    FormsModule,
  ],
  templateUrl: "./bookings.component.html",
  styleUrl: "./bookings.component.scss",
})
export class BookingsComponent {
  private readonly api = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly session = inject(PortalSessionService);
  private readonly activeLocation = inject(PortalActiveLocationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly tab = toSignal(this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? "pending")), {
    initialValue: this.route.snapshot.queryParamMap.get("tab") ?? "pending",
  });
  readonly focusBookingId = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get("focus"))),
    { initialValue: this.route.snapshot.queryParamMap.get("focus") },
  );

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly bookings = signal<BookingResponse[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly totalPageCount = computed(() => computeTotalPages(this.totalCount(), this.pageSize()));
  readonly pageLabel = computed(() =>
    pageRangeLabel(this.page(), this.pageSize(), this.totalCount()),
  );
  readonly bookingCountTotal = computed(() =>
    this.bookingSearch().trim() ? null : this.totalCount(),
  );
  readonly displayedBookings = computed(() =>
    prioritizeFocus(this.bookings(), this.focusBookingId()),
  );
  readonly bookingSearch = signal("");
  readonly viewMode = signal<AdeniListViewMode>(
    readPersistedListViewMode("portal.bookings", "cards"),
  );

  readonly exporting = signal(false);
  readonly planEntitlements = signal<TenantEntitlements | null>(null);
  readonly exportItems = computed((): AdeniExportMenuItem[] =>
    buildReportExportMenuItems({
      entitlements: this.planEntitlements(),
      xlsx: true,
      pdf: { disabled: true, hint: "Not available for bookings register" },
    }),
  );

  readonly sortColumnId = signal<string | null>(null);
  readonly sortDirection = signal<AdeniDataTableSortDirection>("asc");

  readonly filteredBookings = computed(() => {
    const query = this.bookingSearch().trim().toLowerCase();
    let list = this.displayedBookings();
    if (!query) {
      return list;
    }
    return list.filter((booking) => {
      const haystack = [
        booking.customerName ?? "",
        booking.serviceName,
        booking.customerPhoneMasked ?? "",
        booking.staffDisplayName ?? "",
      ]
        .join(" ")
        .toLowerCase();
      return haystack.includes(query);
    });
  });

  readonly bookingTableColumns: AdeniDataTableColumn<BookingResponse>[] = [
    {
      id: "customer",
      header: "Customer",
      cell: (b) => b.customerName?.trim() || "Guest",
      sortValue: (b) => (b.customerName?.trim() || "Guest").toLowerCase(),
    },
    {
      id: "when",
      header: "When",
      cell: (b) => formatSlotTime(b.startAt),
      sortValue: (b) => b.startAt,
    },
    {
      id: "service",
      header: "Service",
      cell: (b) => b.serviceName,
      sortValue: (b) => b.serviceName.toLowerCase(),
    },
    {
      id: "status",
      header: "Status",
      cell: () => "",
      sortValue: (b) => b.status,
    },
  ];

  readonly trackBookingRow = (booking: BookingResponse): string => booking.id;
  readonly kioskStatus = signal<KioskStatusResponse | null>(null);
  readonly savingPin = signal(false);
  readonly actionId = signal<string | null>(null);
  kioskPin = "";
  readonly tabs = [
    { id: "pending", label: "Pending" },
    { id: "upcoming", label: "Upcoming" },
    { id: "past", label: "Past" },
  ];

  readonly formatSlotTime = formatSlotTime;

  isPinned(bookingId: string): boolean {
    return this.focusBookingId() === bookingId;
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

  bookingStatusView(status: number): {
    label: string;
    icon: AdeniCarbonIconName;
    tone: "pending" | "scheduled" | "floor" | "complete" | "paid" | "closed";
  } {
    switch (status) {
      case PENDING_STATUS:
        return { label: "Awaiting approval", icon: "time", tone: "pending" };
      case CONFIRMED_STATUS:
        return { label: "Scheduled", icon: "calendar", tone: "scheduled" };
      case CHECKED_IN_STATUS:
        return { label: "On the floor", icon: "user--filled", tone: "floor" };
      case COMPLETED_STATUS:
        return { label: "Service complete", icon: "checkmark", tone: "complete" };
      case PAID_STATUS:
        return { label: "Paid", icon: "checkmark--filled", tone: "paid" };
      case 2:
        return { label: "Declined", icon: "misuse", tone: "closed" };
      case 3:
        return { label: "Cancelled", icon: "close", tone: "closed" };
      default:
        return { label: "Unknown", icon: "time", tone: "closed" };
    }
  }

  pageDescription(): string {
    let base: string;
    if (this.session.can("portal.bookings.self") && !this.session.can("portal.bookings")) {
      base = "Your assigned appointments — accept, check in, complete, and close payment.";
    } else {
      base = "Accept requests, check clients in, complete visits, and collect or mark payment.";
    }
    if (!this.activeLocation.showLocationSwitch()) {
      return base;
    }
    return `${base} (Scope: ${this.activeLocation.switchDisplay().title})`;
  }

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const page = Number(params.get("page") ?? "1");
      this.page.set(Number.isFinite(page) && page >= 1 ? page : 1);
      void this.load();
    });
    this.activeLocation.selectionChanged$.pipe(takeUntilDestroyed()).subscribe(() => {
      this.page.set(1);
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { page: null },
        queryParamsHandling: "merge",
      });
      void this.load();
    });
  }

  emptyState(): { title: string; description: string; icon: AdeniCarbonIconName } {
    if (this.tab() === "upcoming") {
      return {
        title: "Nothing scheduled ahead",
        description: "Confirmed appointments will show here as your calendar fills up.",
        icon: "calendar-32",
      };
    }
    if (this.tab() === "past") {
      return {
        title: "No past bookings",
        description: "Completed and cancelled visits will appear here for your records.",
        icon: "time",
      };
    }
    return {
      title: "Inbox is clear",
      description: "New booking requests that need your response will land here first.",
      icon: "calendar-32",
    };
  }

  lineSummary(booking: BookingResponse): string {
    if (!booking.lines?.length) {
      return booking.serviceName;
    }
    return booking.lines.map((line) => line.serviceName).join(" · ");
  }

  onSortColumnChange(columnId: string | null): void {
    this.sortColumnId.set(columnId);
    this.page.set(1);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: null },
      queryParamsHandling: "merge",
    });
    void this.load();
  }

  onSortDirectionChange(direction: AdeniDataTableSortDirection): void {
    this.sortDirection.set(direction);
    this.page.set(1);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: null },
      queryParamsHandling: "merge",
    });
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const tab = (this.tab() ?? "pending") as TenantBookingListTab;
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [kiosk, usage, profile, entitlements] = await Promise.all([
          client.getTenantKioskStatus().catch(() => null),
          client.getTenantSubscriptionUsage().catch(() => null),
          client.getTenantProfile().catch(() => null),
          client.getTenantEntitlements().catch(() => null),
        ]);
        if (profile) {
          this.activeLocation.syncFromProfile(profile);
        }
        this.planEntitlements.set(
          mergeTenantEntitlements(entitlements, usage?.entitlements, profile?.entitlements),
        );
        const locationQueryId = this.activeLocation.tenantApiLocationId(this.planEntitlements());
        const bookingsPage = await client.listTenantBookings({
          tab,
          page: this.page(),
          pageSize: this.pageSize(),
          sort: listSortParamsFromColumn(this.sortColumnId(), this.sortDirection()),
          locationId: locationQueryId,
        });
        this.bookings.set(bookingsPage.items);
        this.totalCount.set(bookingsPage.totalCount);
        this.page.set(bookingsPage.page);
        this.kioskStatus.set(kiosk);
      });
    } catch {
      this.error.set("Could not load bookings. Check API and dev business sub.");
      this.bookings.set([]);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  canNextPage(): boolean {
    return this.page() < this.totalPageCount();
  }

  prevPage(): void {
    if (this.page() <= 1) {
      return;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: this.page() - 1 === 1 ? null : this.page() - 1 },
      queryParamsHandling: "merge",
    });
  }

  nextPage(): void {
    if (!this.canNextPage()) {
      return;
    }
    void this.goToPage(this.page() + 1);
  }

  goToPage(next: number): void {
    const page = Math.min(Math.max(1, next), this.totalPageCount());
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: page === 1 ? null : page },
      queryParamsHandling: "merge",
    });
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: null },
      queryParamsHandling: "merge",
    });
    void this.load();
  }

  showBookingActions(booking: BookingResponse): boolean {
    return (
      (this.tab() === "pending" && booking.status === 0) ||
      booking.status === 1 ||
      booking.status === 4 ||
      booking.status === 5
    );
  }

  openKiosk(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const path = this.router.serializeUrl(this.router.createUrlTree(["/kiosk"]));
    const url = new URL(path, document.baseURI).href;
    const popup = window.open(url, "_blank", "noopener,noreferrer");
    if (!popup) {
      this.error.set(
        "Could not open check-in in a new tab. Allow pop-ups for this site, then try again.",
      );
      return;
    }
    popup.opener = null;
  }

  async saveKioskPin(): Promise<void> {
    if (!/^\d{4,8}$/.test(this.kioskPin.trim())) {
      this.error.set("Exit PIN must be 4–8 digits.");
      return;
    }
    this.savingPin.set(true);
    this.error.set(null);
    try {
      const status = await this.api.withAuthorizedClient((client) =>
        client.setTenantKioskPin(this.kioskPin.trim()),
      );
      this.kioskStatus.set(status);
      this.kioskPin = "";
    } catch {
      this.error.set("Could not save exit PIN.");
    } finally {
      this.savingPin.set(false);
    }
  }

  async clearKioskPin(): Promise<void> {
    this.savingPin.set(true);
    this.error.set(null);
    try {
      const status = await this.api.withAuthorizedClient((client) =>
        client.setTenantKioskPin(null),
      );
      this.kioskStatus.set(status);
      this.kioskPin = "";
    } catch {
      this.error.set("Could not clear exit PIN.");
    } finally {
      this.savingPin.set(false);
    }
  }

  async accept(booking: BookingResponse): Promise<void> {
    await this.runStatusAction(booking, "accept");
  }

  async reject(booking: BookingResponse): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: "Reject booking?",
      message: `Reject ${booking.serviceName} on ${formatSlotTime(booking.startAt)}? The customer will be notified.`,
      confirmLabel: "Reject",
      danger: true,
    });
    if (!ok) {
      return;
    }
    await this.runStatusAction(booking, "reject");
  }

  async checkIn(booking: BookingResponse): Promise<void> {
    await this.runStatusAction(booking, "check-in");
  }

  async complete(booking: BookingResponse): Promise<void> {
    await this.runStatusAction(booking, "complete");
  }

  async markPaid(booking: BookingResponse): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: "Mark paid at till?",
      message: `Mark ${booking.serviceName} as paid outside Adeni (cash/transfer)?`,
      confirmLabel: "Mark paid",
    });
    if (!ok) {
      return;
    }
    this.actionId.set(booking.id);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) =>
        client.markTenantBookingPaid(booking.id, {}),
      );
      void updated;
      await this.load();
    } catch {
      this.error.set("Could not mark this booking paid.");
    } finally {
      this.actionId.set(null);
    }
  }

  async collectBalance(booking: BookingResponse): Promise<void> {
    this.actionId.set(booking.id);
    this.error.set(null);
    try {
      const result = await this.api.withAuthorizedClient((client) =>
        client.collectTenantBookingBalance(booking.id),
      );
      await this.load();
      const checkoutUrl = resolvePaymentCheckoutUrl(result.checkoutUrl, this.config.discoverWebUrl, {
        amount: result.amount,
        currency: result.currency,
      });
      const popup = window.open(checkoutUrl, "_blank", "noopener,noreferrer");
      if (!popup) {
        this.error.set(
          "Payment page blocked. Allow pop-ups for this site, or copy the link from Payments after creating a link.",
        );
      }
    } catch {
      this.error.set("Could not start balance collection.");
    } finally {
      this.actionId.set(null);
    }
  }

  private async runStatusAction(
    booking: BookingResponse,
    action: "accept" | "reject" | "check-in" | "complete",
  ): Promise<void> {
    this.actionId.set(booking.id);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) => {
        switch (action) {
          case "accept":
            return client.acceptTenantBooking(booking.id);
          case "reject":
            return client.rejectTenantBooking(booking.id);
          case "check-in":
            return client.checkInTenantBooking(booking.id);
          case "complete":
            return client.completeTenantBooking(booking.id);
        }
      });
      void updated;
      await this.load();
    } catch {
      this.error.set(`Could not ${action} this booking.`);
    } finally {
      this.actionId.set(null);
    }
  }

  async downloadRegisterExport(format: ReportExportFormat): Promise<void> {
    if (format === "pdf") {
      return;
    }

    this.exporting.set(true);
    this.error.set(null);
    try {
      const to = new Date();
      const from = new Date(to);
      from.setDate(from.getDate() - 90);
      const blob = await this.api.withAuthorizedClient((client) =>
        client.downloadTenantReportExport({
          reportKey: REPORT_KEYS.bookingsRegister,
          from: from.toISOString(),
          to: to.toISOString(),
          format,
          locationId: this.activeLocation.reportExportLocationId(this.planEntitlements()),
        }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `bookings-register.${format === "xlsx" ? "xlsx" : "csv"}`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      this.error.set(
        tenantReportExportErrorMessage("en", {
          code: err instanceof AdeniApiError ? err.code : undefined,
          message: err instanceof AdeniApiError ? err.message : undefined,
          fallback: "Could not download the bookings export.",
        }),
      );
    } finally {
      this.exporting.set(false);
    }
  }
}

function prioritizeFocus<T extends { id: string }>(items: T[], focusId: string | null | undefined): T[] {
  if (!focusId) {
    return items;
  }
  const index = items.findIndex((item) => item.id === focusId);
  if (index <= 0) {
    return items;
  }
  const focused = items[index];
  return [focused, ...items.slice(0, index), ...items.slice(index + 1)];
}

