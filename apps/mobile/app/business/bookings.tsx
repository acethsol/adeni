import { useCallback, useEffect, useState } from "react";
import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useRouter } from "expo-router";
import type { AdeniApiError } from "@adeni/api-client";
import type { BookingResponse } from "@adeni/shared";
import { Screen } from "@/components/adeni/Screen";
import { useAuth } from "@/contexts/auth-context";
import { formatBookingStatus, formatSlotTime } from "@/lib/format";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

const PENDING_STATUS = 0;

export default function BusinessBookingsScreen() {
  const router = useRouter();
  const {
    loading: authLoading,
    isBusinessInboxEnabled,
    createBusinessApiClient,
    refreshSession,
  } = useAuth();
  const [bookings, setBookings] = useState<BookingResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionId, setActionId] = useState<string | null>(null);

  const loadBookings = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const items = await client.getTenantBookings();
      setBookings(items);
    } catch (err) {
      const apiError = err as AdeniApiError;
      if (apiError.statusCode === 401) {
        setError("Sign in with a business account to manage bookings.");
      } else {
        setError("Could not load bookings. Try again in a moment.");
      }
      setBookings([]);
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient]);

  useEffect(() => {
    if (authLoading) {
      return;
    }

    if (isBusinessInboxEnabled) {
      void loadBookings();
      return;
    }

    setLoading(false);
  }, [authLoading, isBusinessInboxEnabled, loadBookings]);

  async function handleAccept(bookingId: string) {
    setActionId(bookingId);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const updated = await client.acceptTenantBooking(bookingId);
      setBookings((current) =>
        current.map((item) => (item.id === bookingId ? updated : item)),
      );
    } catch {
      setError("Could not accept this booking.");
    } finally {
      setActionId(null);
    }
  }

  async function handleReject(bookingId: string) {
    setActionId(bookingId);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const updated = await client.rejectTenantBooking(bookingId);
      setBookings((current) =>
        current.map((item) => (item.id === bookingId ? updated : item)),
      );
    } catch {
      setError("Could not reject this booking.");
    } finally {
      setActionId(null);
    }
  }

  const pending = bookings.filter((booking) => booking.status === PENDING_STATUS);

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <Pressable onPress={() => router.back()}>
          <Text style={styles.backLink}>← Back</Text>
        </Pressable>

        <Text style={styles.title}>Booking inbox</Text>
        <Text style={styles.subtitle}>
          Review and respond to customer booking requests.
        </Text>

        {!isBusinessInboxEnabled ? (
          <View style={styles.callout}>
            <Text style={styles.calloutTitle}>Business access required</Text>
            <Text style={styles.calloutBody}>
              {isAuth0Configured()
                ? "Sign in with a business account linked to your tenant."
                : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB in .env for local business mode, or sign in with Auth0."}
            </Text>
            {isAuth0Configured() ? (
              <Pressable
                style={styles.secondaryButton}
                onPress={() => void refreshSession().then(() => loadBookings())}
              >
                <Text style={styles.secondaryButtonText}>Retry</Text>
              </Pressable>
            ) : null}
          </View>
        ) : null}

        {error ? <Text style={styles.error}>{error}</Text> : null}

        {isBusinessInboxEnabled ? (
          <>
            <Text style={styles.sectionLabel}>
              Pending ({pending.length})
            </Text>

            {pending.length === 0 ? (
              <Text style={styles.empty}>No pending bookings right now.</Text>
            ) : (
              <View style={styles.list}>
                {pending.map((booking) => (
                  <BookingCard
                    key={booking.id}
                    booking={booking}
                    busy={actionId === booking.id}
                    onAccept={() => void handleAccept(booking.id)}
                    onReject={() => void handleReject(booking.id)}
                  />
                ))}
              </View>
            )}

            {bookings.length > pending.length ? (
              <>
                <Text style={styles.sectionLabel}>Recent</Text>
                <View style={styles.list}>
                  {bookings
                    .filter((booking) => booking.status !== PENDING_STATUS)
                    .map((booking) => (
                      <View key={booking.id} style={styles.historyCard}>
                        <Text style={styles.serviceName}>{booking.serviceName}</Text>
                        <Text style={styles.meta}>{formatSlotTime(booking.startAt)}</Text>
                        <Text style={styles.status}>{formatBookingStatus(booking.status)}</Text>
                      </View>
                    ))}
                </View>
              </>
            ) : null}
          </>
        ) : null}
      </ScrollView>
    </Screen>
  );
}

function BookingCard({
  booking,
  busy,
  onAccept,
  onReject,
}: {
  booking: BookingResponse;
  busy: boolean;
  onAccept: () => void;
  onReject: () => void;
}) {
  return (
    <View style={styles.card}>
      <Text style={styles.serviceName}>{booking.serviceName}</Text>
      <Text style={styles.meta}>{formatSlotTime(booking.startAt)}</Text>
      {booking.customerNotes ? (
        <Text style={styles.notes}>"{booking.customerNotes}"</Text>
      ) : null}

      <View style={styles.actions}>
        <Pressable
          onPress={onReject}
          disabled={busy}
          style={({ pressed }) => [
            styles.rejectButton,
            (busy || pressed) && styles.buttonDisabled,
          ]}
        >
          <Text style={styles.rejectButtonText}>Reject</Text>
        </Pressable>
        <Pressable
          onPress={onAccept}
          disabled={busy}
          style={({ pressed }) => [
            styles.acceptButton,
            (busy || pressed) && styles.buttonDisabled,
          ]}
        >
          {busy ? (
            <ActivityIndicator color="#ffffff" size="small" />
          ) : (
            <Text style={styles.acceptButtonText}>Accept</Text>
          )}
        </Pressable>
      </View>
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
  secondaryButton: {
    marginTop: 14,
    alignSelf: "flex-start",
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: 999,
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  secondaryButtonText: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.text,
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
  empty: {
    marginTop: 12,
    fontSize: 14,
    color: adeniTheme.textMuted,
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
  historyCard: {
    backgroundColor: adeniTheme.surface,
    borderRadius: 14,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    padding: 14,
  },
  serviceName: {
    fontSize: 16,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  meta: {
    marginTop: 6,
    fontSize: 14,
    color: adeniTheme.textMuted,
  },
  notes: {
    marginTop: 10,
    fontSize: 14,
    lineHeight: 20,
    fontStyle: "italic",
    color: adeniTheme.text,
  },
  status: {
    marginTop: 6,
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.accent,
  },
  actions: {
    marginTop: 16,
    flexDirection: "row",
    justifyContent: "flex-end",
    gap: 10,
  },
  rejectButton: {
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: 999,
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  rejectButtonText: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  acceptButton: {
    minWidth: 96,
    alignItems: "center",
    backgroundColor: adeniTheme.primary,
    borderRadius: 999,
    paddingHorizontal: 18,
    paddingVertical: 10,
  },
  acceptButtonText: {
    fontSize: 14,
    fontWeight: "600",
    color: "#ffffff",
  },
  buttonDisabled: {
    opacity: 0.65,
  },
});
