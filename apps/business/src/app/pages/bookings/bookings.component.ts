import { IntakeBookingStatusService } from "../intake/intake-booking-status.service";
import { Component, computed, effect, inject, signal } from "@angular/core";
import { takeUntilDestroyed, toSignal } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
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
  totalPages as computeTotalPages,
  type ReportExportFormat,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniFeedbackService,
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
  injectAdeniTranslate,
  injectAdeniApiError,
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
  providers: [IntakeBookingStatusService],
  imports: [RouterLink,
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
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  readonly intakeStatuses = inject(IntakeBookingStatusService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly feedback = inject(AdeniFeedbackService);
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
      pdf: { disabled: true, hint: this.tr("portal.bookings.exportPdfUnavailable") },
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

  readonly bookingTableColumns = computed<AdeniDataTableColumn<BookingResponse>[]>(() => [
    {
      id: "customer",
      header: this.tr("common.customer"),
      cell: (b) => b.customerName?.trim() || this.tr("portal.bookings.guest"),
      sortValue: (b) => (b.customerName?.trim() || this.tr("portal.bookings.guest")).toLowerCase(),
    },
    {
      id: "when",
      header: this.tr("portal.bookings.when"),
      cell: (b) => formatSlotTime(b.startAt),
      sortValue: (b) => b.startAt,
    },
    {
      id: "service",
      header: this.tr("common.service"),
      cell: (b) => b.serviceName,
      sortValue: (b) => b.serviceName.toLowerCase(),
    },
    {
      id: "status",
      header: this.tr("common.status"),
      cell: () => "",
      sortValue: (b) => b.status,
    },
  ]);

  readonly trackBookingRow = (booking: BookingResponse): string => booking.id;
  readonly kioskStatus = signal<KioskStatusResponse | null>(null);
  readonly savingPin = signal(false);
  readonly actionId = signal<string | null>(null);
  kioskPin = "";
  readonly tabs = computed(() => [
    { id: "pending", label: this.tr("portal.staff.pending") },
    { id: "upcoming", label: this.tr("portal.bookings.upcoming") },
    { id: "past", label: this.tr("portal.bookings.past") },
  ]);

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
        return { label: this.tr("portal.bookings.awaitingApproval"), icon: "time", tone: "pending" };
      case CONFIRMED_STATUS:
        return { label: this.tr("portal.bookings.scheduled"), icon: "calendar", tone: "scheduled" };
      case CHECKED_IN_STATUS:
        return { label: this.tr("portal.bookings.onFloor"), icon: "user--filled", tone: "floor" };
      case COMPLETED_STATUS:
        return { label: this.tr("portal.bookings.serviceComplete"), icon: "checkmark", tone: "complete" };
      case PAID_STATUS:
        return { label: this.tr("portal.bookings.paid"), icon: "checkmark--filled", tone: "paid" };
      case 2:
        return { label: this.tr("portal.bookings.declined"), icon: "misuse", tone: "closed" };
      case 3:
        return { label: this.tr("portal.bookings.cancelled"), icon: "close", tone: "closed" };
      default:
        return { label: this.tr("portal.bookings.unknown"), icon: "time", tone: "closed" };
    }
  }

  pageDescription(): string {
    if (this.session.can("portal.bookings.self") && !this.session.can("portal.bookings")) {
      return this.tr("portal.bookings.yourAssignedAppointmentsAcceptCheck");
    }
    return this.tr("portal.bookings.acceptRequestsCheckClientsComplete");
  }

  constructor() {
    effect(() => { void this.intakeStatuses.load(this.filteredBookings().map(x => x.id)); });
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
      void this.activeLocation.runOnBranchSwitch(() => this.load());
    });
  }

  emptyState(): { title: string; description: string; icon: AdeniCarbonIconName } {
    if (this.tab() === "upcoming") {
      return {
        title: this.tr("portal.bookings.nothingScheduledAhead"),
        description: this.tr("portal.bookings.confirmedAppointmentsWillShowHere"),
        icon: "calendar-32",
      };
    }
    if (this.tab() === "past") {
      return {
        title: this.tr("portal.bookings.noPastBookings"),
        description: this.tr("portal.bookings.completedCancelledVisitsWillAppear"),
        icon: "time",
      };
    }
    return {
      title: this.tr("portal.bookings.inboxClear"),
      description: this.tr("portal.bookings.newBookingRequestsNeedResponse"),
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
      await this.feedback.runLoading(async () => {
      await this.api.withAuthorizedClient(async (client) => {
        const profile = await client.getTenantProfile().catch(() => null);
        if (profile) {
          this.activeLocation.syncFromProfile(profile);
        }
        const entitlements = await client.getTenantEntitlements().catch(() => null);
        const mergedForLocation = mergeTenantEntitlements(
          entitlements,
          profile?.entitlements,
          profile?.entitlements,
        );
        const locationQueryId = this.activeLocation.tenantApiLocationId(mergedForLocation);
        const [kiosk, usage] = await Promise.all([
          client.getTenantKioskStatus().catch(() => null),
          client.getTenantSubscriptionUsage(locationQueryId).catch(() => null),
        ]);
        this.planEntitlements.set(
          mergeTenantEntitlements(entitlements, usage?.entitlements, profile?.entitlements),
        );
        const bookingsLocationId = this.activeLocation.tenantApiLocationId(this.planEntitlements());
        const bookingsPage = await client.listTenantBookings({
          tab,
          page: this.page(),
          pageSize: this.pageSize(),
          sort: listSortParamsFromColumn(this.sortColumnId(), this.sortDirection()),
          locationId: bookingsLocationId,
        });
        this.bookings.set(bookingsPage.items);
        this.totalCount.set(bookingsPage.totalCount);
        this.page.set(bookingsPage.page);
        this.kioskStatus.set(kiosk);
      });
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.bookings.couldNotLoadBookingsCheck"));
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
    const locationId = this.activeLocation.tenantApiLocationId(this.planEntitlements());
    const path = this.router.serializeUrl(
      this.router.createUrlTree(["/kiosk"], locationId ? { queryParams: { locationId } } : {}),
    );
    const url = new URL(path, document.baseURI).href;
    const popup = window.open(url, "_blank", "noopener,noreferrer");
    if (!popup) {
      this.error.set(
        this.tr("portal.bookings.couldNotOpenCheckNew"),
      );
      return;
    }
    popup.opener = null;
  }

  async saveKioskPin(): Promise<void> {
    if (!/^\d{4,8}$/.test(this.kioskPin.trim())) {
      this.error.set(this.tr("portal.bookings.exitPinMustBe4"));
      return;
    }
    const confirmed = await this.confirmDialog.confirmKind("save", {
      subject: this.tr("common.confirm.subjects.kioskPin"),
    });
    if (!confirmed) {
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.bookings.couldNotSaveExitPin"));
    } finally {
      this.savingPin.set(false);
    }
  }

  async clearKioskPin(): Promise<void> {
    const confirmed = await this.confirmDialog.confirmKind("remove", {
      subject: this.tr("common.confirm.subjects.kioskPin"),
    });
    if (!confirmed) {
      return;
    }
    this.savingPin.set(true);
    this.error.set(null);
    try {
      const status = await this.api.withAuthorizedClient((client) =>
        client.setTenantKioskPin(null),
      );
      this.kioskStatus.set(status);
      this.kioskPin = "";
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.bookings.couldNotClearExitPin"));
    } finally {
      this.savingPin.set(false);
    }
  }

  async accept(booking: BookingResponse): Promise<void> {
    const ok = await this.confirmDialog.confirmKind("approve", {
      subject: `${booking.serviceName} · ${formatSlotTime(booking.startAt)}`,
    });
    if (!ok) {
      return;
    }
    await this.runStatusAction(booking, "accept");
  }

  async reject(booking: BookingResponse): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: this.tr("portal.bookings.rejectBooking"),
      message: this.tr("portal.bookings.rejectConfirm", { service: booking.serviceName, time: formatSlotTime(booking.startAt) }),
      confirmLabel: this.tr("admin.pending.reject"),
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
    const ok = await this.confirmDialog.confirmKind("approve", {
      title: this.tr("portal.bookings.completeConfirmTitle"),
      message: this.tr("portal.bookings.completeConfirm", { service: booking.serviceName }),
      confirmLabel: this.tr("portal.bookings.completeAction"),
    });
    if (!ok) {
      return;
    }
    await this.runStatusAction(booking, "complete");
  }

  async markPaid(booking: BookingResponse): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: this.tr("portal.bookings.markPaidAtTill"),
      message: this.tr("portal.bookings.markAsPaidOutsideAdeni", { serviceName: booking.serviceName }),
      confirmLabel: this.tr("portal.bookings.markPaid"),
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.bookings.couldNotMarkBookingPaid"));
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
          this.tr("portal.bookings.paymentPageBlockedAllowPop"),
        );
      }
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.bookings.couldNotStartBalanceCollection"));
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
      this.error.set(this.tr("portal.bookings.couldNotBooking", { action: action }));
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
        this.apiErr(err, "portal.bookings.couldNotDownloadExport"),
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

