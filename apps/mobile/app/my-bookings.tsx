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

  const upcoming = bookings.filter(
    (booking) => booking.status !== 2 && booking.status !== 3,
  );
  const past = bookings.filter(
    (booking) => booking.status === 2 || booking.status === 3,
  );

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
          <Text style={styles.empty}>No bookings yet. Discover a business and book your first visit.</Text>
        ) : null}

        {isBookingEnabled && upcoming.length > 0 ? (
          <>
            <Text style={styles.sectionLabel}>Upcoming ({upcoming.length})</Text>
            <View style={styles.list}>
              {upcoming.map((booking) => (
                <BookingCard key={booking.id} booking={booking} />
              ))}
            </View>
          </>
        ) : null}

        {isBookingEnabled && past.length > 0 ? (
          <>
            <Text style={styles.sectionLabel}>Past</Text>
            <View style={styles.list}>
              {past.map((booking) => (
                <BookingCard key={booking.id} booking={booking} muted />
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
}: {
  booking: CustomerBookingResponse;
  muted?: boolean;
}) {
  const router = useRouter();

  return (
    <View style={[styles.card, muted && styles.cardMuted]}>
      <Text style={styles.serviceName}>{booking.serviceName}</Text>
      <Text style={styles.businessName}>{booking.businessName}</Text>
      <Text style={styles.meta}>{formatSlotTime(booking.startAt)}</Text>
      <Text style={styles.status}>{formatBookingStatus(booking.status)}</Text>
      {booking.customerNotes ? (
        <Text style={styles.notes}>&ldquo;{booking.customerNotes}&rdquo;</Text>
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
  empty: {
    marginTop: 20,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
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
  buttonPressed: {
    opacity: 0.9,
  },
});
