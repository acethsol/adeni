import { useCallback, useEffect, useState } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";
import type { AdeniApiError } from "@adeni/api-client";
import { VERIFICATION_BADGE_LABELS, type VerificationBadge } from "@adeni/shared";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Callout } from "@/components/ui/Callout";
import { useAuth } from "@/contexts/auth-context";
import { adeniTheme } from "@/lib/theme";

const UPGRADE_BADGES = ["cac", "address", "license"] as const;

type UpgradeBadgeType = (typeof UPGRADE_BADGES)[number];

export function BusinessBadgeUpgrade() {
  const { createBusinessApiClient } = useAuth();
  const [badges, setBadges] = useState<VerificationBadge[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [selectedType, setSelectedType] = useState<UpgradeBadgeType>("cac");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const loadBadges = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      setBadges(await client.listTenantVerificationBadges());
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(
        apiError.statusCode === 401
          ? "Sign in with your business account to manage badges."
          : "Could not load verification badges.",
      );
      setBadges([]);
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient]);

  useEffect(() => {
    void loadBadges();
  }, [loadBadges]);

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);
    setMessage(null);

    try {
      const client = await createBusinessApiClient();
      await client.requestVerificationBadge({
        badgeType: selectedType,
        referenceNumber: referenceNumber.trim() || undefined,
      });
      await loadBadges();
      setReferenceNumber("");
      setMessage("Badge upgrade requested.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not request badge.");
    } finally {
      setSubmitting(false);
    }
  }

  const granted = new Set(
    badges.filter((badge) => badge.status === "granted").map((badge) => badge.badgeType),
  );
  const pending = new Set(
    badges.filter((badge) => badge.status === "pending").map((badge) => badge.badgeType),
  );

  return (
    <Card title="Trust badges" description="Request advanced verification to stand out in discovery.">
      {loading ? <Text style={styles.hint}>Loading badges…</Text> : null}
      {error ? <Callout tone="error">{error}</Callout> : null}
      {message ? <Text style={styles.success}>{message}</Text> : null}

      {!loading ? (
        <>
          <View style={styles.badgeRow}>
            {Object.entries(VERIFICATION_BADGE_LABELS).map(([type, label]) => (
              <Text
                key={type}
                style={[
                  styles.badgeChip,
                  granted.has(type) && styles.badgeGranted,
                  pending.has(type) && styles.badgePending,
                ]}
              >
                {label}
                {pending.has(type) ? " · pending" : granted.has(type) ? "" : " · not granted"}
              </Text>
            ))}
          </View>

          <Text style={styles.fieldLabel}>Request upgrade</Text>
          <View style={styles.chipRow}>
            {UPGRADE_BADGES.map((type) => {
              const disabled = granted.has(type) || pending.has(type);
              const active = selectedType === type;
              return (
                <Pressable
                  key={type}
                  disabled={disabled}
                  onPress={() => setSelectedType(type)}
                  style={[
                    styles.chip,
                    active && styles.chipActive,
                    disabled && styles.chipDisabled,
                  ]}
                >
                  <Text style={[styles.chipText, active && styles.chipTextActive]}>
                    {VERIFICATION_BADGE_LABELS[type]}
                  </Text>
                </Pressable>
              );
            })}
          </View>

          <Input
            label="Reference number (optional)"
            value={referenceNumber}
            onChangeText={setReferenceNumber}
            placeholder="CAC number, license ID, or utility bill reference"
            autoCapitalize="characters"
          />

          <Button
            title={submitting ? "Submitting…" : "Request badge review"}
            onPress={() => void handleSubmit()}
            loading={submitting}
            disabled={submitting || (granted.has(selectedType) || pending.has(selectedType))}
            containerStyle={styles.submitButton}
          />
        </>
      ) : null}
    </Card>
  );
}

const styles = StyleSheet.create({
  hint: { color: adeniTheme.textMuted, fontSize: adeniTheme.typography.bodySm.fontSize },
  success: {
    marginTop: adeniTheme.spacing.sm,
    color: adeniTheme.accent,
    fontSize: adeniTheme.typography.bodySm.fontSize,
  },
  badgeRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.sm,
    marginTop: adeniTheme.spacing.md,
  },
  badgeChip: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.full,
    backgroundColor: adeniTheme.subtle,
    paddingHorizontal: adeniTheme.spacing.sm,
    paddingVertical: 4,
    fontSize: 11,
    fontWeight: "700",
    color: adeniTheme.textMuted,
  },
  badgeGranted: {
    borderColor: `${adeniTheme.accent}55`,
    backgroundColor: `${adeniTheme.accent}18`,
    color: adeniTheme.accent,
  },
  badgePending: {
    borderColor: "#fcd34d",
    backgroundColor: "#fffbeb",
    color: "#92400e",
  },
  fieldLabel: {
    marginTop: adeniTheme.spacing.lg,
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.textSubtle,
  },
  chipRow: {
    marginTop: adeniTheme.spacing.sm,
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.sm,
  },
  chip: {
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: adeniTheme.radius.full,
    paddingHorizontal: adeniTheme.spacing.lg,
    paddingVertical: adeniTheme.spacing.sm,
    backgroundColor: adeniTheme.surface,
  },
  chipActive: {
    backgroundColor: adeniTheme.primary,
    borderColor: adeniTheme.primary,
  },
  chipDisabled: {
    opacity: 0.45,
  },
  chipText: {
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  chipTextActive: {
    color: adeniTheme.primaryForeground,
  },
  submitButton: {
    marginTop: adeniTheme.spacing.lg,
    alignSelf: "stretch",
  },
});
