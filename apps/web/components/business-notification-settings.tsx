"use client";

import { useCallback, useEffect, useState } from "react";
import type { NotificationPreferences } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { useToast } from "@/contexts/toast-context";
import { useApiErrorMessage } from "@/lib/api-error";

export function BusinessNotificationSettings() {
  const toast = useToast();
  const { formatApiError } = useApiErrorMessage();
  const [prefs, setPrefs] = useState<NotificationPreferences>({
    emailEnabled: true,
    pushEnabled: false,
    smsWhatsAppReminderEnabled: false,
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadPrefs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch("/api/business/notification-preferences");
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Could not load notification preferences."));
      }

      setPrefs(payload as NotificationPreferences);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load notification preferences.");
    } finally {
      setLoading(false);
    }
  }, [formatApiError]);

  useEffect(() => {
    void loadPrefs();
  }, [loadPrefs]);

  async function handleSave() {
    setSaving(true);
    setError(null);

    try {
      const response = await fetch("/api/business/notification-preferences", {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(prefs),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Could not save notification preferences."));
      }

      setPrefs(payload as NotificationPreferences);
      toast.success("Notification preferences saved.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save notification preferences.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <p className="text-sm text-muted">Loading notification preferences…</p>;
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted">
        Choose which channels Adeni uses when notifying customers about bookings, waitlist slots,
        and reminders. Delivery is logged in development until email, push, and SMS providers are
        connected.
      </p>

      <label className="flex items-start gap-3 rounded-xl border border-border bg-subtle/40 p-4 text-sm">
        <input
          type="checkbox"
          className="mt-1"
          checked={prefs.emailEnabled}
          onChange={(event) => setPrefs((current) => ({ ...current, emailEnabled: event.target.checked }))}
        />
        <span>
          <span className="font-medium text-foreground">Email</span>
          <span className="mt-1 block text-muted">Booking confirmations, declines, and payment receipts.</span>
        </span>
      </label>

      <label className="flex items-start gap-3 rounded-xl border border-border bg-subtle/40 p-4 text-sm">
        <input
          type="checkbox"
          className="mt-1"
          checked={prefs.pushEnabled}
          onChange={(event) => setPrefs((current) => ({ ...current, pushEnabled: event.target.checked }))}
        />
        <span>
          <span className="font-medium text-foreground">Push (FCM)</span>
          <span className="mt-1 block text-muted">Mobile push alerts when customers opt in.</span>
        </span>
      </label>

      <label className="flex items-start gap-3 rounded-xl border border-border bg-subtle/40 p-4 text-sm">
        <input
          type="checkbox"
          className="mt-1"
          checked={prefs.smsWhatsAppReminderEnabled}
          onChange={(event) =>
            setPrefs((current) => ({ ...current, smsWhatsAppReminderEnabled: event.target.checked }))
          }
        />
        <span>
          <span className="font-medium text-foreground">SMS / WhatsApp reminders</span>
          <span className="mt-1 block text-muted">
            Appointment reminders via SMS or WhatsApp where supported.
          </span>
        </span>
      </label>

      {error ? <Callout tone="error">{error}</Callout> : null}
      <Button type="button" onClick={handleSave} disabled={saving}>
        {saving ? "Saving…" : "Save notification preferences"}
      </Button>
    </div>
  );
}
