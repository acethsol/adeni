import { useCallback, useEffect, useState } from "react";
import { Alert, Image, ScrollView, StyleSheet, Text, TextInput, View } from "react-native";
import type { QuoteRequestResponse, ServiceOffering } from "@adeni/shared";
import { QUOTE_STATUS_LABELS } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { BusinessTabs } from "@/components/adeni/BusinessTabs";
import { Button } from "@/components/ui/Button";
import { useAuth } from "@/contexts/auth-context";
import { adeniTheme } from "@/lib/theme";

export default function BusinessQuotesScreen() {
  const {
    loading: authLoading,
    isBusinessPortalEnabled,
    hasBusinessAccount,
    createBusinessApiClient,
  } = useAuth();

  const [quotes, setQuotes] = useState<QuoteRequestResponse[]>([]);
  const [services, setServices] = useState<ServiceOffering[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [amount, setAmount] = useState("");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const client = await createBusinessApiClient();
      const [quoteItems, serviceItems] = await Promise.all([
        client.listTenantQuotes(),
        client.getTenantServices(),
      ]);
      setQuotes(quoteItems);
      setServices(serviceItems.filter((item) => item.isActive));
    } catch {
      setError("Could not load quote inbox.");
      setQuotes([]);
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient]);

  useEffect(() => {
    if (authLoading) return;
    if (isBusinessPortalEnabled && hasBusinessAccount) void load();
    else setLoading(false);
  }, [authLoading, hasBusinessAccount, isBusinessPortalEnabled, load]);

  async function submitOffer(quoteId: string) {
    const service = services[0];
    const parsedAmount = Number(amount);
    if (!service || !amount.trim() || Number.isNaN(parsedAmount) || parsedAmount <= 0) {
      Alert.alert("Enter a valid amount and add at least one active service.");
      return;
    }

    setBusy(true);
    try {
      const client = await createBusinessApiClient();
      const start = new Date();
      start.setDate(start.getDate() + 2);
      start.setHours(10, 0, 0, 0);
      const end = new Date(start.getTime() + service.durationMinutes * 60_000);
      const updated = await client.submitQuoteOffer(quoteId, {
        amount: parsedAmount,
        currency: service.currency,
        serviceOfferingId: service.id,
        proposedStartAt: start.toISOString(),
        proposedEndAt: end.toISOString(),
      });
      setQuotes((current) => current.map((item) => (item.id === quoteId ? updated : item)));
      setSelectedId(null);
      setAmount("");
    } catch {
      Alert.alert("Could not send quote.");
    } finally {
      setBusy(false);
    }
  }

  const canManage = isBusinessPortalEnabled && hasBusinessAccount;

  return (
    <Screen loading={authLoading || loading} error={error}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Quote inbox"
          subtitle="Review customer job requests and send formal quotes."
        />
        {canManage ? <BusinessTabs /> : null}

        {quotes.length === 0 ? (
          <Text style={styles.hint}>No quote requests yet.</Text>
        ) : (
          quotes.map((quote) => (
            <View key={quote.id} style={styles.card}>
              <Text style={styles.status}>{QUOTE_STATUS_LABELS[quote.status] ?? quote.status}</Text>
              <Text style={styles.description}>{quote.description}</Text>
              {quote.serviceAddress ? <Text style={styles.meta}>At: {quote.serviceAddress}</Text> : null}
              {quote.photoUrls && quote.photoUrls.length > 0 ? (
                <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                  {quote.photoUrls.map((url) => (
                    <Image key={url} source={{ uri: url }} style={styles.photo} />
                  ))}
                </ScrollView>
              ) : null}
              {quote.status === "submitted" ? (
                selectedId === quote.id ? (
                  <View style={styles.offerForm}>
                    <TextInput
                      value={amount}
                      onChangeText={setAmount}
                      placeholder="Quote amount"
                      keyboardType="decimal-pad"
                      style={styles.input}
                    />
                    <Button
                      title={busy ? "Sending…" : "Send quote"}
                      onPress={() => void submitOffer(quote.id)}
                      disabled={busy}
                    />
                  </View>
                ) : (
                  <Button title="Send quote" onPress={() => setSelectedId(quote.id)} />
                )
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
  hint: { color: adeniTheme.textMuted },
  card: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.lg,
    backgroundColor: adeniTheme.surface,
    padding: adeniTheme.spacing.lg,
    gap: adeniTheme.spacing.sm,
  },
  status: { fontSize: 11, fontWeight: "700", textTransform: "uppercase", color: adeniTheme.textMuted },
  description: { color: adeniTheme.text },
  meta: { color: adeniTheme.textMuted, fontSize: adeniTheme.typography.bodySm.fontSize },
  photo: { width: 72, height: 72, borderRadius: adeniTheme.radius.md, marginRight: adeniTheme.spacing.sm },
  offerForm: { gap: adeniTheme.spacing.sm, marginTop: adeniTheme.spacing.sm },
  input: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.md,
    paddingHorizontal: adeniTheme.spacing.md,
    paddingVertical: adeniTheme.spacing.sm,
    color: adeniTheme.text,
  },
});
