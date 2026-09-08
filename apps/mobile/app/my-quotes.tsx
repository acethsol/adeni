import { useCallback, useEffect, useState } from "react";
import { Alert, Image, Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { useRouter } from "expo-router";
import type { AdeniApiError } from "@adeni/api-client";
import type { QuoteRequestResponse } from "@adeni/shared";
import { QUOTE_STATUS_LABELS } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { Button } from "@/components/ui/Button";
import { useAuth } from "@/contexts/auth-context";
import { adeniTheme } from "@/lib/theme";

export default function MyQuotesScreen() {
  const router = useRouter();
  const { loading: authLoading, isBookingEnabled, createApiClient } = useAuth();
  const [quotes, setQuotes] = useState<QuoteRequestResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionId, setActionId] = useState<string | null>(null);

  const loadQuotes = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const client = createApiClient("customer");
      setQuotes(await client.listCustomerQuotes());
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(apiError.statusCode === 401 ? "Sign in to view your quotes." : "Could not load quotes.");
      setQuotes([]);
    } finally {
      setLoading(false);
    }
  }, [createApiClient]);

  useEffect(() => {
    if (authLoading) return;
    if (isBookingEnabled) void loadQuotes();
    else setLoading(false);
  }, [authLoading, isBookingEnabled, loadQuotes]);

  async function handleAccept(quote: QuoteRequestResponse) {
    setActionId(quote.id);
    try {
      const client = createApiClient("customer");
      const updated = await client.acceptQuote(quote.id);
      setQuotes((current) => current.map((item) => (item.id === quote.id ? updated : item)));
    } catch {
      Alert.alert("Could not accept quote", "Try again in a moment.");
    } finally {
      setActionId(null);
    }
  }

  async function handleDecline(quote: QuoteRequestResponse) {
    setActionId(quote.id);
    try {
      const client = createApiClient("customer");
      const updated = await client.declineQuote(quote.id);
      setQuotes((current) => current.map((item) => (item.id === quote.id ? updated : item)));
    } catch {
      Alert.alert("Could not decline quote", "Try again in a moment.");
    } finally {
      setActionId(null);
    }
  }

  return (
    <Screen loading={authLoading || loading} error={error}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Customer"
          title="My quotes"
          subtitle="Track quote requests and accept offers from businesses."
        />

        {!isBookingEnabled ? (
          <Text style={styles.hint}>Sign in from Account to view quote requests.</Text>
        ) : quotes.length === 0 ? (
          <Text style={styles.hint}>No quote requests yet. Browse businesses and request a quote.</Text>
        ) : (
          quotes.map((quote) => (
            <View key={quote.id} style={styles.card}>
              <Text style={styles.status}>{QUOTE_STATUS_LABELS[quote.status] ?? quote.status}</Text>
              <Text style={styles.description}>{quote.description}</Text>
              {quote.serviceAddress ? <Text style={styles.meta}>At: {quote.serviceAddress}</Text> : null}
              {quote.photoUrls && quote.photoUrls.length > 0 ? (
                <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.photoRow}>
                  {quote.photoUrls.map((url) => (
                    <Image key={url} source={{ uri: url }} style={styles.photo} />
                  ))}
                </ScrollView>
              ) : null}
              {quote.status === "quoted" && quote.quotedAmount != null ? (
                <View style={styles.offer}>
                  <Text style={styles.amount}>
                    {quote.quotedCurrency} {quote.quotedAmount.toLocaleString()}
                  </Text>
                  <View style={styles.actions}>
                    <Button
                      title={actionId === quote.id ? "Working…" : "Accept"}
                      onPress={() => void handleAccept(quote)}
                      disabled={actionId === quote.id}
                    />
                    <Button
                      title="Decline"
                      variant="secondary"
                      onPress={() => void handleDecline(quote)}
                      disabled={actionId === quote.id}
                    />
                  </View>
                </View>
              ) : null}
              {quote.status === "accepted" ? (
                <Pressable onPress={() => router.push("/my-bookings")}>
                  <Text style={styles.link}>View booking →</Text>
                </Pressable>
              ) : null}
            </View>
          ))
        )}
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  content: { padding: adeniTheme.spacing.xl, gap: adeniTheme.spacing.md },
  hint: { color: adeniTheme.textMuted, fontSize: adeniTheme.typography.bodySm.fontSize },
  card: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.lg,
    backgroundColor: adeniTheme.surface,
    padding: adeniTheme.spacing.lg,
    gap: adeniTheme.spacing.sm,
  },
  status: {
    fontSize: 11,
    fontWeight: "700",
    textTransform: "uppercase",
    color: adeniTheme.textMuted,
  },
  description: { color: adeniTheme.text, lineHeight: 20 },
  meta: { color: adeniTheme.textMuted, fontSize: adeniTheme.typography.bodySm.fontSize },
  photoRow: { marginTop: adeniTheme.spacing.sm },
  photo: { width: 72, height: 72, borderRadius: adeniTheme.radius.md, marginRight: adeniTheme.spacing.sm },
  offer: {
    marginTop: adeniTheme.spacing.sm,
    borderWidth: 1,
    borderColor: `${adeniTheme.accent}33`,
    borderRadius: adeniTheme.radius.md,
    backgroundColor: `${adeniTheme.accent}10`,
    padding: adeniTheme.spacing.md,
    gap: adeniTheme.spacing.sm,
  },
  amount: { fontSize: 18, fontWeight: "700", color: adeniTheme.text },
  actions: { flexDirection: "row", gap: adeniTheme.spacing.sm },
  link: { color: adeniTheme.accent, fontWeight: "600" },
});
