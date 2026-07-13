"use client";

import { useCallback, useEffect, useState } from "react";
import { CalendarClock } from "lucide-react";
import type { BookingResponse } from "@adeni/shared";
import { formatBookingStatus } from "@adeni/shared";
import { EmptyState } from "@/components/ui/empty-state";
import { SkeletonList } from "@/components/ui/skeleton";
import { useActionLoading } from "@/contexts/action-loading-context";
import { useConfirm } from "@/contexts/confirm-context";
import { useToast } from "@/contexts/toast-context";

const PENDING_STATUS = 0;

function formatSlotTime(iso: string) {
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(iso));
}

export function BusinessBookingInbox() {
  const { run } = useActionLoading();
  const confirm = useConfirm();
  const toast = useToast();
  const [bookings, setBookings] = useState<BookingResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionId, setActionId] = useState<string | null>(null);

  const loadBookings = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch("/api/business/bookings");
      if (!response.ok) {
        throw new Error("Failed to load bookings");
      }

      const payload = (await response.json()) as { items: BookingResponse[] };
      setBookings(payload.items ?? []);
    } catch {
      setError("Could not load bookings. Check your business session and API.");
      setBookings([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadBookings();
  }, [loadBookings]);

  async function handleAction(booking: BookingResponse, action: "accept" | "reject") {
    if (action === "reject") {
      const confirmed = await confirm({
        title: "Reject this booking?",
        description: `${booking.serviceName} on ${formatSlotTime(booking.startAt)} will be cancelled and the customer notified.`,
        confirmLabel: "Reject booking",
        tone: "destructive",
      });
      if (!confirmed) {
        return;
      }
    }

    setActionId(booking.id);
    setError(null);

    try {
      await run(action === "accept" ? "Accepting booking…" : "Rejecting booking…", async () => {
        const response = await fetch(`/api/business/bookings/${booking.id}/${action}`, {
          method: "POST",
          headers: action === "reject" ? { "Content-Type": "application/json" } : undefined,
          body: action === "reject" ? JSON.stringify({ reason: null }) : undefined,
        });

        if (!response.ok) {
          throw new Error(`Failed to ${action} booking`);
        }

        const updated = (await response.json()) as BookingResponse;
        setBookings((current) =>
          current.map((item) => (item.id === booking.id ? updated : item)),
        );
      });
      toast.success(action === "accept" ? "Booking accepted" : "Booking rejected", {
        description: booking.serviceName,
      });
    } catch {
      toast.error(`Could not ${action} this booking.`);
    } finally {
      setActionId(null);
    }
  }

  const pending = bookings.filter((booking) => booking.status === PENDING_STATUS);
  const recent = bookings.filter((booking) => booking.status !== PENDING_STATUS);

  if (loading) {
    return <SkeletonList count={3} />;
  }

  return (
    <div className="space-y-8">
      {error ? (
        <p className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">{error}</p>
      ) : null}

      <section>
        <h2 className="text-lg font-semibold">Pending ({pending.length})</h2>
        {pending.length === 0 ? (
          <EmptyState
            className="mt-3"
            icon={<CalendarClock className="h-6 w-6" aria-hidden />}
            title="No pending bookings"
            description="New booking requests will show up here for you to accept or reject."
          />
        ) : (
          <ul className="mt-4 space-y-3">
            {pending.map((booking) => (
              <li
                key={booking.id}
                className="rounded-xl border border-[#1b4332]/10 bg-white p-5 shadow-sm"
              >
                <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <p className="font-semibold">{booking.serviceName}</p>
                    <p className="mt-1 text-sm text-[#1b4332]/70">
                      {formatSlotTime(booking.startAt)}
                    </p>
                    {booking.customerNotes ? (
                      <p className="mt-2 text-sm italic text-[#1b4332]/80">
                        "{booking.customerNotes}"
                      </p>
                    ) : null}
                  </div>
                  <div className="flex shrink-0 gap-2">
                    <button
                      type="button"
                      disabled={actionId === booking.id}
                      onClick={() => void handleAction(booking, "reject")}
                      className="rounded-full border border-[#1b4332]/20 px-4 py-2 text-sm font-medium hover:bg-[#f6f8f6] disabled:opacity-60"
                    >
                      Reject
                    </button>
                    <button
                      type="button"
                      disabled={actionId === booking.id}
                      onClick={() => void handleAction(booking, "accept")}
                      className="rounded-full bg-[#1b4332] px-4 py-2 text-sm font-medium text-white hover:opacity-90 disabled:opacity-60"
                    >
                      {actionId === booking.id ? "Saving…" : "Accept"}
                    </button>
                  </div>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      {recent.length > 0 ? (
        <section>
          <h2 className="text-lg font-semibold">Recent</h2>
          <ul className="mt-4 divide-y divide-[#1b4332]/10 rounded-xl border border-[#1b4332]/10 bg-white">
            {recent.map((booking) => (
              <li key={booking.id} className="flex items-center justify-between px-5 py-4">
                <div>
                  <p className="font-medium">{booking.serviceName}</p>
                  <p className="text-sm text-[#1b4332]/60">{formatSlotTime(booking.startAt)}</p>
                </div>
                <span className="text-sm font-medium text-[#40916c]">
                  {formatBookingStatus(booking.status)}
                </span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}
