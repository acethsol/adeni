"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { CustomerBookingResponse } from "@adeni/shared";
import { formatBookingStatus, queryKeys, staleTimes } from "@adeni/shared";
import { BookingReviewForm, BookingReviewSummary } from "@/components/booking-review-form";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Callout } from "@/components/ui/callout";
import { EmptyState } from "@/components/ui/empty-state";
import { SkeletonList } from "@/components/ui/skeleton";

function formatSlotTime(iso: string) {
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(iso));
}

async function fetchBookings() {
  const response = await fetch("/api/bookings");
  if (!response.ok) {
    throw new Error("Failed to load bookings");
  }

  const payload = (await response.json()) as { items: CustomerBookingResponse[] };
  return payload.items ?? [];
}

export function MyBookingsList() {
  const queryClient = useQueryClient();

  const { data: bookings = [], isLoading, error } = useQuery({
    queryKey: queryKeys.myBookings,
    queryFn: fetchBookings,
    staleTime: staleTimes.bookings,
  });

  const cancelMutation = useMutation({
    mutationFn: async (bookingId: string) => {
      const response = await fetch(`/api/bookings/${encodeURIComponent(bookingId)}/cancel`, {
        method: "POST",
      });
      if (!response.ok) {
        throw new Error("Cancel failed");
      }
      return (await response.json()) as CustomerBookingResponse;
    },
    onSuccess: (updated) => {
      queryClient.setQueryData<CustomerBookingResponse[]>(queryKeys.myBookings, (current = []) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      );
    },
  });

  if (isLoading) {
    return <SkeletonList count={2} />;
  }

  if (error) {
    return (
      <Callout tone="error">
        Could not load your bookings. Check your session and API.
      </Callout>
    );
  }

  if (bookings.length === 0) {
    return (
      <EmptyState
        title="No bookings yet"
        description="Browse businesses and book your first appointment."
        actionLabel="Discover businesses"
        actionHref="/discover"
      />
    );
  }

  const upcoming = bookings.filter(
    (booking) => booking.status !== 2 && booking.status !== 3,
  );
  const past = bookings.filter(
    (booking) => booking.status === 2 || booking.status === 3 || booking.canReview || booking.hasReview,
  );

  return (
    <div className="space-y-8">
      {cancelMutation.isError ? (
        <Callout tone="error">Could not cancel this booking.</Callout>
      ) : null}

      {upcoming.length > 0 ? (
        <section>
          <h2 className="text-lg font-semibold">Upcoming ({upcoming.length})</h2>
          <ul className="mt-4 space-y-3">
            {upcoming.map((booking) => (
              <BookingCard
                key={booking.id}
                booking={booking}
                busy={cancelMutation.isPending && cancelMutation.variables === booking.id}
                onCancel={() => cancelMutation.mutate(booking.id)}
              />
            ))}
          </ul>
        </section>
      ) : null}

      {past.length > 0 ? (
        <section>
          <h2 className="text-lg font-semibold">Past</h2>
          <Card padding="sm" className="mt-4 divide-y divide-border overflow-hidden p-0">
            {past.map((booking) => (
              <div key={booking.id} className="px-5 py-4">
                <BookingCard booking={booking} compact />
                {booking.hasReview && booking.reviewRating ? (
                  <BookingReviewSummary rating={booking.reviewRating} />
                ) : null}
                {booking.canReview ? (
                  <BookingReviewForm
                    booking={booking}
                    onSubmitted={() => void queryClient.invalidateQueries({ queryKey: queryKeys.myBookings })}
                  />
                ) : null}
              </div>
            ))}
          </Card>
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
    <div className="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <p className="font-semibold">{booking.serviceName}</p>
        {businessHref ? (
          <Link href={businessHref} className="text-sm font-semibold text-accent hover:underline">
            {booking.businessName}
          </Link>
        ) : (
          <p className="text-sm text-muted">{booking.businessName}</p>
        )}
        <p className="mt-1 text-sm text-muted">{formatSlotTime(booking.startAt)}</p>
        {booking.customerNotes ? (
          <p className="mt-2 text-sm italic text-muted">&ldquo;{booking.customerNotes}&rdquo;</p>
        ) : null}
        {canCancel ? (
          <Button
            variant="secondary"
            size="sm"
            className="mt-4"
            disabled={busy}
            onClick={onCancel}
          >
            {busy ? "Cancelling…" : "Cancel booking"}
          </Button>
        ) : null}
      </div>
      <Badge tone="accent" className="mt-2 sm:mt-0">
        {formatBookingStatus(booking.status)}
      </Badge>
    </div>
  );

  if (compact) {
    return content;
  }

  return (
    <li>
      <Card padding="md">{content}</Card>
    </li>
  );
}
