import { useCallback, useEffect, useState } from "react";
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useRouter } from "expo-router";
import type { AdeniApiError } from "@adeni/api-client";
import type { CustomerBookingResponse } from "@adeni/shared";
import { Screen } from "@/components/adeni/Screen";
import { EmptyState } from "@/components/ui/EmptyState";
import { useAuth } from "@/contexts/auth-context";
import { formatBookingStatus, formatSlotTime } from "@/lib/format";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

export default function MyBookingsScreen() {
  const router = useRouter();
  const { loading: authLoading, isBookingEnabled, createApiClient } = useAuth();
  const [bookings, setBookings] = useState<CustomerBookingResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionId, setActionId] = useState<string | null>(null);

  const loadBookings = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = createApiClient("customer");
      const items = await client.getMyBookings();
      setBookings(items);
    } catch (err) {
      const apiError = err as AdeniApiError;
      if (apiError.statusCode === 401) {
        setError("Sign in to view your bookings.");
      } else {
        setError("Could not load bookings. Try again in a moment.");
      }
      setBookings([]);
    } finally {
      setLoading(false);
    }
  }, [createApiClient]);

  useEffect(() => {
    if (authLoading) {
      return;
    }

    if (isBookingEnabled) {
      void loadBookings();
      return;
    }

    setLoading(false);
  }, [authLoading, isBookingEnabled, loadBookings]);

  async function handleCancel(bookingId: string) {
    setActionId(bookingId);
    setError(null);

    try {
      const client = createApiClient("customer");
      const updated = await client.cancelMyBooking(bookingId);
      setBookings((current) =>
        current.map((item) => (item.id === bookingId ? updated : item)),
      );
    } catch {
      setError("Could not cancel this booking.");
    } finally {
      setActionId(null);
    }
  }

  const upcoming = bookings.filter(
    (booking) => booking.status !== 2 && booking.status !== 3,
  );
  const past = bookings.filter(
    (booking) =>
      booking.status === 2 ||
      booking.status === 3 ||
      booking.canReview ||
      booking.hasReview,
  );

  async function handleReview(bookingId: string, rating: number) {
    setActionId(bookingId);
    setError(null);

    try {
      const client = createApiClient("customer");
      await client.createBookingReview(bookingId, { rating });
      await loadBookings();
    } catch {
      setError("Could not submit review.");
    } finally {
      setActionId(null);
    }
  }

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <Pressable onPress={() => router.back()}>
          <Text style={styles.backLink}>← Back</Text>
        </Pressable>

        <Text style={styles.title}>My bookings</Text>
        <Text style={styles.subtitle}>Appointments you have booked on Adeni.</Text>

        {!isBookingEnabled ? (
          <View style={styles.callout}>
            <Text style={styles.calloutTitle}>Sign in required</Text>
            <Text style={styles.calloutBody}>
              {isAuth0Configured()
                ? "Sign in with your customer account to view booking history."
                : "Set EXPO_PUBLIC_DEV_CUSTOMER_AUTH0_SUB in .env for local booking mode, or sign in with Auth0."}
            </Text>
          </View>
        ) : null}

        {error ? <Text style={styles.error}>{error}</Text> : null}

        {isBookingEnabled && bookings.length === 0 && !error ? (
          <EmptyState
            title="No bookings yet"
            description="Discover a business and book your first visit."
            actionLabel="Browse businesses"
            onAction={() => router.push("/(tabs)/discover")}
          />
        ) : null}

        {isBookingEnabled && upcoming.length > 0 ? (
          <>
            <Text style={styles.sectionLabel}>Upcoming ({upcoming.length})</Text>
            <View style={styles.list}>
              {upcoming.map((booking) => (
                <BookingCard
                  key={booking.id}
                  booking={booking}
                  busy={actionId === booking.id}
                  onCancel={() => void handleCancel(booking.id)}
                />
              ))}
            </View>
          </>
        ) : null}

        {isBookingEnabled && past.length > 0 ? (
          <>
            <Text style={styles.sectionLabel}>Past</Text>
            <View style={styles.list}>
              {past.map((booking) => (
                <BookingCard
                  key={booking.id}
                  booking={booking}
                  muted
                  busy={actionId === booking.id}
                  onReview={(rating) => void handleReview(booking.id, rating)}
                />
              ))}
            </View>
          </>
        ) : null}
      </ScrollView>
    </Screen>
  );
}

