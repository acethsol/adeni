"use client";

import { useCallback, useEffect, useState } from "react";
import { ShieldCheck } from "lucide-react";
import { VERIFICATION_BADGE_LABELS, type VerificationBadge } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { Input } from "@/components/ui/input";
import { useActionLoading } from "@/contexts/action-loading-context";
import { useToast } from "@/contexts/toast-context";

const UPGRADE_BADGES = [
  { type: "cac", label: VERIFICATION_BADGE_LABELS.cac, needsReference: true },
  { type: "address", label: VERIFICATION_BADGE_LABELS.address, needsReference: true },
  { type: "license", label: VERIFICATION_BADGE_LABELS.license, needsReference: true },
] as const;

export function BusinessBadgeUpgrade() {
  const { run } = useActionLoading();
  const toast = useToast();

  const [badges, setBadges] = useState<VerificationBadge[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedType, setSelectedType] = useState<(typeof UPGRADE_BADGES)[number]["type"]>("cac");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const loadBadges = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch("/api/business/verification/badges");
      if (!response.ok) {
        throw new Error("Could not load verification badges.");
      }
      const payload = (await response.json()) as { items: VerificationBadge[] };
      setBadges(payload.items ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load verification badges.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadBadges();
  }, [loadBadges]);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSubmitting(true);

    try {
      await run("Requesting badge…", async () => {
        const response = await fetch("/api/business/verification/badges", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            badgeType: selectedType,
            referenceNumber: referenceNumber.trim() || undefined,
          }),
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(typeof payload.title === "string" ? payload.title : "Could not request badge.");
        }

        await loadBadges();
        setReferenceNumber("");
        toast.success("Badge upgrade requested");
      });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not request badge.");
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
    <div className="rounded-xl border border-border bg-surface p-6 shadow-sm">
      <div className="flex items-center gap-2">
        <ShieldCheck className="h-5 w-5 text-accent" aria-hidden />
        <h2 className="text-lg font-semibold text-foreground">Trust badges</h2>
      </div>
      <p className="mt-1 text-sm text-muted">
        Request advanced verification to stand out in discovery.
      </p>

      {loading ? (
        <p className="mt-4 text-sm text-muted">Loading badges…</p>
      ) : error ? (
        <Callout tone="error" className="mt-4">
          {error}
        </Callout>
      ) : (
        <>
          <div className="mt-4 flex flex-wrap gap-2">
            {Object.entries(VERIFICATION_BADGE_LABELS).map(([type, label]) => (
              <span
                key={type}
                className={`rounded-full px-2.5 py-1 text-xs font-semibold ${
                  granted.has(type)
                    ? "border border-accent/30 bg-accent/10 text-accent"
                    : pending.has(type)
                      ? "border border-amber-300/50 bg-amber-50 text-amber-800"
                      : "border border-border bg-subtle text-muted"
                }`}
              >
                {label}
                {pending.has(type) ? " · pending" : granted.has(type) ? "" : " · not granted"}
              </span>
            ))}
          </div>

          <form onSubmit={(event) => void handleSubmit(event)} className="mt-6 space-y-4">
            <label className="block text-sm font-medium text-foreground">
              Request upgrade
              <select
                className="mt-1.5 w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm"
                value={selectedType}
                onChange={(event) =>
                  setSelectedType(event.target.value as (typeof UPGRADE_BADGES)[number]["type"])
                }
              >
                {UPGRADE_BADGES.map((badge) => (
                  <option
                    key={badge.type}
                    value={badge.type}
                    disabled={granted.has(badge.type) || pending.has(badge.type)}
                  >
                    {badge.label}
                    {granted.has(badge.type) ? " (granted)" : pending.has(badge.type) ? " (pending)" : ""}
                  </option>
                ))}
              </select>
            </label>

            <Input
              label="Reference number (optional)"
              value={referenceNumber}
              onChange={(event) => setReferenceNumber(event.target.value)}
              placeholder="CAC number, license ID, or utility bill reference"
            />

            <Button type="submit" loading={submitting} loadingLabel="Submitting…">
              Request badge review
            </Button>
          </form>
        </>
      )}
    </div>
  );
}
