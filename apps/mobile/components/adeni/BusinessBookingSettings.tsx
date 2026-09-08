import { useState } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";
import type { BusinessProfile } from "@adeni/shared";
import { hasCapability } from "@adeni/shared";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { Input } from "@/components/ui/Input";
import { useAuth } from "@/contexts/auth-context";
import { adeniTheme } from "@/lib/theme";

type Props = {
  profile: BusinessProfile;
  onSaved?: (profile: BusinessProfile) => void;
};

export function BusinessBookingSettings({ profile, onSaved }: Props) {
  const { createBusinessApiClient } = useAuth();
  const [autoConfirm, setAutoConfirm] = useState(profile.autoConfirmBookings ?? false);
  const [depositPercent, setDepositPercent] = useState(String(profile.depositPercent ?? 0));
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!hasCapability(profile.capabilities, "calendar")) {
    return null;
  }

  async function handleSave() {
    const parsedDeposit = Number(depositPercent);
    if (!Number.isFinite(parsedDeposit) || parsedDeposit < 0 || parsedDeposit > 100) {
      setError("Deposit must be between 0 and 100.");
      return;
    }

    setSaving(true);
    setMessage(null);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const updated = await client.updateTenantSettings({
        autoConfirmBookings: autoConfirm,
        depositPercent: parsedDeposit,
      });
      onSaved?.(updated);
      setMessage("Booking settings saved.");
    } catch {
      setError("Could not save settings.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <View>
      <Text style={styles.lead}>
        When enabled, new online bookings are confirmed immediately without manual accept.
      </Text>

      <Pressable
        onPress={() => setAutoConfirm((value) => !value)}
        style={({ pressed }) => [styles.toggleRow, pressed && styles.toggleRowPressed]}
      >
        <View style={[styles.checkbox, autoConfirm && styles.checkboxChecked]}>
          {autoConfirm ? <Text style={styles.checkmark}>✓</Text> : null}
        </View>
        <View style={styles.toggleCopy}>
          <Text style={styles.toggleTitle}>Auto-confirm bookings</Text>
          <Text style={styles.toggleHint}>
            Recommended for trusted businesses with predictable schedules.
          </Text>
        </View>
      </Pressable>

      {hasCapability(profile.capabilities, "deposits") ? (
        <Input
          label="Deposit at confirm (%)"
          hint="Customers pay this percentage when booking online. Set 0 to disable deposit checkout."
          value={depositPercent}
          onChangeText={setDepositPercent}
          keyboardType="number-pad"
        />
      ) : null}

      {error ? (
        <Callout tone="error" title="Could not save">
          {error}
        </Callout>
      ) : null}
      {message ? <Text style={styles.success}>{message}</Text> : null}

      <Button
        title={saving ? "Saving…" : "Save booking settings"}
        onPress={() => void handleSave()}
        loading={saving}
        containerStyle={styles.saveButton}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  lead: {
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  toggleRow: {
    marginTop: adeniTheme.spacing.lg,
    flexDirection: "row",
    alignItems: "flex-start",
    gap: adeniTheme.spacing.md,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.lg,
    backgroundColor: adeniTheme.subtle,
    padding: adeniTheme.spacing.lg,
  },
  toggleRowPressed: {
    opacity: 0.9,
  },
  checkbox: {
    marginTop: 2,
    width: 22,
    height: 22,
    borderRadius: 6,
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    backgroundColor: adeniTheme.surface,
    alignItems: "center",
    justifyContent: "center",
  },
  checkboxChecked: {
    backgroundColor: adeniTheme.primary,
    borderColor: adeniTheme.primary,
  },
  checkmark: {
    fontSize: 14,
    fontWeight: "700",
    color: adeniTheme.primaryForeground,
  },
  toggleCopy: {
    flex: 1,
  },
  toggleTitle: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  toggleHint: {
    marginTop: 4,
    fontSize: 13,
    lineHeight: 18,
    color: adeniTheme.textMuted,
  },
  saveButton: {
    marginTop: adeniTheme.spacing.xl,
    alignSelf: "stretch",
  },
  success: {
    marginTop: adeniTheme.spacing.md,
    fontSize: 14,
    color: adeniTheme.accent,
  },
});
