"use client";

import { useState } from "react";
import type { BusinessProfile } from "@adeni/shared";
import { hasCapability } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { useToast } from "@/contexts/toast-context";

type Props = {
  profile: BusinessProfile;
};

export function BusinessBookingSettings({ profile }: Props) {
  const toast = useToast();
  const [autoConfirm, setAutoConfirm] = useState(profile.autoConfirmBookings ?? false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!hasCapability(profile.capabilities, "calendar")) {
    return null;
  }

  async function handleSave() {
    setSaving(true);
    setError(null);

    try {
      const response = await fetch("/api/business/settings", {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ autoConfirmBookings: autoConfirm }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(typeof payload.title === "string" ? payload.title : "Could not save settings.");
      }

      toast.success("Booking settings saved.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save settings.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted">
        When enabled, new online bookings are confirmed immediately without manual accept.
      </p>
      <label className="flex items-start gap-3 rounded-xl border border-border bg-subtle/40 p-4 text-sm">
        <input
          type="checkbox"
          className="mt-1"
          checked={autoConfirm}
          onChange={(event) => setAutoConfirm(event.target.checked)}
        />
        <span>
          <span className="font-medium text-foreground">Auto-confirm bookings</span>
          <span className="mt-1 block text-muted">
            Recommended for trusted businesses with predictable schedules.
          </span>
        </span>
      </label>
      {error ? <Callout tone="error">{error}</Callout> : null}
      <Button type="button" onClick={handleSave} disabled={saving}>
        {saving ? "Saving…" : "Save booking settings"}
      </Button>
    </div>
  );
}
