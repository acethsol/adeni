import { describe, expect, it } from "vitest";
import type { CustomerBookingResponse } from "@adeni/shared";
import {
  bookingCountByDay,
  bookingsOnDay,
  dayKey,
  monthGridDays,
  weekGridDays,
} from "./customer-calendar.helpers";

function booking(partial: Partial<CustomerBookingResponse>): CustomerBookingResponse {
  return {
    id: "b1",
    tenantId: "t1",
    businessName: "Cuts",
    businessSlug: "cuts",
    serviceOfferingId: "s1",
    serviceName: "Fade",
    startAt: "2026-06-15T10:00:00.000Z",
    endAt: "2026-06-15T10:30:00.000Z",
    status: 1,
    createdAt: "2026-06-01T09:00:00.000Z",
    guestCount: 1,
    ...partial,
  };
}

describe("customer-calendar helpers (C2)", () => {
  it("builds a 6x7 month grid starting on Sunday", () => {
    const days = monthGridDays(new Date(2026, 5, 15), 0);
    expect(days).toHaveLength(42);
    expect(days[0]!.getDay()).toBe(0);
  });

  it("builds a 7-day week grid", () => {
    const days = weekGridDays(new Date(2026, 5, 17), 0);
    expect(days).toHaveLength(7);
    expect(dayKey(days[0]!)).toBe("2026-06-14");
  });

  it("groups bookings by local day key", () => {
    const rows = [
      booking({ id: "a", startAt: "2026-06-15T10:00:00.000Z" }),
      booking({ id: "b", startAt: "2026-06-15T14:00:00.000Z" }),
      booking({ id: "c", startAt: "2026-06-16T10:00:00.000Z" }),
    ];
    const counts = bookingCountByDay(rows);
    expect(counts.get(dayKeyFromLocal(rows[0]!.startAt))).toBe(2);
    expect(bookingsOnDay(rows, dayKeyFromLocal(rows[2]!.startAt))).toHaveLength(1);
  });
});

function dayKeyFromLocal(iso: string): string {
  return dayKey(new Date(iso));
}
