import {
  Component,
  computed,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  viewChild,
} from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import type {
  StaffCalendarBookingItem,
  StaffCalendarLeaveItem,
  StaffCalendarResponse,
  StaffTeamCalendarResponse,
  TenantEntitlements,
  WeeklyAvailabilityRule,
} from "@adeni/shared";
import { Subscription } from "rxjs";
import {
  AdeniCarbonIconComponent,
  AdeniIconBtnComponent,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniFeedbackService,
  AdeniFilterSelectComponent,
  AdeniListFilterRowComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  CALENDAR_DAYS_ORDER,
  PortalPageComponent,
  readPersistedListViewMode,
  addDays,
  addMonths,
  clipToDay,
  dayKey,
  endOfMonth,
  formatBookedHours,
  formatClock,
  formatDay,
  overlapsDay,
  parseTimeToMinutes,
  sameDay,
  startOfDay,
  startOfMonth,
  startOfWeek,
  type AdeniDataTableColumn,
  type AdeniFilterSelectOption,
  type AdeniListViewMode,
  type AdeniPillTone,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
import { PortalSessionService } from "../../core/services/portal-session.service";

const PX_PER_HOUR = 64;
const DEFAULT_START_HOUR = 8;
const DEFAULT_END_HOUR = 20;
const DAYS_ORDER = CALENDAR_DAYS_ORDER;
const STAFF_EVENT_PALETTE = ["#6929c4", "#1192e8", "#9f1853", "#198038", "#b28600", "#697077"];

type CalendarView = "month" | "week" | "day";
type CalendarColorBy = "staff" | "status";
type CalendarKindFilter = "all" | "bookings" | "leave";
type CalendarStatusFilter =
  | "all"
  | "pending"
  | "confirmed"
  | "rejected"
  | "cancelled"
  | "leave";

type DayColumn = {
  date: Date;
  key: string;
  dayOfWeek: number;
  label: string;
  shortLabel: string;
  dateNum: number;
  isToday: boolean;
  isPast: boolean;
  openMinutes: number | null;
  closeMinutes: number | null;
  isClosed: boolean;
  bookingCount: number;
  leaveCount: number;
  inMonth: boolean;
};

type PlacedBlock = {
  id: string;
  kind: "booking" | "leave";
  dayKey: string;
  top: number;
  height: number;
  startLabel: string;
  endLabel: string;
  title: string;
  subtitle: string | null;
  status: number | null;
  statusLabel: string | null;
  customerSelectedStaff: boolean;
  staffDisplayName: string | null;
  staffMemberId: string | null;
  booking: StaffCalendarBookingItem | null;
  leave: StaffCalendarLeaveItem | null;
};

type MonthCell = DayColumn & {
  items: PlacedBlock[];
};

@Component({
  selector: "app-staff-calendar",
  standalone: true,
  imports: [
    PortalPageComponent,
    RouterLink,
    AdeniCarbonIconComponent,
    AdeniIconBtnComponent,
    AdeniListToolbarComponent,
    AdeniListFilterRowComponent,
    AdeniFilterSelectComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniPillComponent,
    AdeniPortalEmptyStateComponent,
  ],
  templateUrl: "./staff-calendar.component.html",
  styleUrl: "./staff-calendar.component.scss",
})
export class StaffCalendarComponent implements OnInit, OnDestroy {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly session = inject(PortalSessionService);
  private readonly activeLocation = inject(PortalActiveLocationService);

  private readonly detailRail = viewChild<ElementRef<HTMLElement>>("detailRail");

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly calendar = signal<StaffCalendarResponse | null>(null);
  readonly team = signal<StaffTeamCalendarResponse | null>(null);
  /** Team board for owners/managers; member/self for a single practitioner. */
  readonly teamMode = signal(false);
  /** Hidden practitioners on the team board (empty = everyone visible). */
  readonly hiddenStaffIds = signal<Set<string>>(new Set());
  readonly staffSidebarSearch = signal("");
  readonly staffSidebarSearchFocused = signal(false);
  readonly staffSidebarCompact = signal(false);
  readonly listViewMode = signal<AdeniListViewMode>(
    readPersistedListViewMode("portal.calendar", "cards"),
  );
  readonly calendarSearch = signal("");
  readonly kindFilter = signal<CalendarKindFilter>("all");
  readonly statusFilter = signal<CalendarStatusFilter>("all");
  readonly colorBy = signal<CalendarColorBy>("staff");
  readonly sidebarMonth = signal(startOfMonth(new Date()));
  readonly weekStart = signal(startOfWeek(new Date()));
  readonly monthStart = signal(startOfMonth(new Date()));
  readonly view = signal<CalendarView>("week");
  readonly focusDay = signal(startOfDay(new Date()));
  readonly selectedId = signal<string | null>(null);
  readonly nowTick = signal(Date.now());

  staffId = "";
  private tickTimer: ReturnType<typeof setInterval> | null = null;
  private branchSub: Subscription | null = null;
  private tenantEntitlements = signal<TenantEntitlements | null>(null);

  readonly activeHours = computed((): WeeklyAvailabilityRule[] => {
    if (this.teamMode()) {
      return [];
    }
    return this.calendar()?.hours ?? [];
  });

  readonly inheritsBusinessHours = computed(() => {
    if (this.teamMode()) {
      return true;
    }
    return this.calendar()?.inheritsBusinessHours ?? false;
  });

  private visibleForStaff(staffMemberId: string | null | undefined): boolean {
    if (!staffMemberId) {
      return true;
    }
    return !this.hiddenStaffIds().has(staffMemberId);
  }

  readonly sidebarTeamStaff = computed((): StaffTeamCalendarResponse["staff"] => {
    const team = this.team();
    if (!team) {
      return [];
    }
    const query = this.staffSidebarSearch().trim().toLowerCase();
    if (!query) {
      return team.staff;
    }
    return team.staff.filter((member) => member.displayName.toLowerCase().includes(query));
  });

  readonly visibleTeamStaffCount = computed(() => {
    const team = this.team();
    if (!team) {
      return 0;
    }
    return team.staff.filter((member) => this.visibleForStaff(member.staffMemberId)).length;
  });

  readonly sourceBookings = computed((): StaffCalendarBookingItem[] => {
    if (this.teamMode()) {
      const team = this.team();
      if (!team) {
        return [];
      }
      return team.bookings.filter((b) => this.visibleForStaff(b.staffMemberId));
    }
    return this.calendar()?.bookings ?? [];
  });

  readonly sourceLeave = computed((): StaffCalendarLeaveItem[] => {
    if (this.teamMode()) {
      const team = this.team();
      if (!team) {
        return [];
      }
      return team.leave.filter((l) => this.visibleForStaff(l.staffMemberId));
    }
    return this.calendar()?.leave ?? [];
  });

  readonly boardTitle = computed(() => {
    if (this.teamMode()) {
      return this.tr("portal.dashboard.appointments");
    }
    return this.calendar()?.displayName
      ? `${this.calendar()!.displayName} · Calendar`
      : this.tr("portal.dashboard.myCalendar");
  });

  readonly boardSubtitle = computed(() => this.appointmentSummary());

  readonly appointmentSummary = computed(() => {
    const count = this.stats().bookings;
    const label = count === 1 ? "appointment" : "appointments";
    return `${count} ${label} · ${this.periodLabel()}`;
  });

  readonly focusDayKey = computed(() => dayKey(this.focusDay()));

  readonly kindFilterOptions = computed<AdeniFilterSelectOption[]>(() => [
    { value: "all", label: this.tr("portal.staff.allItems") },
    { value: "bookings", label: this.tr("portal.dashboard.appointments") },
    { value: "leave", label: this.tr("common.leave") },
  ]);

  readonly statusFilterOptions = computed<AdeniFilterSelectOption[]>(() => [
    { value: "all", label: this.tr("portal.staff.allStatuses") },
    { value: "pending", label: this.tr("portal.staff.pending") },
    { value: "confirmed", label: this.tr("portal.staff.confirmed") },
    { value: "rejected", label: this.tr("admin.pending.rejected") },
    { value: "cancelled", label: this.tr("portal.bookings.cancelled") },
    { value: "leave", label: this.tr("common.leave") },
  ]);

  readonly displayBlocks = computed((): PlacedBlock[] => {
    let list = this.blocks().filter(
      (b) =>
        this.view() !== "month" || this.days().find((d) => d.key === b.dayKey)?.inMonth,
    );

    const kind = this.kindFilter();
    if (kind === "bookings") {
      list = list.filter((b) => b.kind === "booking");
    } else if (kind === "leave") {
      list = list.filter((b) => b.kind === "leave");
    }

    const status = this.statusFilter();
    if (status === "leave") {
      list = list.filter((b) => b.kind === "leave");
    } else if (status !== "all") {
      list = list.filter((b) => b.kind === "booking" && this.statusMatchesFilter(b.status, status));
    }

    const query = this.calendarSearch().trim().toLowerCase();
    if (query) {
      list = list.filter((b) => {
        const haystack = [
          b.title,
          b.staffDisplayName ?? "",
          b.statusLabel ?? "",
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
      header: this.tr("portal.bookings.when"),
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
      id: "staff",
      header: this.tr("common.staff"),
      cell: (b) => b.staffDisplayName ?? "—",
      sortValue: (b) => (b.staffDisplayName ?? "").toLowerCase(),
    },
    {
      id: "status",
      header: this.tr("common.status"),
      cell: (b) => (b.kind === "leave" ? this.tr("common.leave") : (b.statusLabel ?? "—")),
      sortValue: (b) => (b.kind === "leave" ? "leave" : (b.statusLabel ?? "")),
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
        openMinutes: null,
        closeMinutes: null,
        isClosed: false,
        bookingCount: 0,
        leaveCount: 0,
        inMonth: d.getMonth() === month.getMonth(),
      });
    }
    return out;
  });


  readonly rangeHours = computed(() => {
    const hours = this.activeHours();
    let start = DEFAULT_START_HOUR;
    let end = DEFAULT_END_HOUR;
    if (hours.length) {
      const opens = hours.map((h) => parseTimeToMinutes(h.openTime) / 60);
      const closes = hours.map((h) => parseTimeToMinutes(h.closeTime) / 60);
      start = Math.max(6, Math.floor(Math.min(...opens)) - 1);
      end = Math.min(23, Math.ceil(Math.max(...closes)) + 1);
    }
    if (end <= start) {
      end = start + 8;
    }
    return { start, end };
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
    const hoursByDow = indexHours(this.activeHours());
    const month = this.monthStart();
    const bookingsSrc = this.sourceBookings();
    const leaveSrc = this.sourceLeave();
    const hasSchedule = !this.teamMode() && this.calendar() != null;
    const inherits = this.inheritsBusinessHours();

    const buildDay = (date: Date, inMonth: boolean): DayColumn => {
      const dayOfWeek = date.getDay();
      const key = dayKey(date);
      const rule = hoursByDow.get(dayOfWeek);
      const bookings = bookingsSrc.filter((b) => overlapsDay(b.startAt, b.endAt, date));
      const leave = leaveSrc.filter((l) => overlapsDay(l.startAt, l.endAt, date));
      return {
        date,
        key,
        dayOfWeek,
        label: date.toLocaleDateString(undefined, { weekday: "short" }),
        shortLabel: date.toLocaleDateString(undefined, { weekday: "narrow" }),
        dateNum: date.getDate(),
        isToday: sameDay(date, today),
        isPast: date < today,
        openMinutes: rule ? parseTimeToMinutes(rule.openTime) : null,
        closeMinutes: rule ? parseTimeToMinutes(rule.closeTime) : null,
        isClosed: hasSchedule ? !rule && !inherits : false,
        bookingCount: bookings.length,
        leaveCount: leave.length,
        inMonth,
      };
    };

    if (this.view() === "month") {
      const gridStart = startOfWeek(month);
      const monthEnd = endOfMonth(month);
      const gridEnd = addDays(startOfWeek(monthEnd), 6);
      const out: DayColumn[] = [];
      for (let d = new Date(gridStart); d <= gridEnd; d = addDays(d, 1)) {
        out.push(buildDay(d, d.getMonth() === month.getMonth()));
      }
      return out;
    }

    return DAYS_ORDER.map((dayOfWeek, i) => {
      const date = addDays(this.weekStart(), i);
      return buildDay(date, true);
    });
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
    // Day may sit outside loaded week — synthesize one column.
    return [
      {
        date: this.focusDay(),
        key: focus,
        dayOfWeek: this.focusDay().getDay(),
        label: this.focusDay().toLocaleDateString(undefined, { weekday: "short" }),
        shortLabel: this.focusDay().toLocaleDateString(undefined, { weekday: "narrow" }),
        dateNum: this.focusDay().getDate(),
        isToday: sameDay(this.focusDay(), startOfDay(new Date())),
        isPast: this.focusDay() < startOfDay(new Date()),
        openMinutes: null,
        closeMinutes: null,
        isClosed: false,
        bookingCount: 0,
        leaveCount: 0,
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
    const bookings = this.sourceBookings();
    const leaveItems = this.sourceLeave();
    if (!this.teamMode() && !this.calendar()) {
      return [];
    }
    if (this.teamMode() && !this.team()) {
      return [];
    }
    const { start } = this.rangeHours();
    const dayStartMin = start * 60;
    const out: PlacedBlock[] = [];

    for (const day of this.days()) {
      for (const leave of leaveItems) {
        const clip = clipToDay(leave.startAt, leave.endAt, day.date);
        if (!clip) {
          continue;
        }
        const top = minutesToPx(clip.startMin - dayStartMin);
        const height = Math.max(28, minutesToPx(clip.endMin - clip.startMin));
        out.push({
          id: `leave-${leave.id}-${day.key}`,
          kind: "leave",
          dayKey: day.key,
          top,
          height,
          startLabel: formatClock(clip.startMin),
          endLabel: formatClock(clip.endMin),
          title: leave.staffDisplayName
            ? this.tr("portal.staff.timeOff", { staffDisplayName: leave.staffDisplayName })
            : this.tr("portal.staff.timeOff2"),
          subtitle: leave.reason ?? null,
          status: null,
          statusLabel: null,
          customerSelectedStaff: false,
          staffDisplayName: leave.staffDisplayName ?? null,
          staffMemberId: leave.staffMemberId ?? null,
          booking: null,
          leave,
        });
      }

      for (const booking of bookings) {
        const clip = clipToDay(booking.startAt, booking.endAt, day.date);
        if (!clip) {
          continue;
        }
        const top = minutesToPx(clip.startMin - dayStartMin);
        const height = Math.max(36, minutesToPx(clip.endMin - clip.startMin));
        const selected = booking.customerSelectedStaff !== false && !!booking.staffMemberId;
        out.push({
          id: `${booking.id}-${day.key}`,
          kind: "booking",
          dayKey: day.key,
          top,
          height,
          startLabel: formatClock(clip.startMin),
          endLabel: formatClock(clip.endMin),
          title: booking.serviceName,
          subtitle:
            booking.customerNotes?.trim()
            || (this.teamMode() ? booking.staffDisplayName?.trim() || this.tr("portal.staff.anyAvailable") : null),
          status: booking.status,
          statusLabel: this.bookingStatusLabel(booking.status),
          customerSelectedStaff: selected,
          staffDisplayName: booking.staffDisplayName ?? null,
          staffMemberId: booking.staffMemberId ?? null,
          booking,
          leave: null,
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
    return this.displayBlocks().find((b) => b.id === id || b.booking?.id === id) ?? null;
  });

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
    return `${formatDay(start)} – ${formatDay(end)}`;
  });

  readonly stats = computed(() => {
    const scopeDays = this.days().filter((d) => this.view() !== "month" || d.inMonth);
    const bookings = this.sourceBookings().filter((b) =>
      scopeDays.some((d) => overlapsDay(b.startAt, b.endAt, d.date)),
    );
    const confirmed = bookings.filter((b) => b.status === 1);
    const bookedMinutes = confirmed.reduce((sum, b) => {
      const ms = new Date(b.endAt).getTime() - new Date(b.startAt).getTime();
      return sum + Math.max(0, Math.round(ms / 60000));
    }, 0);
    return {
      bookings: bookings.length,
      confirmed: confirmed.length,
      leave: this.sourceLeave().filter((l) =>
        scopeDays.some((d) => overlapsDay(l.startAt, l.endAt, d.date)),
      ).length,
      bookedMinutes,
      bookedHoursLabel: formatBookedHours(bookedMinutes),
      requested: bookings.filter(
        (b) => b.customerSelectedStaff !== false && !!b.staffMemberId,
      ).length,
    };
  });

  ngOnInit(): void {
    void this.bootstrap();
    this.branchSub = this.activeLocation.selectionChanged$.subscribe(() => {
      void this.activeLocation.runOnBranchSwitch(() => this.load());
    });
  }

  private async bootstrap(): Promise<void> {
    await this.session.ensureLoaded();
    await this.api.withAuthorizedClient(async (c) => {
      const profile = await c.getTenantProfile().catch(() => null);
      this.activeLocation.syncFromProfile(profile);
      this.tenantEntitlements.set(profile?.entitlements ?? null);
    });
    const routeId = this.route.snapshot.paramMap.get("id");
    const path = this.route.snapshot.routeConfig?.path ?? "";
    const isTeamRoute = path === "calendar" || this.route.snapshot.data["teamCalendar"] === true;
    this.teamMode.set(isTeamRoute && this.session.can("portal.staff"));
    this.colorBy.set(isTeamRoute && this.session.can("portal.staff") ? "staff" : "status");
    this.staffId = routeId ?? this.session.staffMemberId() ?? "";
    if (typeof window !== "undefined" && window.matchMedia("(max-width: 820px)").matches) {
      this.view.set("day");
      this.focusDay.set(startOfDay(new Date()));
    }
    this.tickTimer = setInterval(() => this.nowTick.set(Date.now()), 60_000);
    await this.load();
  }

  onListViewChange(mode: AdeniListViewMode): void {
    this.listViewMode.set(mode);
    this.clearSelection();
  }

  setColorBy(mode: CalendarColorBy): void {
    this.colorBy.set(mode);
  }

  isStaffVisible(staffMemberId: string): boolean {
    return this.visibleForStaff(staffMemberId);
  }

  toggleStaffVisibility(staffMemberId: string): void {
    const next = new Set(this.hiddenStaffIds());
    if (next.has(staffMemberId)) {
      next.delete(staffMemberId);
    } else {
      next.add(staffMemberId);
    }
    this.hiddenStaffIds.set(next);
    this.clearSelection();
  }

  onStaffSidebarSearchChange(value: string): void {
    this.staffSidebarSearch.set(value);
  }

  clearStaffSidebarSearch(): void {
    this.staffSidebarSearch.set("");
  }

  showAllTeamStaffOnCalendar(): void {
    const filteredIds = new Set(this.sidebarTeamStaff().map((m) => m.staffMemberId));
    if (filteredIds.size === 0) {
      return;
    }
    const next = new Set(this.hiddenStaffIds());
    for (const id of filteredIds) {
      next.delete(id);
    }
    this.hiddenStaffIds.set(next);
    this.clearSelection();
  }

  hideAllTeamStaffOnCalendar(): void {
    const filtered = this.sidebarTeamStaff();
    if (filtered.length === 0) {
      return;
    }
    const next = new Set(this.hiddenStaffIds());
    for (const member of filtered) {
      next.add(member.staffMemberId);
    }
    this.hiddenStaffIds.set(next);
    this.clearSelection();
  }

  toggleStaffSidebarCompact(): void {
    this.staffSidebarCompact.update((value) => !value);
  }

  staffAccentColor(staffMemberId: string | null): string {
    if (!staffMemberId) {
      return STAFF_EVENT_PALETTE[STAFF_EVENT_PALETTE.length - 1]!;
    }
    const team = this.team()?.staff ?? [];
    const idx = team.findIndex((s) => s.staffMemberId === staffMemberId);
    if (idx < 0) {
      return STAFF_EVENT_PALETTE[0]!;
    }
    return STAFF_EVENT_PALETTE[idx % STAFF_EVENT_PALETTE.length]!;
  }

  staffInitials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0]!.charAt(0) + parts[1]!.charAt(0)).toUpperCase();
    }
    return (parts[0] ?? "?").slice(0, 2).toUpperCase();
  }

  eventUsesStaffColor(block: PlacedBlock): boolean {
    return this.teamMode() && this.colorBy() === "staff" && block.kind === "booking";
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
    void this.load();
  }

  private bookingStatusLabel(status: number): string {
    switch (status) {
      case 0:
        return this.tr("portal.staff.pending");
      case 1:
        return this.tr("portal.staff.confirmed");
      case 2:
        return this.tr("portal.staff.statusRejected");
      case 3:
        return this.tr("portal.staff.statusCancelled");
      default:
        return this.tr("portal.staff.statusUnknown");
    }
  }

  hoursLabel(minutes: number): string {
    return formatBookedHours(minutes);
  }

  /** Sidebar copy — spelled-out hours for the current calendar range. */
  staffBookedHoursSummary(minutes: number): string {
    if (minutes <= 0) {
      return this.tr("portal.staff.noHoursBookedRange");
    }
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    const parts: string[] = [];
    if (hours > 0) {
      parts.push(this.tr(hours === 1 ? "portal.staff.hourCountOne" : "portal.staff.hourCountMany", { count: hours }));
    }
    if (mins > 0) {
      parts.push(this.tr("portal.staff.minuteCount", { count: mins }));
    }
    return this.tr("portal.staff.totalDuration", { duration: parts.join(" ") });
  }

  /** Roster admins see a back link; practitioners viewing their own calendar do not. */
  showRosterBackLink(): boolean {
    return this.session.can("portal.staff");
  }

  ngOnDestroy(): void {
    if (this.tickTimer) {
      clearInterval(this.tickTimer);
    }
    this.branchSub?.unsubscribe();
  }

  /** Active shell branch when set; otherwise same resolution as other tenant list APIs. */
  private branchLocationQuery(): string | undefined {
    const branch = this.activeLocation.selectedLocation();
    if (branch) {
      return branch.id;
    }
    return this.activeLocation.tenantApiLocationId(this.tenantEntitlements());
  }

  prev(): void {
    if (this.view() === "month") {
      this.monthStart.set(addMonths(this.monthStart(), -1));
      void this.load();
      return;
    }
    if (this.view() === "day") {
      const next = addDays(this.focusDay(), -1);
      this.focusDay.set(next);
      this.syncAnchors(next);
      void this.load();
      return;
    }
    this.weekStart.set(addDays(this.weekStart(), -7));
    void this.load();
  }

  next(): void {
    if (this.view() === "month") {
      this.monthStart.set(addMonths(this.monthStart(), 1));
      void this.load();
      return;
    }
    if (this.view() === "day") {
      const next = addDays(this.focusDay(), 1);
      this.focusDay.set(next);
      this.syncAnchors(next);
      void this.load();
      return;
    }
    this.weekStart.set(addDays(this.weekStart(), 7));
    void this.load();
  }

  goToday(): void {
    const today = startOfDay(new Date());
    this.focusDay.set(today);
    this.weekStart.set(startOfWeek(today));
    this.monthStart.set(startOfMonth(today));
    this.sidebarMonth.set(startOfMonth(today));
    void this.load();
  }

  setView(view: CalendarView): void {
    this.view.set(view);
    if (view === "day") {
      this.syncAnchors(this.focusDay());
    } else if (view === "week") {
      this.weekStart.set(startOfWeek(this.focusDay()));
    } else {
      this.monthStart.set(startOfMonth(this.focusDay()));
    }
    void this.load();
  }

  /** Day header / month cell / week column → open that day on the timeline. */
  openDay(day: DayColumn): void {
    const already =
      this.view() === "day" && dayKey(this.focusDay()) === day.key;
    this.focusDay.set(day.date);
    this.weekStart.set(startOfWeek(day.date));
    this.monthStart.set(startOfMonth(day.date));
    this.view.set("day");
    this.clearSelection();
    if (!already) {
      void this.load();
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
    if (block.kind === "leave") {
      return "neutral";
    }
    switch (block.status) {
      case 0:
        return "warning";
      case 1:
        return "success";
      case 2:
        return "danger";
      case 3:
        return "neutral";
      default:
        return "info";
    }
  }

  private statusMatchesFilter(
    status: number | null,
    filter: CalendarStatusFilter,
  ): boolean {
    switch (filter) {
      case "pending":
        return status === 0;
      case "confirmed":
        return status === 1;
      case "rejected":
        return status === 2;
      case "cancelled":
        return status === 3;
      default:
        return true;
    }
  }

  offBandStyle(day: DayColumn): { top: string; height: string }[] {
    const { start, end } = this.rangeHours();
    const dayStart = start * 60;
    const dayEnd = end * 60;
    if (day.isClosed || day.openMinutes == null || day.closeMinutes == null) {
      if (!this.inheritsBusinessHours() || day.isClosed) {
        return [{ top: "0", height: `${this.gridHeight()}px` }];
      }
      return [];
    }
    const bands: { top: string; height: string }[] = [];
    if (day.openMinutes > dayStart) {
      bands.push({
        top: "0",
        height: `${minutesToPx(day.openMinutes - dayStart)}px`,
      });
    }
    if (day.closeMinutes < dayEnd) {
      bands.push({
        top: `${minutesToPx(day.closeMinutes - dayStart)}px`,
        height: `${minutesToPx(dayEnd - day.closeMinutes)}px`,
      });
    }
    return bands;
  }

  hourLabel(hour: number): string {
    const d = new Date();
    d.setHours(hour, 0, 0, 0);
    return d.toLocaleTimeString(undefined, { hour: "numeric" });
  }

  statusClass(status: number | null): string {
    switch (status) {
      case 0:
        return "is-pending";
      case 1:
        return "is-confirmed";
      case 2:
        return "is-rejected";
      case 3:
        return "is-cancelled";
      default:
        return "";
    }
  }

  private syncAnchors(date: Date): void {
    this.weekStart.set(startOfWeek(date));
    this.monthStart.set(startOfMonth(date));
  }

  private loadRange(): { from: Date; to: Date } {
    if (this.view() === "month") {
      const gridStart = startOfWeek(this.monthStart());
      const monthEnd = endOfMonth(this.monthStart());
      const gridEnd = addDays(startOfWeek(monthEnd), 7); // exclusive end
      return { from: gridStart, to: gridEnd };
    }
    if (this.view() === "day") {
      const day = startOfDay(this.focusDay());
      return { from: day, to: addDays(day, 1) };
    }
    const from = this.weekStart();
    return { from, to: addDays(from, 7) };
  }

  async load(): Promise<void> {
    if (!this.teamMode() && !this.staffId) {
      this.error.set(this.tr("portal.staff.staffMemberNotFound"));
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const { from, to } = this.loadRange();
    const locationId = this.branchLocationQuery();
    try {
      await this.feedback.runLoading(async () => {
        if (this.teamMode()) {
          const data = await this.api.withAuthorizedClient((c) =>
            c.getTenantTeamCalendar({
              from: from.toISOString(),
              to: to.toISOString(),
              locationId,
            }),
          );
          this.team.set(data);
          this.calendar.set(null);
        } else {
          const data = await this.api.withAuthorizedClient((c) =>
            c.getTenantStaffCalendar(this.staffId, {
              from: from.toISOString(),
              to: to.toISOString(),
              locationId,
            }),
          );
          this.calendar.set(data);
          this.team.set(null);
        }
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.staff.couldNotLoadCalendar"));
      this.calendar.set(null);
      this.team.set(null);
    } finally {
      this.loading.set(false);
    }
  }
}

function minutesToPx(minutes: number): number {
  return (minutes / 60) * PX_PER_HOUR;
}

function indexHours(hours: WeeklyAvailabilityRule[]): Map<number, WeeklyAvailabilityRule> {
  const map = new Map<number, WeeklyAvailabilityRule>();
  for (const rule of hours) {
    map.set(rule.dayOfWeek, rule);
  }
  return map;
}
