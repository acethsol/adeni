"use client";

import { useCallback, useEffect, useState } from "react";
import { DAY_OF_WEEK_LABELS, type WeeklyAvailabilityRule } from "@adeni/shared";
import { SkeletonList } from "@/components/ui/skeleton";
import { useActionLoading } from "@/contexts/action-loading-context";
import { Button } from "@/components/ui/button";

type DayRow = {
  dayOfWeek: number;
  enabled: boolean;
  openTime: string;
  closeTime: string;
};

const DEFAULT_OPEN = "09:00";
const DEFAULT_CLOSE = "17:00";

function buildDefaultRows(): DayRow[] {
  return [1, 2, 3, 4, 5, 6].map((dayOfWeek) => ({
    dayOfWeek,
    enabled: true,
    openTime: DEFAULT_OPEN,
    closeTime: DEFAULT_CLOSE,
  }));
}

function toTimeInput(value: string): string {
  return value.slice(0, 5);
}

function toApiTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

function rulesToRows(rules: WeeklyAvailabilityRule[]): DayRow[] {
  const defaults = buildDefaultRows();
  const allDays = [0, 1, 2, 3, 4, 5, 6].map((dayOfWeek) => {
    const rule = rules.find((item) => item.dayOfWeek === dayOfWeek);
    if (rule) {
      return {
        dayOfWeek,
        enabled: true,
        openTime: toTimeInput(rule.openTime),
        closeTime: toTimeInput(rule.closeTime),
      };
    }

    return {
      dayOfWeek,
      enabled: false,
      openTime: DEFAULT_OPEN,
      closeTime: DEFAULT_CLOSE,
    };
  });

  return allDays.length > 0 ? allDays : defaults;
}

export function BusinessAvailabilityEditor() {
  const { run } = useActionLoading();
  const [rows, setRows] = useState<DayRow[]>(buildDefaultRows);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const loadAvailability = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch("/api/business/availability");
      if (!response.ok) {
        throw new Error("Could not load availability.");
      }

      const payload = (await response.json()) as { items: WeeklyAvailabilityRule[] };
      setRows(rulesToRows(payload.items ?? []));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load availability.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadAvailability();
  }, [loadAvailability]);

  async function handleSave(event: React.FormEvent) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setMessage(null);

    const rules = rows
      .filter((row) => row.enabled)
      .map((row) => ({
        dayOfWeek: row.dayOfWeek,
        openTime: toApiTime(row.openTime),
        closeTime: toApiTime(row.closeTime),
      }));

    try {
      await run("Saving weekly hours…", async () => {
        const response = await fetch("/api/business/availability", {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ items: rules }),
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(
            typeof payload.title === "string" ? payload.title : "Could not save availability.",
          );
        }

        setRows(rulesToRows((payload as { items: WeeklyAvailabilityRule[] }).items ?? []));
        setMessage("Weekly hours saved.");
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save availability.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <SkeletonList count={2} />;
  }

  return (
    <form onSubmit={(event) => void handleSave(event)} className="space-y-6">
      {message ? (
        <p className="rounded-lg bg-green-50 px-4 py-3 text-sm text-green-800">{message}</p>
      ) : null}
      {error ? (
        <p className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">{error}</p>
      ) : null}

      <p className="text-sm text-[#1b4332]/70">
        Set the hours customers can book each day. Disabled days have no open slots.
      </p>

      <ul className="divide-y divide-[#1b4332]/10 rounded-xl border border-[#1b4332]/10 bg-white">
        {rows.map((row) => (
          <li key={row.dayOfWeek} className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center">
            <label className="flex min-w-36 items-center gap-2">
              <input
                type="checkbox"
                checked={row.enabled}
                onChange={(event) =>
                  setRows((current) =>
                    current.map((item) =>
                      item.dayOfWeek === row.dayOfWeek
                        ? { ...item, enabled: event.target.checked }
                        : item,
                    ),
                  )
                }
              />
              <span className="font-medium">{DAY_OF_WEEK_LABELS[row.dayOfWeek]}</span>
            </label>

            <div className="flex flex-wrap items-center gap-3">
              <label className="text-sm text-[#1b4332]/70">
                Open
                <input
                  type="time"
                  disabled={!row.enabled}
                  value={row.openTime}
                  onChange={(event) =>
                    setRows((current) =>
                      current.map((item) =>
                        item.dayOfWeek === row.dayOfWeek
                          ? { ...item, openTime: event.target.value }
                          : item,
                      ),
                    )
                  }
                  className="ml-2 rounded-lg border border-[#1b4332]/20 px-2 py-1 disabled:opacity-50"
                />
              </label>
              <label className="text-sm text-[#1b4332]/70">
                Close
                <input
                  type="time"
                  disabled={!row.enabled}
                  value={row.closeTime}
                  onChange={(event) =>
                    setRows((current) =>
                      current.map((item) =>
                        item.dayOfWeek === row.dayOfWeek
                          ? { ...item, closeTime: event.target.value }
                          : item,
                      ),
                    )
                  }
                  className="ml-2 rounded-lg border border-[#1b4332]/20 px-2 py-1 disabled:opacity-50"
                />
              </label>
            </div>
          </li>
        ))}
      </ul>

      <Button type="submit" loading={saving} loadingLabel="Saving…">
        Save weekly hours
      </Button>
    </form>
  );
}
