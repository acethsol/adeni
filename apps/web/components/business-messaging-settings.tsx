"use client";

import { useCallback, useEffect, useState } from "react";
import type { MessagingSettings } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { useToast } from "@/contexts/toast-context";
import { useApiErrorMessage } from "@/lib/api-error";

export function BusinessMessagingSettings() {
  const toast = useToast();
  const { formatApiError } = useApiErrorMessage();
  const [settings, setSettings] = useState<MessagingSettings>({ faqAutoResponderEnabled: true });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadSettings = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch("/api/business/messages/settings");
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Could not load messaging settings."));
      }

      setSettings(payload as MessagingSettings);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load messaging settings.");
    } finally {
      setLoading(false);
    }
  }, [formatApiError]);

  useEffect(() => {
    void loadSettings();
  }, [loadSettings]);

  async function handleSave() {
    setSaving(true);
    setError(null);

    try {
      const response = await fetch("/api/business/messages/settings", {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(settings),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Could not save messaging settings."));
      }

      setSettings(payload as MessagingSettings);
      toast.success("Messaging settings saved.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save messaging settings.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <p className="text-sm text-muted">Loading messaging settings…</p>;
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted">
        Automatically reply to common customer questions about pricing, hours, and location when
        they message you in-app.
      </p>

      <label className="flex items-start gap-3 rounded-xl border border-border bg-subtle/40 p-4 text-sm">
        <input
          type="checkbox"
          className="mt-1"
          checked={settings.faqAutoResponderEnabled}
          onChange={(event) =>
            setSettings({ faqAutoResponderEnabled: event.target.checked })
          }
        />
        <span>
          <span className="font-medium text-foreground">FAQ auto-responder</span>
          <span className="mt-1 block text-muted">
            Sends rule-based replies for price, hours, and availability questions. You can still
            reply manually from your inbox.
          </span>
        </span>
      </label>

      {error ? <Callout tone="error">{error}</Callout> : null}
      <Button type="button" onClick={handleSave} disabled={saving}>
        {saving ? "Saving…" : "Save messaging settings"}
      </Button>
    </div>
  );
}