function BookingCard({
  booking,
  muted = false,
  busy = false,
  onCancel,
  onReview,
}: {
  booking: CustomerBookingResponse;
  muted?: boolean;
  busy?: boolean;
  onCancel?: () => void;
  onReview?: (rating: number) => void;
}) {
  const router = useRouter();
  const canCancel =
    onCancel &&
    (booking.status === 0 || booking.status === 1) &&
    new Date(booking.startAt) > new Date();

  return (
    <View style={[styles.card, muted && styles.cardMuted]}>
      <Text style={styles.serviceName}>{booking.serviceName}</Text>
      <Text style={styles.businessName}>{booking.businessName}</Text>
      <Text style={styles.meta}>{formatSlotTime(booking.startAt)}</Text>
      <Text style={styles.status}>{formatBookingStatus(booking.status)}</Text>
      {booking.customerNotes ? (
        <Text style={styles.notes}>&ldquo;{booking.customerNotes}&rdquo;</Text>
      ) : null}
      {booking.hasReview && booking.reviewRating ? (
        <Text style={styles.notes}>You rated this visit {booking.reviewRating}/5</Text>
      ) : null}
      {booking.canReview && onReview ? (
        <View style={styles.reviewRow}>
          {[1, 2, 3, 4, 5].map((rating) => (
            <Pressable
              key={rating}
              disabled={busy}
              onPress={() => onReview(rating)}
              style={({ pressed }) => [
                styles.reviewButton,
                (busy || pressed) && styles.buttonPressed,
              ]}
            >
              <Text style={styles.reviewButtonText}>{rating}★</Text>
            </Pressable>
          ))}
        </View>
      ) : null}
      {canCancel ? (
        <Pressable
          onPress={onCancel}
          disabled={busy}
          style={({ pressed }) => [
            styles.cancelButton,
            (busy || pressed) && styles.buttonPressed,
          ]}
        >
          <Text style={styles.cancelButtonText}>
            {busy ? "Cancelling…" : "Cancel booking"}
          </Text>
        </Pressable>
      ) : null}
      {booking.businessSlug ? (
        <Pressable
          onPress={() => router.push(`/business/${booking.businessSlug}`)}
          style={({ pressed }) => [styles.linkButton, pressed && styles.buttonPressed]}
        >
          <Text style={styles.linkButtonText}>View business</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  content: {
    paddingHorizontal: 20,
    paddingBottom: 32,
  },
  backLink: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.accent,
    marginBottom: 12,
  },
  title: {
    fontSize: 26,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  subtitle: {
    marginTop: 8,
    fontSize: 15,
    lineHeight: 22,
    color: adeniTheme.textMuted,
  },
  callout: {
    marginTop: 20,
    padding: 16,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    backgroundColor: adeniTheme.surface,
  },
  calloutTitle: {
    fontSize: 16,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  calloutBody: {
    marginTop: 8,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  error: {
    marginTop: 16,
    padding: 10,
    borderRadius: 8,
    backgroundColor: "#fef2f2",
    color: "#991b1b",
    fontSize: 14,
  },
  sectionLabel: {
    marginTop: 24,
    fontSize: 12,
    fontWeight: "700",
    letterSpacing: 0.8,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  list: {
    marginTop: 12,
    gap: 12,
  },
  card: {
    backgroundColor: adeniTheme.surface,
    borderRadius: 14,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    padding: 16,
  },
  cardMuted: {
    opacity: 0.92,
  },
  serviceName: {
    fontSize: 16,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  businessName: {
    marginTop: 4,
    fontSize: 14,
    fontWeight: "500",
    color: adeniTheme.accent,
  },
  meta: {
    marginTop: 6,
    fontSize: 14,
    color: adeniTheme.textMuted,
  },
  status: {
    marginTop: 6,
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.primary,
  },
  notes: {
    marginTop: 10,
    fontSize: 14,
    lineHeight: 20,
    fontStyle: "italic",
    color: adeniTheme.text,
  },
  linkButton: {
    marginTop: 14,
    alignSelf: "flex-start",
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: 999,
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  linkButtonText: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  cancelButton: {
    marginTop: 14,
    alignSelf: "flex-start",
    borderWidth: 1,
    borderColor: "#fecaca",
    borderRadius: 999,
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  cancelButtonText: {
    fontSize: 14,
    fontWeight: "600",
    color: "#991b1b",
  },
  reviewRow: {
    marginTop: 12,
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 8,
  },
  reviewButton: {
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  reviewButtonText: {
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  buttonPressed: {
    opacity: 0.9,
  },
});
