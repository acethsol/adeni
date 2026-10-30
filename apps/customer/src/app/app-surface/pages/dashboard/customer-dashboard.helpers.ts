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

/** Past visits, most recent first — fills the dashboard when nothing is upcoming. */
export function pickRecentPastBookings(
  bookings: readonly CustomerBookingResponse[],
  now: number,
  limit = 3,
): CustomerBookingResponse[] {
  return bookings
    .filter((b) => startMs(b) < now)
    .sort((a, b) => startMs(b) - startMs(a))
    .slice(0, Math.max(0, limit));
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

export const BENEFITS_EXPIRY_WARN_DAYS = 14;

export function isBookingOnLocalDay(startAtIso: string, nowMs: number): boolean {
  const start = new Date(startAtIso);
  const now = new Date(nowMs);
  return (
    start.getFullYear() === now.getFullYear()
    && start.getMonth() === now.getMonth()
    && start.getDate() === now.getDate()
  );
}

export function countTodayUpcoming(
  bookings: readonly CustomerBookingResponse[],
  nowMs: number,
): number {
  return pickUpcomingBookings(bookings, nowMs).filter((b) => isBookingOnLocalDay(b.startAt, nowMs)).length;
}

export function pickTodayNextBooking(
  bookings: readonly CustomerBookingResponse[],
  nowMs: number,
): CustomerBookingResponse | null {
  return (
    pickUpcomingBookings(bookings, nowMs).find((b) => isBookingOnLocalDay(b.startAt, nowMs)) ?? null
  );
}

/** Whole minutes until start; null if already started or in the past. */
export function minutesUntilStart(startAtIso: string, nowMs: number): number | null {
  const diffMs = new Date(startAtIso).getTime() - nowMs;
  if (diffMs <= 0) {
    return null;
  }
  return Math.ceil(diffMs / 60_000);
}

export function formatDurationMinutes(totalMinutes: number): string {
  if (totalMinutes < 60) {
    return `${totalMinutes} min`;
  }
  const hours = Math.floor(totalMinutes / 60);
  const mins = totalMinutes % 60;
  return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
}

export function benefitsExpiryWithinDays(
  soonestExpiry: string | null,
  nowMs: number,
  withinDays: number,
): boolean {
  if (!soonestExpiry) {
    return false;
  }
  const expiryMs = new Date(soonestExpiry).getTime();
  const horizonMs = nowMs + withinDays * 86_400_000;
  return expiryMs > nowMs && expiryMs <= horizonMs;
}

export type DashboardActionKind = "link" | "review";

export type DashboardAction = {
  id: string;
  kind: DashboardActionKind;
  labelKey: string;
  icon: "chat" | "edit" | "gift" | "document";
  route?: string[];
  queryParams?: Record<string, string>;
};

export function buildDashboardActions(input: {
  unreadMessages: number;
  review: CustomerBookingResponse | null;
  benefitsExpiringSoon: boolean;
  receiptCount: number;
}): DashboardAction[] {
  const actions: DashboardAction[] = [];
  if (input.unreadMessages > 0) {
    actions.push({
      id: "messages",
      kind: "link",
      labelKey: "customer.app.dashboard.actionReplyMessages",
      icon: "chat",
      route: ["/app/messages"],
    });
  }
  if (input.review) {
    actions.push({
      id: "review",
      kind: "review",
      labelKey: "customer.app.dashboard.leaveReview",
      icon: "edit",
    });
  }
  if (input.benefitsExpiringSoon) {
    actions.push({
      id: "benefits",
      kind: "link",
      labelKey: "customer.app.dashboard.actionUseBenefits",
      icon: "gift",
      route: ["/app/benefits"],
    });
  }
  if (input.receiptCount > 0) {
    actions.push({
      id: "receipts",
      kind: "link",
      labelKey: "customer.app.dashboard.actionViewReceipts",
      icon: "document",
      route: ["/app/receipts"],
    });
  }
  return actions;
}
