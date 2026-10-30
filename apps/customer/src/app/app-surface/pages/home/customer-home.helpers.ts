import {
  bookingPreselectionQuery,
  type BusinessGiftCard,
  type ConversationSummary,
  type CustomerBookingResponse,
  type CustomerPackage,
} from "@adeni/shared";

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

/** Most recent completed booking the customer can still review. */
export function pickReviewCandidate(
  bookings: readonly CustomerBookingResponse[],
  now: number,
): CustomerBookingResponse | null {
  return (
    bookings
      .filter(
        (b) =>
          b.canReview === true
          && b.hasReview !== true
          && startMs(b) < now
          && !!b.businessSlug,
      )
      .sort((a, b) => startMs(b) - startMs(a))[0] ?? null
  );
}

/** Top conversations needing attention, then by recency. */
export function pickHomeConversations(
  rows: readonly ConversationSummary[],
  limit = 2,
): ConversationSummary[] {
  return [...rows]
    .sort((a, b) => {
      if (a.needsAttention !== b.needsAttention) {
        return a.needsAttention ? -1 : 1;
      }
      return new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime();
    })
    .slice(0, Math.max(0, limit));
}

export type HomeBenefitsSnapshot = {
  activePackages: number;
  creditsRemaining: number;
  soonestExpiry: string | null;
  giftBalancesByCurrency: { currency: string; amount: number }[];
};

/** Session credits + gift-card balances for the Home benefits card (never sum currencies). */
export function summarizeBenefits(
  packages: readonly CustomerPackage[],
  giftCards: readonly BusinessGiftCard[],
): HomeBenefitsSnapshot {
  const active = packages.filter((p) => p.status === "active");
  let creditsRemaining = 0;
  let soonestExpiry: string | null = null;
  for (const pkg of active) {
    for (const balance of pkg.balances) {
      creditsRemaining += balance.remaining;
      if (balance.expiresAt) {
        if (!soonestExpiry || balance.expiresAt < soonestExpiry) {
          soonestExpiry = balance.expiresAt;
        }
      }
    }
    if (pkg.expiresAt && (!soonestExpiry || pkg.expiresAt < soonestExpiry)) {
      soonestExpiry = pkg.expiresAt;
    }
  }

  const byCurrency = new Map<string, number>();
  for (const card of giftCards) {
    if (card.state !== "Active" || card.remainingAmount <= 0) {
      continue;
    }
    byCurrency.set(card.currency, (byCurrency.get(card.currency) ?? 0) + card.remainingAmount);
  }

  return {
    activePackages: active.length,
    creditsRemaining,
    soonestExpiry,
    giftBalancesByCurrency: [...byCurrency.entries()]
      .map(([currency, amount]) => ({ currency, amount }))
      .sort((a, b) => a.currency.localeCompare(b.currency)),
  };
}

export function hasBenefitsSnapshot(snapshot: HomeBenefitsSnapshot): boolean {
  return (
    snapshot.activePackages > 0
    || snapshot.creditsRemaining > 0
    || snapshot.giftBalancesByCurrency.length > 0
  );
}
