import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import {
  DAY_OF_WEEK_LABELS,
  type BusinessClosedDate,
  type WeeklyAvailabilityRule,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniFeedbackService,
  PortalPageComponent,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

const DAYS_ORDER = [1, 2, 3, 4, 5, 6, 0];
const MONTH_LABELS = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December",
];

type DayRow = {
  dayOfWeek: number;
  openTime: string;
  closeTime: string;
  closed: boolean;
};

type CalendarCell = {
  key: string;
  date: string;
  day: number;
  outside: boolean;
  today: boolean;
  closed: boolean;
};

function toInputTime(value: string): string {
  return value.slice(0, 5);
}

function toApiTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

function rulesToRows(rules: WeeklyAvailabilityRule[]): DayRow[] {
  return DAYS_ORDER.map((dayOfWeek) => {
    const rule = rules.find((r) => r.dayOfWeek === dayOfWeek);
    if (!rule) {
      return { dayOfWeek, openTime: "09:00", closeTime: "17:00", closed: dayOfWeek === 0 };
    }
    return {
      dayOfWeek,
      openTime: toInputTime(rule.openTime),
      closeTime: toInputTime(rule.closeTime),
      closed: false,
    };
  });
}

function rowsToRules(rows: DayRow[]): WeeklyAvailabilityRule[] {
  return rows
    .filter((row) => !row.closed)
    .map((row) => ({
      dayOfWeek: row.dayOfWeek,
      openTime: toApiTime(row.openTime),
      closeTime: toApiTime(row.closeTime),
    }));
}

function pad2(n: number): string {
  return n < 10 ? `0${n}` : `${n}`;
}

function toDateKey(year: number, monthIndex: number, day: number): string {
  return `${year}-${pad2(monthIndex + 1)}-${pad2(day)}`;
}

function todayKey(): string {
  const now = new Date();
  return toDateKey(now.getFullYear(), now.getMonth(), now.getDate());
}

function isDateInRanges(date: string, ranges: BusinessClosedDate[]): boolean {
  return ranges.some((r) => r.startDate <= date && r.endDate >= date);
}

function findRangeCovering(date: string, ranges: BusinessClosedDate[]): BusinessClosedDate | undefined {
  return ranges.find((r) => r.startDate <= date && r.endDate >= date);
}

