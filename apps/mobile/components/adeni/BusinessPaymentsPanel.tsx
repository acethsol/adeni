import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Linking,
  Share,
  StyleSheet,
  Text,
  View,
} from "react-native";
import type { AdeniApiError } from "@adeni/api-client";
import type { BusinessProfile, PaymentLedgerEntry } from "@adeni/shared";
import { hasCapability } from "@adeni/shared";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { Input } from "@/components/ui/Input";
import { Card } from "@/components/ui/Card";
import { useAuth } from "@/contexts/auth-context";
import { formatPrice } from "@/lib/format";
import { getWebBaseUrl } from "@/lib/env";
import { adeniTheme } from "@/lib/theme";

type Props = {
  profile: BusinessProfile;
};

function resolveCheckoutUrl(checkoutUrl: string): string {
  if (/^https?:\/\//i.test(checkoutUrl)) {
    return checkoutUrl;
  }

  const base = getWebBaseUrl().replace(/\/$/, "");
  const path = checkoutUrl.startsWith("/") ? checkoutUrl : `/${checkoutUrl}`;
  return `${base}${path}`;
}

function whatsAppShareUrl(description: string, checkoutUrl: string) {
  const text = encodeURIComponent(`Pay ${description} via Adeni: ${checkoutUrl}`);
  return `https://wa.me/?text=${text}`;
}

export function BusinessPaymentsPanel({ profile }: Props) {
  const { createBusinessApiClient } = useAuth();
  const [ledger, setLedger] = useState<PaymentLedgerEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [refundingId, setRefundingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [lastLink, setLastLink] = useState<{ checkoutUrl: string; description: string } | null>(
    null,
  );

  const supportsDeposits = hasCapability(profile.capabilities, "deposits");

  const loadLedger = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const items = await client.listPaymentLedger(profile.tenantId);
      setLedger(items);
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(apiError.message || "Could not load payments.");
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient, profile.tenantId]);

  useEffect(() => {
    if (supportsDeposits) {
      void loadLedger();
    }
  }, [loadLedger, supportsDeposits]);

  async function handleCreateLink() {
    const parsedAmount = Number(amount);
    if (!Number.isFinite(parsedAmount) || parsedAmount <= 0 || !description.trim()) {
      setError("Enter a valid amount and description.");
      return;
    }

    setCreating(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const payment = await client.createPaymentLink({
        tenantId: profile.tenantId,
        amount: parsedAmount,
        currency: "NGN",
        description: description.trim(),
      });

      const checkoutUrl = resolveCheckoutUrl(payment.checkoutUrl);
      setLastLink({ checkoutUrl, description: description.trim() });
      setAmount("");
      setDescription("");
      await loadLedger();
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(apiError.message || "Could not create payment link.");
    } finally {
      setCreating(false);
    }
  }

  function handleRefund(entry: PaymentLedgerEntry) {
    if (entry.status !== "completed") {
      return;
    }

    Alert.alert(
      "Refund payment",
      `Refund ${formatPrice(entry.amount, entry.currency)}?`,
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Refund",
          style: "destructive",
          onPress: () => void confirmRefund(entry),
        },
      ],
    );
  }

  async function confirmRefund(entry: PaymentLedgerEntry) {
    setRefundingId(entry.id);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      await client.refundPayment(entry.id, { tenantId: profile.tenantId });
      await loadLedger();
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(apiError.message || "Refund failed.");
    } finally {
      setRefundingId(null);
    }
  }

  if (!supportsDeposits) {
    return (
      <Callout title="Payments unavailable">
        Payments are not enabled for your business category yet.
      </Callout>
    );
  }

  return (
    <View style={styles.stack}>
      <Card title="Create payment link" description="Share a Pay ₦X link with customers on WhatsApp.">
        <Input
          label="Amount (NGN)"
          value={amount}
          onChangeText={setAmount}
          keyboardType="decimal-pad"
        />
        <Input
          label="Description"
          value={description}
          onChangeText={setDescription}
          placeholder="e.g. Fade + beard trim deposit"
          maxLength={500}
        />
        <Button
          title={creating ? "Creating…" : "Create link"}
          onPress={() => void handleCreateLink()}
          loading={creating}
          containerStyle={styles.primaryAction}
        />

        {lastLink ? (
          <View style={styles.latestLink}>
            <Text style={styles.latestTitle}>Latest link</Text>
            <Text style={styles.latestUrl} selectable>
              {lastLink.checkoutUrl}
            </Text>
            <View style={styles.linkActions}>
              <Button
                title="Share link"
                variant="secondary"
                onPress={() =>
                  void Share.share({
                    message: `Pay ${lastLink.description} via Adeni: ${lastLink.checkoutUrl}`,
                  })
                }
              />
              <Button
                title="WhatsApp"
                onPress={() => void Linking.openURL(whatsAppShareUrl(lastLink.description, lastLink.checkoutUrl))}
                containerStyle={styles.whatsAppButton}
              />
            </View>
          </View>
        ) : null}
      </Card>

      <Card title="Payment history">
        {error ? (
          <Callout tone="error" title="Something went wrong">
            {error}
          </Callout>
        ) : null}

        {loading ? (
          <Text style={styles.muted}>Loading payments…</Text>
        ) : ledger.length === 0 ? (
          <Text style={styles.muted}>No payments yet.</Text>
        ) : (
          <View style={styles.ledger}>
            {ledger.map((entry) => (
              <View key={entry.id} style={styles.ledgerRow}>
                <View style={styles.ledgerCopy}>
                  <Text style={styles.ledgerAmount}>
                    {formatPrice(entry.amount, entry.currency)}
                  </Text>
                  <Text style={styles.ledgerMeta}>
                    {entry.type} · {entry.status}
                  </Text>
                  {entry.description ? (
                    <Text style={styles.ledgerDescription}>{entry.description}</Text>
                  ) : null}
                  <Text style={styles.ledgerRef}>{entry.providerReference}</Text>
                </View>
                {entry.status === "completed" ? (
                  <Button
                    title={refundingId === entry.id ? "Refunding…" : "Refund"}
                    variant="secondary"
                    onPress={() => handleRefund(entry)}
                    loading={refundingId === entry.id}
                    disabled={refundingId !== null && refundingId !== entry.id}
                  />
                ) : null}
              </View>
            ))}
          </View>
        )}
      </Card>
    </View>
  );
}

