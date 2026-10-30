import type { CustomerBookingResponse } from "@adeni/shared";
import { addDays, endOfMonth, startOfMonth, startOfWeek } from "@adeni/ui";

/** Local YYYY-MM-DD for a Date (calendar grid keys). */
export function dayKey(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

export function dayKeyFromIso(iso: string): string {
  return dayKey(new Date(iso));
}

export function addMonths(anchor: Date, delta: number): Date {
  return new Date(anchor.getFullYear(), anchor.getMonth() + delta, 1);
}

/** Month grid cells (Mon-start week rows), matching business staff calendar. */
export function monthGridDays(anchor: Date): Date[] {
  const month = startOfMonth(anchor);
  const gridStart = startOfWeek(month);
  const monthEnd = endOfMonth(month);
  const gridEnd = addDays(startOfWeek(monthEnd), 6);
  const out: Date[] = [];
  for (let d = new Date(gridStart); d <= gridEnd; d = addDays(d, 1)) {
    out.push(d);
  }
  return out;
}

/** Mon–Sun week containing `anchor`. */
export function weekGridDays(anchor: Date): Date[] {
  const start = startOfWeek(anchor);
  return Array.from({ length: 7 }, (_, i) => addDays(start, i));
}

export function bookingsOnDay(
  bookings: readonly CustomerBookingResponse[],
  key: string,
): CustomerBookingResponse[] {
  return bookings
    .filter((b) => dayKeyFromIso(b.startAt) === key)
    .sort((a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime());
}

export function bookingCountByDay(
  bookings: readonly CustomerBookingResponse[],
): Map<string, number> {
  const map = new Map<string, number>();
  for (const booking of bookings) {
    const key = dayKeyFromIso(booking.startAt);
    map.set(key, (map.get(key) ?? 0) + 1);
  }
  return map;
}

export function isSameMonth(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth();
}

export function isSameDay(a: Date, b: Date): boolean {
  return dayKey(a) === dayKey(b);
}

export const CALENDAR_PX_PER_HOUR = 64;

export function clipIntervalToDay(
  startIso: string,
  endIso: string,
  day: Date,
): { startMin: number; endMin: number } | null {
  const dayStart = new Date(day.getFullYear(), day.getMonth(), day.getDate());
  const dayEnd = addDays(dayStart, 1);
  const start = new Date(startIso);
  const end = new Date(endIso);
  if (end.getTime() <= dayStart.getTime() || start.getTime() >= dayEnd.getTime()) {
    return null;
  }
  const clipStart = start.getTime() < dayStart.getTime() ? dayStart : start;
  const clipEnd = end.getTime() > dayEnd.getTime() ? dayEnd : end;
  return {
    startMin: clipStart.getHours() * 60 + clipStart.getMinutes(),
    endMin: clipEnd.getHours() * 60 + clipEnd.getMinutes(),
  };
}

export function minutesToCalendarPx(minutes: number, pxPerHour = CALENDAR_PX_PER_HOUR): number {
  return (minutes / 60) * pxPerHour;
}

export function formatClockMinutes(totalMin: number, locale: string): string {
  const hours = Math.floor(totalMin / 60);
  const minutes = totalMin % 60;
  const anchor = new Date(2000, 0, 1, hours, minutes);
  return new Intl.DateTimeFormat(locale, { hour: "numeric", minute: "2-digit" }).format(anchor);
}

export function bookingRangeHoursForDays(
  bookings: readonly CustomerBookingResponse[],
  days: readonly Date[],
  fallbackStart = 8,
  fallbackEnd = 20,
): { start: number; end: number } {
  let minMin = 24 * 60;
  let maxMin = 0;
  for (const day of days) {
    for (const booking of bookings) {
      const clip = clipIntervalToDay(booking.startAt, booking.endAt, day);
      if (!clip) {
        continue;
      }
      minMin = Math.min(minMin, clip.startMin);
      maxMin = Math.max(maxMin, clip.endMin);
    }
  }
  if (maxMin <= minMin) {
    return { start: fallbackStart, end: fallbackEnd };
  }
  const start = Math.max(6, Math.floor(minMin / 60) - 1);
  const end = Math.min(23, Math.ceil(maxMin / 60) + 1);
  return end <= start ? { start: fallbackStart, end: fallbackEnd } : { start, end };
}
