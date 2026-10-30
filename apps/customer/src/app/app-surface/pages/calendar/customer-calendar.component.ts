import {
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
  viewChild,
} from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { RouterLink } from "@angular/router";
import {
  formatBookingStatusLabel,
  type CustomerBookingResponse,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniFeedbackService,
  AdeniListToolbarComponent,
  AdeniLocaleService,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  CALENDAR_DAYS_ORDER,
  PortalPageComponent,
  addDays,
  addMonths,
  clipToDay,
  endOfMonth,
  formatClock,
  injectAdeniTranslate,
  overlapsDay,
  readPersistedListViewMode,
  sameDay,
  startOfDay,
  startOfMonth,
  startOfWeek,
  type AdeniDataTableColumn,
  type AdeniListViewMode,
  type AdeniPillTone,
} from "@adeni/ui";
import { CustomerApiService } from "../../../core/services/customer-api.service";
import {
  bookingRangeHoursForDays,
  bookingsOnDay,
  dayKey,
  monthGridDays,
  weekGridDays,
} from "./customer-calendar.helpers";

const PX_PER_HOUR = 64;
const DEFAULT_START_HOUR = 8;
const DEFAULT_END_HOUR = 20;
const DAYS_ORDER = CALENDAR_DAYS_ORDER;

type CalendarView = "month" | "week" | "day";

type DayColumn = {
  date: Date;
  key: string;
  dayOfWeek: number;
  label: string;
  shortLabel: string;
  dateNum: number;
  isToday: boolean;
  isPast: boolean;
  bookingCount: number;
  inMonth: boolean;
};

type PlacedBlock = {
  id: string;
  dayKey: string;
  top: number;
  height: number;
  startLabel: string;
  endLabel: string;
  title: string;
  subtitle: string | null;
  status: number;
  statusLabel: string;
  booking: CustomerBookingResponse;
};

type MonthCell = DayColumn & {
  items: PlacedBlock[];
};

