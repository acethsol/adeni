import { useCallback, useEffect, useState } from "react";
import { ScrollView, StyleSheet, Text, View } from "react-native";
import type { AdeniApiError } from "@adeni/api-client";
import type { BookingResponse } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { BusinessTabs } from "@/components/adeni/BusinessTabs";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { EmptyState } from "@/components/ui/EmptyState";
import { useAuth } from "@/contexts/auth-context";
import { formatBookingStatus, formatSlotTime } from "@/lib/format";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

const PENDING_STATUS = 0;

export default function BusinessBookingsScreen() {
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
  const history = bookings.filter((booking) => booking.status !== PENDING_STATUS);

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Booking inbox"
          subtitle="Review and respond to customer booking requests."
        />

        {isBusinessInboxEnabled ? <BusinessTabs /> : null}

        <View style={styles.section}>
          {!isBusinessInboxEnabled ? (
            <>
              <Callout title="Business access required">
                {isAuth0Configured()
                  ? "Sign in with a business account linked to your tenant."
                  : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB in .env for local business mode, or sign in with Auth0."}
              </Callout>
              {isAuth0Configured() ? (
                <Button
                  title="Retry"
                  variant="secondary"
                  onPress={() => void refreshSession().then(() => loadBookings())}
                  containerStyle={styles.retryButton}
                />
              ) : null}
            </>
          ) : null}

          {error ? <Callout tone="error">{error}</Callout> : null}

          {isBusinessInboxEnabled ? (
            <>
              <Text style={styles.sectionLabel}>Pending ({pending.length})</Text>

              {pending.length === 0 ? (
                <EmptyState
                  title="No pending bookings"
                  description="New booking requests will show up here for you to accept or reject."
                />
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

              {history.length > 0 ? (
                <>
                  <Text style={styles.sectionLabel}>Recent</Text>
                  <View style={styles.list}>
                    {history.map((booking) => (
                      <Card key={booking.id} style={styles.historyCard} padding="sm">
                        <View style={styles.historyHeader}>
                          <Text style={styles.serviceName}>{booking.serviceName}</Text>
                          <Badge
                            label={formatBookingStatus(booking.status)}
                            tone={booking.status === 1 ? "success" : "default"}
                          />
                        </View>
                        <Text style={styles.meta}>{formatSlotTime(booking.startAt)}</Text>
                      </Card>
                    ))}
                  </View>
                </>
              ) : null}
            </>
          ) : null}
        </View>
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
    <Card style={styles.card}>
      <Text style={styles.serviceName}>{booking.serviceName}</Text>
      <Text style={styles.meta}>{formatSlotTime(booking.startAt)}</Text>
      {booking.customerNotes ? (
        <Text style={styles.notes}>&ldquo;{booking.customerNotes}&rdquo;</Text>
      ) : null}

      <View style={styles.actions}>
        <Button title="Reject" variant="secondary" size="sm" disabled={busy} onPress={onReject} />
        <Button title="Accept" size="sm" loading={busy} onPress={onAccept} />
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  content: {
    paddingBottom: adeniTheme.spacing["3xl"],
  },
  section: {
    paddingHorizontal: adeniTheme.spacing.xl,
  },
  retryButton: {
    marginTop: adeniTheme.spacing.md,
  },
  sectionLabel: {
    marginTop: adeniTheme.spacing["2xl"],
    fontSize: 12,
    fontWeight: "700",
    letterSpacing: 0.8,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  list: {
    marginTop: adeniTheme.spacing.md,
    gap: adeniTheme.spacing.md,
  },
  card: {},
  historyCard: {},
  historyHeader: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: adeniTheme.spacing.sm,
  },
  serviceName: {
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  meta: {
    marginTop: 4,
    fontSize: 13,
    color: adeniTheme.textMuted,
  },
  notes: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 14,
    lineHeight: 20,
    fontStyle: "italic",
    color: adeniTheme.text,
  },
  actions: {
    marginTop: adeniTheme.spacing.lg,
    flexDirection: "row",
    justifyContent: "flex-end",
    gap: adeniTheme.spacing.sm,
  },
});