@Component({
  selector: "app-availability",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, AdeniCarbonIconComponent],
  templateUrl: "./availability.component.html",
  styleUrl: "./availability.component.scss",
})
export class AvailabilityComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly closedBusy = signal(false);
  readonly error = signal<string | null>(null);
  readonly dayLabels = DAY_OF_WEEK_LABELS;
  readonly dowHeaders = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

  rows: DayRow[] = [];
  closedRanges = signal<BusinessClosedDate[]>([]);
  viewYear = signal(new Date().getFullYear());
  viewMonth = signal(new Date().getMonth());
  rangeStart = "";
  rangeEnd = "";
  rangeReason = "";
  conflictWarning = signal<string | null>(null);

  readonly monthLabel = computed(() => {
    return `${MONTH_LABELS[this.viewMonth()]} ${this.viewYear()}`;
  });

  readonly calendarCells = computed((): CalendarCell[] => {
    const year = this.viewYear();
    const month = this.viewMonth();
    const ranges = this.closedRanges();
    const first = new Date(year, month, 1);
    // Monday-first: JS Sun=0 → shift so Mon=0
    const startOffset = (first.getDay() + 6) % 7;
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const today = todayKey();
    const cells: CalendarCell[] = [];

    for (let i = 0; i < startOffset; i++) {
      const d = new Date(year, month, 1 - (startOffset - i));
      const date = toDateKey(d.getFullYear(), d.getMonth(), d.getDate());
      cells.push({
        key: `out-${date}`,
        date,
        day: d.getDate(),
        outside: true,
        today: date === today,
        closed: isDateInRanges(date, ranges),
      });
    }

    for (let day = 1; day <= daysInMonth; day++) {
      const date = toDateKey(year, month, day);
      cells.push({
        key: date,
        date,
        day,
        outside: false,
        today: date === today,
        closed: isDateInRanges(date, ranges),
      });
    }

    while (cells.length % 7 !== 0) {
      const last = cells[cells.length - 1]!;
      const d = new Date(`${last.date}T12:00:00`);
      d.setDate(d.getDate() + 1);
      const date = toDateKey(d.getFullYear(), d.getMonth(), d.getDate());
      cells.push({
        key: `out-${date}`,
        date,
        day: d.getDate(),
        outside: true,
        today: date === today,
        closed: isDateInRanges(date, ranges),
      });
    }

    return cells;
  });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [rules, closed] = await Promise.all([
        this.api.withAuthorizedClient((c) => c.getTenantAvailability()),
        this.api.withAuthorizedClient((c) => c.getTenantClosedDates()),
      ]);
      this.rows = rulesToRows(rules);
      this.closedRanges.set(closed);
    } catch {
      this.error.set("Could not load availability.");
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    try {
      const saved = await this.api.withAuthorizedClient((c) =>
        c.replaceTenantAvailability(rowsToRules(this.rows)),
      );
      this.rows = rulesToRows(saved);
      this.feedback.success("Weekly hours saved.");
    } catch {
      this.error.set("Could not save availability.");
    } finally {
      this.saving.set(false);
    }
  }

  prevMonth(): void {
    const m = this.viewMonth();
    if (m === 0) {
      this.viewMonth.set(11);
      this.viewYear.update((y) => y - 1);
    } else {
      this.viewMonth.set(m - 1);
    }
  }

  nextMonth(): void {
    const m = this.viewMonth();
    if (m === 11) {
      this.viewMonth.set(0);
      this.viewYear.update((y) => y + 1);
    } else {
      this.viewMonth.set(m + 1);
    }
  }

  async toggleDay(cell: CalendarCell): Promise<void> {
    if (this.closedBusy()) {
      return;
    }

    const covering = findRangeCovering(cell.date, this.closedRanges());
    if (covering) {
      if (covering.startDate === covering.endDate) {
        await this.removeClosed(covering);
        return;
      }
      this.feedback.error(
        "That day is part of a multi-day range. Remove the range from the list, or add a single-day block.",
      );
      return;
    }

    await this.createClosed(cell.date, cell.date, null);
  }

  async addClosedRange(): Promise<void> {
    if (!this.rangeStart || !this.rangeEnd) {
      this.feedback.error("Choose a start and end date.");
      return;
    }
    if (this.rangeEnd < this.rangeStart) {
      this.feedback.error("End date must be on or after start date.");
      return;
    }
    await this.createClosed(this.rangeStart, this.rangeEnd, this.rangeReason.trim() || null);
  }

  async removeClosed(item: BusinessClosedDate): Promise<void> {
    const label =
      item.startDate === item.endDate
        ? item.startDate
        : `${item.startDate} – ${item.endDate}`;
    const ok = await this.confirmDialog.confirm({
      title: "Open these dates?",
      message: `Remove closed range ${label}? Customers will be able to book again subject to weekly hours.`,
      confirmLabel: "Remove",
      cancelLabel: "Keep closed",
      danger: true,
    });
    if (!ok) {
      return;
    }

    this.closedBusy.set(true);
    this.conflictWarning.set(null);
    try {
      await this.api.withAuthorizedClient((c) => c.deleteTenantClosedDate(item.id));
      this.closedRanges.update((items) => items.filter((x) => x.id !== item.id));
      this.feedback.success("Dates reopened.");
    } catch {
      this.feedback.error("Could not remove closed range.");
    } finally {
      this.closedBusy.set(false);
    }
  }

  private async createClosed(
    startDate: string,
    endDate: string,
    reason: string | null,
  ): Promise<void> {
    this.closedBusy.set(true);
    this.conflictWarning.set(null);
    try {
      const created = await this.api.withAuthorizedClient((c) =>
        c.createTenantClosedDate({ startDate, endDate, reason }),
      );
      this.closedRanges.update((items) =>
        [...items, created].sort((a, b) =>
          a.startDate === b.startDate
            ? a.endDate.localeCompare(b.endDate)
            : a.startDate.localeCompare(b.startDate),
        ),
      );
      this.rangeStart = "";
      this.rangeEnd = "";
      this.rangeReason = "";
      const conflicts = created.conflictingBookingIds?.length ?? 0;
      if (conflicts > 0) {
        this.conflictWarning.set(
          `Closed dates saved. ${conflicts} existing booking${conflicts === 1 ? "" : "s"} fall in this range — not cancelled. Review Bookings.`,
        );
        this.feedback.success("Closed dates saved (existing bookings warned).");
      } else {
        this.feedback.success("Closed dates saved.");
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Could not close dates.";
      this.feedback.error(message);
    } finally {
      this.closedBusy.set(false);
    }
  }
}