@Component({
  selector: "app-customer-calendar",
  host: { class: "customer-calendar-page" },
  standalone: true,
  imports: [
    RouterLink,
    PortalPageComponent,
    AdeniPillComponent,
    AdeniPortalEmptyStateComponent,
    AdeniCarbonIconComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
  ],
  templateUrl: "./customer-calendar.component.html",
  styleUrl: "./customer-calendar.component.scss",
})
export class CustomerCalendarComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly api = inject(CustomerApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly localeService = inject(AdeniLocaleService);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly abort = new AbortController();

  private readonly detailRail = viewChild<ElementRef<HTMLElement>>("detailRail");

  /** Exposed for template hour-line math (matches business staff calendar). */
  protected readonly pxPerHour = PX_PER_HOUR;

  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly bookings = signal<CustomerBookingResponse[]>([]);
  readonly listViewMode = signal<AdeniListViewMode>(
    readPersistedListViewMode("customerApp.calendar", "cards"),
  );
  readonly calendarSearch = signal("");
  readonly sidebarMonth = signal(startOfMonth(new Date()));
  readonly weekStart = signal(startOfWeek(new Date()));
  readonly monthStart = signal(startOfMonth(new Date()));
  readonly view = signal<CalendarView>("week");
  readonly focusDay = signal(startOfDay(new Date()));
  readonly selectedId = signal<string | null>(null);
  readonly nowTick = signal(Date.now());

  private tickTimer: ReturnType<typeof setInterval> | null = null;

  readonly locale = this.localeService.locale;

  readonly focusDayKey = computed(() => dayKey(this.focusDay()));

  readonly appointmentSummary = computed(() => {
    const count = this.stats().bookings;
    const label =
      count === 1 ? this.tr("common.appointment") : this.tr("businessApp.dashboard.appointments");
    return `${count} ${label} · ${this.periodLabel()}`;
  });

  readonly displayBlocks = computed((): PlacedBlock[] => {
    let list = this.blocks().filter(
      (b) =>
        this.view() !== "month" || this.days().find((d) => d.key === b.dayKey)?.inMonth,
    );

    const query = this.calendarSearch().trim().toLowerCase();
    if (query) {
      list = list.filter((b) => {
        const haystack = [
          b.title,
          b.subtitle ?? "",
          b.statusLabel,
          b.dayKey,
          this.formatAgendaDay(b.dayKey),
        ]
          .join(" ")
          .toLowerCase();
        return haystack.includes(query);
      });
    }

    return list.sort((a, b) => {
      const dayCmp = a.dayKey.localeCompare(b.dayKey);
      if (dayCmp !== 0) {
        return dayCmp;
      }
      return a.top - b.top;
    });
  });

  readonly agendaTableColumns = computed<AdeniDataTableColumn<PlacedBlock>[]>(() => [
    {
      id: "when",
      header: this.tr("businessApp.bookings.when"),
      cell: (b) => `${this.formatAgendaDay(b.dayKey)} · ${b.startLabel} – ${b.endLabel}`,
      sortValue: (b) => `${b.dayKey}${b.top.toString().padStart(5, "0")}`,
    },
    {
      id: "service",
      header: this.tr("common.service"),
      cell: (b) => b.title,
      sortValue: (b) => b.title.toLowerCase(),
    },
    {
      id: "business",
      header: this.tr("common.business"),
      cell: (b) => b.subtitle ?? "—",
      sortValue: (b) => (b.subtitle ?? "").toLowerCase(),
    },
    {
      id: "status",
      header: this.tr("common.status"),
      cell: (b) => b.statusLabel,
      sortValue: (b) => b.statusLabel,
    },
  ]);

  readonly trackAgendaRow = (block: PlacedBlock): string => block.id;

  readonly sidebarMiniCells = computed((): DayColumn[] => {
    const month = this.sidebarMonth();
    const gridStart = startOfWeek(month);
    const monthEnd = endOfMonth(month);
    const gridEnd = addDays(startOfWeek(monthEnd), 6);
    const today = startOfDay(new Date());
    const out: DayColumn[] = [];
    for (let d = new Date(gridStart); d <= gridEnd; d = addDays(d, 1)) {
      out.push({
        date: d,
        key: dayKey(d),
        dayOfWeek: d.getDay(),
        label: d.toLocaleDateString(undefined, { weekday: "short" }),
        shortLabel: d.toLocaleDateString(undefined, { weekday: "narrow" }),
        dateNum: d.getDate(),
        isToday: sameDay(d, today),
        isPast: d < today,
        bookingCount: 0,
        inMonth: d.getMonth() === month.getMonth(),
      });
    }
    return out;
  });

  readonly rangeHours = computed(() => {
    if (this.view() === "month") {
      return { start: DEFAULT_START_HOUR, end: DEFAULT_END_HOUR };
    }
    const scopeDays = this.visibleDays();
    if (scopeDays.length === 0) {
      return { start: DEFAULT_START_HOUR, end: DEFAULT_END_HOUR };
    }
    const fromBookings = bookingRangeHoursForDays(
      this.bookings(),
      scopeDays.map((d) => d.date),
    );
    return {
      start: Math.min(DEFAULT_START_HOUR, fromBookings.start),
      end: Math.max(DEFAULT_END_HOUR, fromBookings.end),
    };
  });

  readonly hourMarks = computed(() => {
    const { start, end } = this.rangeHours();
    const marks: number[] = [];
    for (let h = start; h <= end; h++) {
      marks.push(h);
    }
    return marks;
  });

  readonly gridHeight = computed(() => {
    const { start, end } = this.rangeHours();
    return (end - start) * PX_PER_HOUR;
  });

  readonly days = computed((): DayColumn[] => {
    const today = startOfDay(new Date());
    const month = this.monthStart();
    const bookingsSrc = this.bookings();

    const buildDay = (date: Date, inMonth: boolean): DayColumn => {
      const key = dayKey(date);
      const bookings = bookingsSrc.filter((b) => overlapsDay(b.startAt, b.endAt, date));
      return {
        date,
        key,
        dayOfWeek: date.getDay(),
        label: date.toLocaleDateString(undefined, { weekday: "short" }),
        shortLabel: date.toLocaleDateString(undefined, { weekday: "narrow" }),
        dateNum: date.getDate(),
        isToday: sameDay(date, today),
        isPast: date < today,
        bookingCount: bookings.length,
        inMonth,
      };
    };

    if (this.view() === "month") {
      return monthGridDays(month).map((d) => buildDay(d, d.getMonth() === month.getMonth()));
    }

    return weekGridDays(this.weekStart()).map((date) => buildDay(date, true));
  });

  readonly visibleDays = computed(() => {
    if (this.view() === "week") {
      return this.days();
    }
    if (this.view() === "month") {
      return [];
    }
    const focus = dayKey(this.focusDay());
    const match = this.days().find((d) => d.key === focus);
    if (match) {
      return [match];
    }
    const date = this.focusDay();
    return [
      {
        date,
        key: focus,
        dayOfWeek: date.getDay(),
        label: date.toLocaleDateString(undefined, { weekday: "short" }),
        shortLabel: date.toLocaleDateString(undefined, { weekday: "narrow" }),
        dateNum: date.getDate(),
        isToday: sameDay(date, startOfDay(new Date())),
        isPast: date < startOfDay(new Date()),
        bookingCount: bookingsOnDay(this.bookings(), focus).length,
        inMonth: true,
      },
    ];
  });

  readonly monthCells = computed((): MonthCell[] => {
    if (this.view() !== "month") {
      return [];
    }
    return this.days().map((day) => ({
      ...day,
      items: this.displayBlocks()
        .filter((b) => b.dayKey === day.key)
        .sort((a, b) => a.top - b.top),
    }));
  });

  readonly weekdayLabels = computed(() => {
    const start = startOfWeek(new Date());
    return DAYS_ORDER.map((_, i) =>
      addDays(start, i).toLocaleDateString(undefined, { weekday: "short" }),
    );
  });

  readonly blocks = computed((): PlacedBlock[] => {
    const bookings = this.bookings();
    const { start } = this.rangeHours();
    const dayStartMin = start * 60;
    const locale = this.locale();
    const out: PlacedBlock[] = [];

    for (const day of this.days()) {
      for (const booking of bookings) {
        const clip = clipToDay(booking.startAt, booking.endAt, day.date);
        if (!clip) {
          continue;
        }
        const top = minutesToPx(Math.max(0, clip.startMin - dayStartMin));
        const height = Math.max(36, minutesToPx(clip.endMin - clip.startMin));
        out.push({
          id: `${booking.id}-${day.key}`,
          dayKey: day.key,
          top,
          height,
          startLabel: formatClock(clip.startMin),
          endLabel: formatClock(clip.endMin),
          title: booking.serviceName,
          subtitle: booking.businessName,
          status: booking.status,
          statusLabel: formatBookingStatusLabel(locale, booking.status),
          booking,
        });
      }
    }

    return out;
  });

  readonly selectedBlock = computed(() => {
    const id = this.selectedId();
    if (!id) {
      return null;
    }
    return this.displayBlocks().find((b) => b.id === id || b.booking.id === id) ?? null;
  });

  readonly focusDayBookings = computed(() =>
    bookingsOnDay(this.bookings(), this.focusDayKey()),
  );

  readonly nowLine = computed(() => {
    const { start, end } = this.rangeHours();
    const now = new Date(this.nowTick());
    const todayCol = this.visibleDays().find((d) => d.isToday);
    if (!todayCol) {
      return null;
    }
    const minutes = now.getHours() * 60 + now.getMinutes();
    const dayStart = start * 60;
    const dayEnd = end * 60;
    if (minutes < dayStart || minutes > dayEnd) {
      return null;
    }
    return {
      dayKey: todayCol.key,
      top: minutesToPx(minutes - dayStart),
      label: now.toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" }),
    };
  });

  readonly periodLabel = computed(() => {
    if (this.view() === "month") {
      return this.monthStart().toLocaleDateString(undefined, {
        month: "long",
        year: "numeric",
      });
    }
    if (this.view() === "day") {
      return this.focusDay().toLocaleDateString(undefined, {
        weekday: "long",
        month: "long",
        day: "numeric",
        year: "numeric",
      });
    }
    const start = this.weekStart();
    const end = addDays(start, 6);
    const sameMonth = start.getMonth() === end.getMonth();
    if (sameMonth) {
      return `${start.toLocaleDateString(undefined, { month: "long" })} ${start.getDate()}–${end.getDate()}, ${start.getFullYear()}`;
    }
    return `${start.toLocaleDateString(undefined, { month: "short", day: "numeric" })} – ${end.toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" })}`;
  });

  readonly stats = computed(() => {
    const scopeDays =
      this.view() === "month"
        ? this.days().filter((d) => d.inMonth)
        : this.visibleDays();
    const bookings = this.bookings().filter((b) =>
      scopeDays.some((d) => overlapsDay(b.startAt, b.endAt, d.date)),
    );
    return { bookings: bookings.length };
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.abort.abort();
      if (this.tickTimer) {
        clearInterval(this.tickTimer);
      }
    });
  }

  ngOnInit(): void {
    if (!this.browser) {
      return;
    }
    if (window.matchMedia("(max-width: 820px)").matches) {
      this.view.set("day");
      this.focusDay.set(startOfDay(new Date()));
    }
    this.tickTimer = setInterval(() => this.nowTick.set(Date.now()), 60_000);
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.failed.set(false);
    try {
      const rows = await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.getMyBookings()),
        this.tr("customer.app.calendar.title"),
        this.tr("toast.loadingDetail"),
      );
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

  onListViewChange(mode: AdeniListViewMode): void {
    this.listViewMode.set(mode);
    this.clearSelection();
  }

  prev(): void {
    if (this.view() === "month") {
      this.monthStart.set(addMonths(this.monthStart(), -1));
      return;
    }
    if (this.view() === "day") {
      const next = addDays(this.focusDay(), -1);
      this.focusDay.set(next);
      this.syncAnchors(next);
      return;
    }
    this.weekStart.set(addDays(this.weekStart(), -7));
  }

  next(): void {
    if (this.view() === "month") {
      this.monthStart.set(addMonths(this.monthStart(), 1));
      return;
    }
    if (this.view() === "day") {
      const next = addDays(this.focusDay(), 1);
      this.focusDay.set(next);
      this.syncAnchors(next);
      return;
    }
    this.weekStart.set(addDays(this.weekStart(), 7));
  }

  goToday(): void {
    const today = startOfDay(new Date());
    this.focusDay.set(today);
    this.weekStart.set(startOfWeek(today));
    this.monthStart.set(startOfMonth(today));
    this.sidebarMonth.set(startOfMonth(today));
    this.clearSelection();
  }

  setView(view: CalendarView): void {
    this.view.set(view);
    if (view === "day") {
      this.syncAnchors(this.focusDay());
      this.focusDayForDayView();
    } else if (view === "week") {
      const anchor = this.focusDay();
      this.weekStart.set(startOfWeek(anchor));
      this.syncAnchors(anchor);
    } else {
      this.monthStart.set(startOfMonth(this.focusDay()));
    }
    this.clearSelection();
  }

  openDay(day: DayColumn): void {
    const already = this.view() === "day" && dayKey(this.focusDay()) === day.key;
    this.focusDay.set(day.date);
    this.weekStart.set(startOfWeek(day.date));
    this.monthStart.set(startOfMonth(day.date));
    this.view.set("day");
    this.clearSelection();
    if (!already) {
      this.syncAnchors(day.date);
    }
  }

  isFocusDay(day: DayColumn): boolean {
    return day.key === dayKey(this.focusDay());
  }

  selectBlock(block: PlacedBlock, event?: Event): void {
    event?.stopPropagation();
    this.selectedId.set(block.id);
    queueMicrotask(() => {
      this.detailRail()?.nativeElement.scrollIntoView({
        behavior: "smooth",
        block: "nearest",
        inline: "nearest",
      });
    });
  }

  clearSelection(): void {
    this.selectedId.set(null);
  }

  blocksForDay(dayKeyValue: string): PlacedBlock[] {
    return this.displayBlocks().filter((b) => b.dayKey === dayKeyValue);
  }

  formatAgendaDay(dayKeyValue: string): string {
    const parts = dayKeyValue.split("-").map((p) => Number.parseInt(p, 10));
    if (parts.length !== 3 || parts.some((n) => Number.isNaN(n))) {
      return dayKeyValue;
    }
    const date = new Date(parts[0]!, parts[1]! - 1, parts[2]!);
    return date.toLocaleDateString(undefined, {
      weekday: "short",
      month: "short",
      day: "numeric",
    });
  }

  blockPillTone(block: PlacedBlock): AdeniPillTone {
    switch (block.status) {
      case 0:
        return "warning";
      case 1:
        return "success";
      case 2:
        return "danger";
      default:
        return "neutral";
    }
  }

  hourLabel(hour: number): string {
    const d = new Date();
    d.setHours(hour, 0, 0, 0);
    return d.toLocaleTimeString(undefined, { hour: "numeric" });
  }

  sidebarMonthLabel(): string {
    return this.sidebarMonth().toLocaleDateString(undefined, { month: "long", year: "numeric" });
  }

  shiftSidebarMonth(delta: number): void {
    this.sidebarMonth.set(addMonths(this.sidebarMonth(), delta));
  }

  pickSidebarDay(cell: DayColumn): void {
    this.focusDay.set(cell.date);
    this.weekStart.set(startOfWeek(cell.date));
    this.monthStart.set(startOfMonth(cell.date));
    this.sidebarMonth.set(startOfMonth(cell.date));
    if (this.view() === "month") {
      this.view.set("week");
    }
    this.clearSelection();
  }

  private syncAnchors(date: Date): void {
    this.weekStart.set(startOfWeek(date));
    this.monthStart.set(startOfMonth(date));
  }

  /** Day view: today when it has events; otherwise next (or latest) booking day. */
  private focusDayForDayView(): void {
    const focus = this.focusDay();
    if (bookingsOnDay(this.bookings(), dayKey(focus)).length > 0) {
      return;
    }
    const rows = this.bookings();
    if (rows.length === 0) {
      return;
    }
    const now = Date.now();
    const upcoming = rows
      .filter((b) => new Date(b.startAt).getTime() >= now)
      .sort((a, b) => +new Date(a.startAt) - +new Date(b.startAt))[0];
    const pick =
      upcoming
      ?? [...rows].sort((a, b) => +new Date(b.startAt) - +new Date(a.startAt))[0]!;
    this.focusDay.set(startOfDay(new Date(pick.startAt)));
    this.syncAnchors(this.focusDay());
  }
}

function minutesToPx(minutes: number): number {
  return (minutes / 60) * PX_PER_HOUR;
}
