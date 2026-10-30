import {
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { RouterLink } from "@angular/router";
import {
  formatBookingStatusLabel,
  formatSlotTime,
  type CustomerBookingResponse,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniLocaleService,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniTabsComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  type AdeniPillTone,
  type AdeniTab,
} from "@adeni/ui";
import { CustomerApiService } from "../../../core/services/customer-api.service";
import {
  addDays,
  addMonths,
  bookingCountByDay,
  bookingsOnDay,
  dayKey,
  isSameDay,
  isSameMonth,
  monthGridDays,
  startOfWeek,
  weekGridDays,
} from "./customer-calendar.helpers";

type CalendarView = "month" | "week";

/**
 * `/app/calendar` — consumer appointment calendar from `getMyBookings` (C2).
 * No tenant/external calendar APIs.
 */
@Component({
  selector: "app-customer-calendar",
  standalone: true,
  imports: [
    RouterLink,
    PortalPageComponent,
    AdeniTabsComponent,
    AdeniPillComponent,
    AdeniPortalEmptyStateComponent,
    AdeniCarbonIconComponent,
  ],
  templateUrl: "./customer-calendar.component.html",
  styleUrl: "./customer-calendar.component.scss",
})
export class CustomerCalendarComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly api = inject(CustomerApiService);
  private readonly locale = inject(AdeniLocaleService).locale;
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly abort = new AbortController();

  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly bookings = signal<CustomerBookingResponse[]>([]);
  readonly view = signal<CalendarView>("month");
  readonly anchor = signal(startOfWeek(new Date()));
  readonly selected = signal(new Date());

  readonly tabs = computed<AdeniTab[]>(() => [
    { id: "month", label: this.tr("customer.app.calendar.month") },
    { id: "week", label: this.tr("customer.app.calendar.week") },
  ]);

  readonly gridDays = computed(() =>
    this.view() === "week"
      ? weekGridDays(this.anchor(), 0)
      : monthGridDays(this.anchor(), 0),
  );

  readonly counts = computed(() => bookingCountByDay(this.bookings()));

  readonly selectedKey = computed(() => dayKey(this.selected()));

  readonly selectedBookings = computed(() =>
    bookingsOnDay(this.bookings(), this.selectedKey()),
  );

  readonly periodLabel = computed(() => {
    const locale = this.locale();
    const anchor = this.anchor();
    if (this.view() === "week") {
      const days = weekGridDays(anchor, 0);
      const start = days[0]!;
      const end = days[6]!;
      const fmt = new Intl.DateTimeFormat(locale, { month: "short", day: "numeric" });
      return `${fmt.format(start)} – ${fmt.format(end)}`;
    }
    return new Intl.DateTimeFormat(locale, { month: "long", year: "numeric" }).format(anchor);
  });

  readonly weekdayLabels = computed(() => {
    const locale = this.locale();
    const start = startOfWeek(new Date(2026, 5, 14), 0);
    return Array.from({ length: 7 }, (_, i) =>
      new Intl.DateTimeFormat(locale, { weekday: "short" }).format(addDays(start, i)),
    );
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.abort.abort());
  }

  ngOnInit(): void {
    if (this.browser) {
      void this.load();
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.failed.set(false);
    try {
      const rows = await this.api.withAuthorizedClient((c) => c.getMyBookings());
      if (this.abort.signal.aborted) {
        return;
      }
      this.bookings.set(rows);
    } catch {
      if (!this.abort.signal.aborted) {
        this.failed.set(true);
        this.bookings.set([]);
      }
    } finally {
      if (!this.abort.signal.aborted) {
        this.loading.set(false);
      }
    }
  }

  setView(id: string): void {
    this.view.set(id === "week" ? "week" : "month");
  }

  prev(): void {
    const anchor = this.anchor();
    this.anchor.set(this.view() === "week" ? addDays(anchor, -7) : addMonths(anchor, -1));
  }

  next(): void {
    const anchor = this.anchor();
    this.anchor.set(this.view() === "week" ? addDays(anchor, 7) : addMonths(anchor, 1));
  }

  goToday(): void {
    const today = new Date();
    this.anchor.set(today);
    this.selected.set(today);
  }

  selectDay(day: Date): void {
    this.selected.set(day);
    if (this.view() === "month" && !isSameMonth(day, this.anchor())) {
      this.anchor.set(day);
    }
  }

  inMonth(day: Date): boolean {
    return isSameMonth(day, this.anchor());
  }

  isSelected(day: Date): boolean {
    return isSameDay(day, this.selected());
  }

  isToday(day: Date): boolean {
    return isSameDay(day, new Date());
  }

  countFor(day: Date): number {
    return this.counts().get(dayKey(day)) ?? 0;
  }

  dayNumber(day: Date): string {
    return String(day.getDate());
  }

  when(iso: string): string {
    return formatSlotTime(iso, this.locale());
  }

  statusLabel(status: number): string {
    return formatBookingStatusLabel(this.locale(), status);
  }

  statusTone(status: number): AdeniPillTone {
    return status === 1 ? "success" : status === 0 ? "warning" : "neutral";
  }

  dayKeyTrack(day: Date): string {
    return dayKey(day);
  }

  dayAria(day: Date): string {
    const label = new Intl.DateTimeFormat(this.locale(), {
      weekday: "long",
      month: "long",
      day: "numeric",
    }).format(day);
    const count = this.countFor(day);
    if (count === 0) {
      return label;
    }
    return this.tr("customer.app.calendar.dayWithCount", { date: label, count });
  }

  dotSlots(count: number): number[] {
    return Array.from({ length: Math.min(count, 3) }, (_, i) => i);
  }
}