const styles = StyleSheet.create({
  stack: {
    gap: adeniTheme.spacing.xl,
  },
  primaryAction: {
    marginTop: adeniTheme.spacing.lg,
    alignSelf: "stretch",
  },
  latestLink: {
    marginTop: adeniTheme.spacing.lg,
    borderWidth: 1,
    borderStyle: "dashed",
    borderColor: "rgba(64, 145, 108, 0.4)",
    borderRadius: adeniTheme.radius.lg,
    backgroundColor: "rgba(64, 145, 108, 0.05)",
    padding: adeniTheme.spacing.lg,
  },
  latestTitle: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  latestUrl: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 13,
    lineHeight: 18,
    color: adeniTheme.textMuted,
  },
  linkActions: {
    marginTop: adeniTheme.spacing.md,
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.sm,
  },
  whatsAppButton: {
    backgroundColor: "#25D366",
  },
  muted: {
    marginTop: adeniTheme.spacing.md,
    fontSize: 14,
    color: adeniTheme.textMuted,
  },
  ledger: {
    marginTop: adeniTheme.spacing.md,
    gap: adeniTheme.spacing.lg,
  },
  ledgerRow: {
    flexDirection: "row",
    alignItems: "flex-start",
    justifyContent: "space-between",
    gap: adeniTheme.spacing.md,
    paddingBottom: adeniTheme.spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: adeniTheme.border,
  },
  ledgerCopy: {
    flex: 1,
  },
  ledgerAmount: {
    fontSize: 16,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  ledgerMeta: {
    marginTop: 2,
    fontSize: 13,
    color: adeniTheme.textMuted,
    textTransform: "capitalize",
  },
  ledgerDescription: {
    marginTop: 4,
    fontSize: 13,
    color: adeniTheme.textMuted,
  },
  ledgerRef: {
    marginTop: 4,
    fontSize: 11,
    fontFamily: "monospace",
    color: adeniTheme.textSubtle,
  },
});
