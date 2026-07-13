"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { CustomerBookingResponse } from "@adeni/shared";
import { formatBookingStatusLabel, queryKeys, staleTimes } from "@adeni/shared";
import { BookingReviewForm, BookingReviewSummary } from "@/components/booking-review-form";
import { TranslatedText } from "@/components/translated-text";
import { useTranslation } from "@/components/locale-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Callout } from "@/components/ui/callout";
import { EmptyState } from "@/components/ui/empty-state";
import { SkeletonList } from "@/components/ui/skeleton";

function formatSlotTime(iso: string, locale: string) {
  return new Intl.DateTimeFormat(locale, {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(iso));
}

function isUpcomingBooking(booking: CustomerBookingResponse): boolean {
  if (booking.status === 2 || booking.status === 3) {
    return false;
  }

  return new Date(booking.endAt).getTime() >= Date.now();
}

function partitionBookings(bookings: CustomerBookingResponse[]) {
  const upcoming = bookings.filter(isUpcomingBooking);
  const upcomingIds = new Set(upcoming.map((booking) => booking.id));
  const past = bookings.filter((booking) => !upcomingIds.has(booking.id));
  return { upcoming, past };
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
  const { locale, t } = useTranslation();

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
    return <Callout tone="error">{t("bookings.loadError")}</Callout>;
  }

  if (bookings.length === 0) {
    return (
      <EmptyState
        title={t("bookings.emptyTitle")}
        description={t("bookings.emptyDescription")}
        actionLabel={t("bookings.discoverBusinesses")}
        actionHref="/discover"
      />
    );
  }

  const { upcoming, past } = partitionBookings(bookings);

  return (
    <div className="space-y-10">
      {cancelMutation.isError ? (
        <Callout tone="error">{t("bookings.cancelError")}</Callout>
      ) : null}

      {upcoming.length > 0 ? (
        <section>
          <h2 className="text-lg font-semibold text-foreground">
            {t("bookings.upcoming", { count: upcoming.length })}
          </h2>
          <ul className="mt-4 space-y-4">
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
          <h2 className="text-lg font-semibold text-foreground">
            {t("bookings.past", { count: past.length })}
          </h2>
          <ul className="mt-4 space-y-4">
            {past.map((booking) => (
              <li key={booking.id}>
                <Card padding="md">
                  <BookingCardContent booking={booking} />
                  {booking.hasReview && booking.reviewRating ? (
                    <BookingReviewSummary rating={booking.reviewRating} />
                  ) : null}
                  {booking.canReview ? (
                    <BookingReviewForm
                      booking={booking}
                      onSubmitted={() =>
                        void queryClient.invalidateQueries({ queryKey: queryKeys.myBookings })
                      }
                    />
                  ) : null}
                </Card>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}

function BookingCardContent({
  booking,
  showCancel = false,
  busy = false,
  onCancel,
}: {
  booking: CustomerBookingResponse;
  showCancel?: boolean;
  busy?: boolean;
  onCancel?: () => void;
}) {
  const { locale, t } = useTranslation();
  const businessHref = booking.businessSlug
    ? `/businesses/${booking.businessSlug}`
    : null;
  const canCancel =
    showCancel &&
    onCancel &&
    (booking.status === 0 || booking.status === 1) &&
    new Date(booking.startAt) > new Date();

  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
      <div className="min-w-0 flex-1">
        <p className="text-lg font-semibold text-foreground">
          <TranslatedText text={booking.serviceName} />
        </p>
        {businessHref ? (
          <Link href={businessHref} className="text-sm font-semibold text-accent hover:underline">
            {booking.businessName}
          </Link>
        ) : (
          <p className="text-sm text-muted">{booking.businessName}</p>
        )}
        <p className="mt-2 text-sm text-muted">{formatSlotTime(booking.startAt, locale)}</p>
        {booking.customerNotes ? (
          <p className="mt-3 text-sm italic text-muted">
            &ldquo;
            <TranslatedText text={booking.customerNotes} showBadge />
            &rdquo;
          </p>
        ) : null}
        {canCancel ? (
          <Button
            variant="secondary"
            size="sm"
            className="mt-4"
            disabled={busy}
            onClick={onCancel}
          >
            {busy ? t("bookings.cancelling") : t("bookings.cancelBooking")}
          </Button>
        ) : null}
      </div>
      <Badge tone="accent" className="self-start">
        {formatBookingStatusLabel(locale, booking.status)}
      </Badge>
    </div>
  );
}

function BookingCard({
  booking,
  busy = false,
  onCancel,
}: {
  booking: CustomerBookingResponse;
  busy?: boolean;
  onCancel?: () => void;
}) {
  return (
    <li>
      <Card padding="md">
        <BookingCardContent
          booking={booking}
          showCancel
          busy={busy}
          onCancel={onCancel}
        />
      </Card>
    </li>
  );
}
