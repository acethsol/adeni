import { bookingPreselectionQuery, type CustomerBookingResponse } from "@adeni/shared";

/** Booking status codes from the API: 0 pending, 1 confirmed, 2 rejected, 3 cancelled. */
const PENDING = 0;
const CONFIRMED = 1;

function startMs(booking: CustomerBookingResponse): number {
  return new Date(booking.startAt).getTime();
}

/** Active upcoming appointments (pending or confirmed, not yet started), soonest first. */
export function pickUpcomingBookings(
  bookings: readonly CustomerBookingResponse[],
  now: number,
): CustomerBookingResponse[] {
  return bookings
    .filter((b) => (b.status === PENDING || b.status === CONFIRMED) && startMs(b) >= now)
    .sort((a, b) => startMs(a) - startMs(b));
}

export function pickNextBooking(
  bookings: readonly CustomerBookingResponse[],
  now: number,
): CustomerBookingResponse | null {
  return pickUpcomingBookings(bookings, now)[0] ?? null;
}

/** Most recent confirmed visit that already happened — the basis for "Book again". */
export function pickBookAgainSource(
  bookings: readonly CustomerBookingResponse[],
  now: number,
): CustomerBookingResponse | null {
  return (
    bookings
      .filter((b) => b.status === CONFIRMED && startMs(b) < now && !!b.businessSlug)
      .sort((a, b) => startMs(b) - startMs(a))[0] ?? null
  );
}

export type BookAgainLink = {
  commands: string[];
  queryParams: Record<string, string>;
  fragment: "book";
};

/**
 * Deep link to the canonical public booking page with F4 service/practitioner preselection.
 * Selectors are validated by the shared helper; an invalid id degrades to the plain business page.
 */
export function bookAgainLink(booking: CustomerBookingResponse): BookAgainLink {
  let queryParams: Record<string, string> = {};
  try {
    const query = bookingPreselectionQuery({
      service: booking.serviceOfferingId,
      practitioner: booking.staffMemberId ?? undefined,
    });
    queryParams = Object.fromEntries(new URLSearchParams(query));
  } catch {
    queryParams = {};
  }

  return { commands: ["/businesses", booking.businessSlug], queryParams, fragment: "book" };
}
