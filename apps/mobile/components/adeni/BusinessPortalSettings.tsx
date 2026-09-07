import { useCallback, useEffect, useState } from "react";
import { Pressable, StyleSheet, Switch, Text, View } from "react-native";
import type { MessagingSettings, NotificationPreferences } from "@adeni/shared";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { useAuth } from "@/contexts/auth-context";
import { adeniTheme } from "@/lib/theme";

export function BusinessPortalSettings() {
  const { createBusinessApiClient } = useAuth();
  const [prefs, setPrefs] = useState<NotificationPreferences>({
    emailEnabled: true,
    pushEnabled: false,
    smsWhatsAppReminderEnabled: false,
  });
  const [messaging, setMessaging] = useState<MessagingSettings>({
    faqAutoResponderEnabled: true,
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const client = await createBusinessApiClient();
      const [nextPrefs, nextMessaging] = await Promise.all([
        client.getNotificationPreferences(),
        client.getMessagingSettings(),
      ]);
      setPrefs(nextPrefs);
      setMessaging(nextMessaging);
    } catch {
      setError("Could not load settings.");
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleSave() {
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      const client = await createBusinessApiClient();
      const [nextPrefs, nextMessaging] = await Promise.all([
        client.updateNotificationPreferences(prefs),
        client.updateMessagingSettings(messaging),
      ]);
      setPrefs(nextPrefs);
      setMessaging(nextMessaging);
      setSaved(true);
    } catch {
      setError("Could not save settings.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <Card style={styles.card}>
        <Text style={styles.title}>Notifications & messaging</Text>
        <Text style={styles.hint}>Loading settings…</Text>
      </Card>
    );
  }

  return (
    <Card style={styles.card}>
      <Text style={styles.title}>Notifications & messaging</Text>
      <Text style={styles.hint}>
        Choose how Adeni notifies customers and whether FAQ auto-replies are sent.
      </Text>

      <SettingRow
        label="Email notifications"
        value={prefs.emailEnabled}
        onValueChange={(emailEnabled) => setPrefs((current) => ({ ...current, emailEnabled }))}
      />
      <SettingRow
        label="Push (FCM)"
        value={prefs.pushEnabled}
        onValueChange={(pushEnabled) => setPrefs((current) => ({ ...current, pushEnabled }))}
      />
      <SettingRow
        label="SMS / WhatsApp reminders"
        value={prefs.smsWhatsAppReminderEnabled}
        onValueChange={(smsWhatsAppReminderEnabled) =>
          setPrefs((current) => ({ ...current, smsWhatsAppReminderEnabled }))
        }
      />
      <SettingRow
        label="FAQ auto-responder"
        value={messaging.faqAutoResponderEnabled}
        onValueChange={(faqAutoResponderEnabled) => setMessaging({ faqAutoResponderEnabled })}
      />

      {error ? <Callout title="Error">{error}</Callout> : null}
      {saved ? <Text style={styles.saved}>Settings saved.</Text> : null}

      <Button
        title={saving ? "Saving…" : "Save settings"}
        onPress={() => void handleSave()}
        loading={saving}
        disabled={saving}
        containerStyle={styles.saveButton}
      />
    </Card>
  );
}

function SettingRow({
  label,
  value,
  onValueChange,
}: {
  label: string;
  value: boolean;
  onValueChange: (value: boolean) => void;
}) {
  return (
    <Pressable style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Switch value={value} onValueChange={onValueChange} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    marginTop: adeniTheme.spacing.xl,
  },
  title: {
    fontSize: 17,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  hint: {
    marginTop: 6,
    fontSize: 13,
    color: adeniTheme.textMuted,
    lineHeight: 18,
  },
  row: {
    marginTop: adeniTheme.spacing.lg,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: adeniTheme.spacing.md,
  },
  rowLabel: {
    flex: 1,
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  saveButton: {
    marginTop: adeniTheme.spacing.lg,
  },
  saved: {
    marginTop: adeniTheme.spacing.md,
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.primary,
  },
});
