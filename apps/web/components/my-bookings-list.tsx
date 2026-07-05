"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import type { CustomerBookingResponse } from "@adeni/shared";
import { formatBookingStatus } from "@adeni/shared";

function formatSlotTime(iso: string) {
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(iso));
}

export function MyBookingsList() {
  const [bookings, setBookings] = useState<CustomerBookingResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionId, setActionId] = useState<string | null>(null);

  const loadBookings = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch("/api/bookings");
      if (!response.ok) {
        throw new Error("Failed to load bookings");
      }

      const payload = (await response.json()) as { items: CustomerBookingResponse[] };
      setBookings(payload.items ?? []);
    } catch {
      setError("Could not load your bookings. Check your session and API.");
      setBookings([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadBookings();
  }, [loadBookings]);

  async function handleCancel(bookingId: string) {
    setActionId(bookingId);
    setError(null);

    try {
      const response = await fetch(`/api/bookings/${encodeURIComponent(bookingId)}/cancel`, {
        method: "POST",
      });
      if (!response.ok) {
        throw new Error("Cancel failed");
      }
      const updated = (await response.json()) as CustomerBookingResponse;
      setBookings((current) =>
        current.map((item) => (item.id === bookingId ? updated : item)),
      );
    } catch {
      setError("Could not cancel this booking.");
    } finally {
      setActionId(null);
    }
  }

  if (loading) {
    return <p className="text-sm text-[#1b4332]/70">Loading your bookings…</p>;
  }

  if (error) {
    return (
      <p className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">{error}</p>
    );
  }

  if (bookings.length === 0) {
    return (
      <div className="rounded-xl border border-[#1b4332]/10 bg-white p-8 text-center shadow-sm">
        <p className="font-medium text-[#1b4332]">No bookings yet</p>
        <p className="mt-2 text-sm text-[#1b4332]/70">
          Browse businesses and book your first appointment.
        </p>
        <Link
          href="/discover"
          className="mt-5 inline-block rounded-full bg-[#1b4332] px-5 py-2.5 text-sm font-medium text-white hover:opacity-90"
        >
          Discover businesses
        </Link>
      </div>
    );
  }

  const upcoming = bookings.filter(
    (booking) => booking.status !== 2 && booking.status !== 3,
  );
  const past = bookings.filter(
    (booking) => booking.status === 2 || booking.status === 3,
  );

  return (
    <div className="space-y-8">
      {upcoming.length > 0 ? (
        <section>
          <h2 className="text-lg font-semibold">Upcoming ({upcoming.length})</h2>
          <ul className="mt-4 space-y-3">
            {upcoming.map((booking) => (
              <BookingCard
                key={booking.id}
                booking={booking}
                busy={actionId === booking.id}
                onCancel={() => void handleCancel(booking.id)}
              />
            ))}
          </ul>
        </section>
      ) : null}

      {past.length > 0 ? (
        <section>
          <h2 className="text-lg font-semibold">Past</h2>
          <ul className="mt-4 divide-y divide-[#1b4332]/10 rounded-xl border border-[#1b4332]/10 bg-white">
            {past.map((booking) => (
              <li key={booking.id} className="px-5 py-4">
                <BookingCard booking={booking} compact />
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}

function BookingCard({
  booking,
  compact = false,
  busy = false,
  onCancel,
}: {
  booking: CustomerBookingResponse;
  compact?: boolean;
  busy?: boolean;
  onCancel?: () => void;
}) {
  const businessHref = booking.businessSlug
    ? `/businesses/${booking.businessSlug}`
    : null;
  const canCancel =
    !compact &&
    onCancel &&
    (booking.status === 0 || booking.status === 1) &&
    new Date(booking.startAt) > new Date();

  const content = (
    <>
      <div className="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p className="font-semibold">{booking.serviceName}</p>
          {businessHref ? (
            <Link
              href={businessHref}
              className="text-sm font-medium text-[#40916c] hover:underline"
            >
              {booking.businessName}
            </Link>
          ) : (
            <p className="text-sm text-[#1b4332]/70">{booking.businessName}</p>
          )}
          <p className="mt-1 text-sm text-[#1b4332]/70">
            {formatSlotTime(booking.startAt)}
          </p>
          {booking.customerNotes ? (
            <p className="mt-2 text-sm italic text-[#1b4332]/80">
              &ldquo;{booking.customerNotes}&rdquo;
            </p>
          ) : null}
          {canCancel ? (
            <button
              type="button"
              disabled={busy}
              onClick={onCancel}
              className="mt-4 rounded-full border border-[#1b4332]/20 px-4 py-2 text-sm font-medium hover:bg-[#f6f8f6] disabled:opacity-60"
            >
              {busy ? "Cancelling…" : "Cancel booking"}
            </button>
          ) : null}
        </div>
        <span className="mt-2 shrink-0 text-sm font-medium text-[#40916c] sm:mt-0">
          {formatBookingStatus(booking.status)}
        </span>
      </div>
    </>
  );

  if (compact) {
    return content;
  }

  return (
    <li className="rounded-xl border border-[#1b4332]/10 bg-white p-5 shadow-sm">
      {content}
    </li>
  );
}
