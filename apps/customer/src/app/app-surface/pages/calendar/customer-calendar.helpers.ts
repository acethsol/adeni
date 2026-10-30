import type { CustomerBookingResponse } from "@adeni/shared";

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

export function startOfMonth(anchor: Date): Date {
  return new Date(anchor.getFullYear(), anchor.getMonth(), 1);
}

export function addMonths(anchor: Date, delta: number): Date {
  return new Date(anchor.getFullYear(), anchor.getMonth() + delta, 1);
}

export function startOfWeek(anchor: Date, weekStartsOn = 0): Date {
  const date = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate());
  const day = date.getDay();
  const diff = (day - weekStartsOn + 7) % 7;
  date.setDate(date.getDate() - diff);
  return date;
}

export function addDays(anchor: Date, delta: number): Date {
  const date = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate());
  date.setDate(date.getDate() + delta);
  return date;
}

/** 42 cells (6 weeks) covering the month that contains `anchor`. */
export function monthGridDays(anchor: Date, weekStartsOn = 0): Date[] {
  const first = startOfMonth(anchor);
  const gridStart = startOfWeek(first, weekStartsOn);
  return Array.from({ length: 42 }, (_, i) => addDays(gridStart, i));
}

/** 7 days for the week that contains `anchor`. */
export function weekGridDays(anchor: Date, weekStartsOn = 0): Date[] {
  const start = startOfWeek(anchor, weekStartsOn);
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
