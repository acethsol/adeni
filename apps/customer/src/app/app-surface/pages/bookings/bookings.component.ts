import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import type { CustomerBookingResponse } from "@adeni/shared";
import {
  DEFAULT_PAGE_SIZE,
  formatBookingStatusLabel,
  formatSlotTime,
  LIST_PAGE_SIZE_OPTIONS,
  sliceListPage,
  t,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniFeedbackService,
  AdeniIconBtnComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniRowActionBarComponent,
  AdeniLocaleService,
  AdeniPortalEmptyStateComponent,
  AdeniPillComponent,
  AdeniTabsComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  readPersistedListViewMode,
  type AdeniCarbonIconName,
  type AdeniDataTableColumn,
  type AdeniListViewMode,
  type AdeniPillTone,
} from "@adeni/ui";
import { CustomerApiService } from "../../../core/services/customer-api.service";
import {
  ADENI_DISCOVER_CONFIG,
  isDiscoverCustomerDevMode,
} from "../../../core/adeni-config";

type BookingsTab = "upcoming" | "past";

@Component({
  selector: "app-bookings",
  standalone: true,
  imports: [
    RouterLink,
    PortalPageComponent,
    AdeniTabsComponent,
    AdeniPortalEmptyStateComponent,
    AdeniPillComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniListPaginationComponent,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    AdeniCarbonIconComponent,
  ],
  templateUrl: "./bookings.component.html",
  styleUrl: "./bookings.component.scss",
})
export class BookingsComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly api = inject(CustomerApiService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly localeService = inject(AdeniLocaleService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly bookings = signal<CustomerBookingResponse[]>([]);
  readonly cancelId = signal<string | null>(null);
  readonly tab = signal<BookingsTab>("upcoming");
  readonly viewMode = signal<AdeniListViewMode>(
    readPersistedListViewMode("customerApp.bookings", "table"),
  );
  readonly bookingSearch = signal("");
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;

  readonly locale = this.localeService.locale;
  readonly devMode = isDiscoverCustomerDevMode(this.config);

  readonly upcoming = computed(() => {
    const now = Date.now();
    return this.bookings()
      .filter((b) => new Date(b.startAt).getTime() >= now && b.status !== 2 && b.status !== 3)
      .sort((a, b) => +new Date(a.startAt) - +new Date(b.startAt));
  });

  readonly past = computed(() => {
    const now = Date.now();
    return this.bookings()
      .filter((b) => new Date(b.startAt).getTime() < now || b.status === 2 || b.status === 3)
      .sort((a, b) => +new Date(b.startAt) - +new Date(a.startAt));
  });

  readonly visible = computed(() =>
    this.tab() === "upcoming" ? this.upcoming() : this.past(),
  );

  readonly filtered = computed(() => {
    const query = this.bookingSearch().trim().toLowerCase();
    const rows = this.visible();
    if (!query) {
      return rows;
    }
    return rows.filter(
      (row) =>
        row.serviceName.toLowerCase().includes(query)
        || row.businessName.toLowerCase().includes(query),
    );
  });

  readonly filteredCount = computed(() => this.filtered().length);

  readonly paginated = computed(() =>
    sliceListPage(this.filtered(), this.page(), this.pageSize()),
  );

  readonly tableColumns = computed((): AdeniDataTableColumn<CustomerBookingResponse>[] => [
    {
      id: "when",
      header: this.label("common.start"),
      cell: (row) => this.formatWhen(row.startAt),
      sortValue: (row) => row.startAt,
    },
    {
      id: "service",
      header: this.label("common.service"),
      cell: (row) => row.serviceName,
      sortValue: (row) => row.serviceName.toLowerCase(),
    },
    {
      id: "business",
      header: this.label("common.business"),
      cell: (row) => row.businessName,
      sortValue: (row) => row.businessName.toLowerCase(),
    },
    {
      id: "status",
      header: this.label("common.status"),
      cell: () => "",
      sortValue: (row) => row.status,
    },
  ]);

  readonly trackRow = (row: CustomerBookingResponse) => row.id;

  readonly tabs = computed(() => [
    {
      id: "upcoming",
      label: this.label("bookings.upcomingCount", { count: this.upcoming().length }),
    },
    {
      id: "past",
      label: this.label("bookings.pastCount", { count: this.past().length }),
    },
  ]);

  label(key: string, vars?: Record<string, string | number>): string {
    return t(this.locale(), key, vars);
  }

  statusLabel(status: number): string {
    return formatBookingStatusLabel(this.locale(), status);
  }

  onTabChange(id: string): void {
    if (id === "upcoming" || id === "past") {
      this.tab.set(id);
      this.page.set(1);
    }
  }

  onSearchChange(query: string): void {
    this.bookingSearch.set(query);
    this.page.set(1);
  }

  goToPage(next: number): void {
    this.page.set(next);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

  pillTone(status: number): AdeniPillTone {
    switch (status) {
      case 0:
        return "warning";
      case 1:
        return "success";
      case 2:
      case 3:
        return "neutral";
      default:
        return "neutral";
    }
  }

  bookingStatusView(status: number): {
    label: string;
    icon: AdeniCarbonIconName;
    tone: "pending" | "confirmed" | "closed";
  } {
    switch (status) {
      case 0:
        return {
          label: this.statusLabel(status),
          icon: "time",
          tone: "pending",
        };
      case 1:
        return {
          label: this.statusLabel(status),
          icon: "calendar",
          tone: "confirmed",
        };
      case 2:
        return {
          label: this.statusLabel(status),
          icon: "misuse",
          tone: "closed",
        };
      case 3:
        return {
          label: this.statusLabel(status),
          icon: "close",
          tone: "closed",
        };
      default:
        return {
          label: this.statusLabel(status),
          icon: "time",
          tone: "closed",
        };
    }
  }

  formatWhen(iso: string): string {
    return formatSlotTime(iso, this.locale());
  }

  namedGuests(booking: CustomerBookingResponse): string[] {
    return (booking.guests ?? [])
      .map((g) => g.displayName?.trim())
      .filter((name): name is string => !!name);
  }

  dayPart(iso: string): { day: string; month: string; weekday: string } {
    const date = new Date(iso);
    const locale = this.locale();
    return {
      day: new Intl.DateTimeFormat(locale, { day: "numeric" }).format(date),
      month: new Intl.DateTimeFormat(locale, { month: "short" }).format(date),
      weekday: new Intl.DateTimeFormat(locale, { weekday: "short" }).format(date),
    };
  }

  formatTimeOnly(iso: string): string {
    return new Intl.DateTimeFormat(this.locale(), {
      hour: "numeric",
      minute: "2-digit",
    }).format(new Date(iso));
  }

  canCancel(booking: CustomerBookingResponse): boolean {
    return (
      (booking.status === 0 || booking.status === 1) &&
      new Date(booking.startAt).getTime() > Date.now()
    );
  }

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadFailed.set(false);
    try {
      const items = await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.getMyBookings()),
        this.label("bookings.title"),
        this.label("bookings.description"),
      );
      this.bookings.set(items);
      if (this.upcoming().length === 0 && this.past().length > 0) {
        this.tab.set("past");
      }
    } catch {
      this.loadFailed.set(true);
      this.bookings.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  async cancel(booking: CustomerBookingResponse): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: this.label("bookings.cancelBooking") + "?",
      message: `${booking.serviceName} · ${this.formatWhen(booking.startAt)}`,
      confirmLabel: this.label("bookings.cancelBooking"),
      danger: true,
    });
    if (!ok) {
      return;
    }

    this.cancelId.set(booking.id);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.cancelMyBooking(booking.id)),
        this.label("bookings.cancelling"),
        booking.serviceName,
      );
      this.feedback.success(this.label("bookings.cancelSuccess"));
      await this.load();
    } catch {
      this.feedback.error(this.label("bookings.cancelError"));
    } finally {
      this.cancelId.set(null);
    }
  }
}
