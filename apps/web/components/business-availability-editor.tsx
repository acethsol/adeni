"use client";

import { useCallback, useEffect, useMemo, useRef, useState, type PointerEvent as ReactPointerEvent } from "react";
import { CalendarRange, Copy, RotateCcw, X } from "lucide-react";
import { DAY_OF_WEEK_LABELS, type WeeklyAvailabilityRule } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { SkeletonList } from "@/components/ui/skeleton";
import { useActionLoading } from "@/contexts/action-loading-context";
import { useToast } from "@/contexts/toast-context";
import { useUnsavedChangesGuard } from "@/contexts/navigation-guard-context";
import { useApiErrorMessage } from "@/lib/api-error";

type TimeBlock = { openTime: string; closeTime: string };

type DayRow = {
  dayOfWeek: number;
  blocks: TimeBlock[];
};

const DAYS_ORDER = [1, 2, 3, 4, 5, 6, 0];
const START_HOUR = 6;
const END_HOUR = 23;
const TOTAL_SLOTS = (END_HOUR - START_HOUR) * 2;
const SLOT_HEIGHT = 16;

function buildDefaultRows(): DayRow[] {
  return DAYS_ORDER.map((dayOfWeek) => ({
    dayOfWeek,
    blocks: dayOfWeek === 0 ? [] : [{ openTime: "09:00", closeTime: "17:00" }],
  }));
}

function toTimeInput(value: string): string {
  return value.slice(0, 5);
}

function toApiTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

function slotToTime(slot: number): string {
  const totalMinutes = START_HOUR * 60 + slot * 30;
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
}

function timeToSlot(value: string): number {
  const [hh, mm] = toTimeInput(value).split(":").map(Number);
  const slot = ((hh - START_HOUR) * 60 + mm) / 30;
  return Math.min(TOTAL_SLOTS, Math.max(0, Math.round(slot)));
}

function formatTime12h(value: string): string {
  const [hh, mm] = toTimeInput(value).split(":").map(Number);
  const period = hh >= 12 ? "PM" : "AM";
  const hour12 = hh % 12 === 0 ? 12 : hh % 12;
  return mm === 0 ? `${hour12} ${period}` : `${hour12}:${String(mm).padStart(2, "0")} ${period}`;
}

function sortBlocks(blocks: TimeBlock[]): TimeBlock[] {
  return [...blocks].sort((a, b) => timeToSlot(a.openTime) - timeToSlot(b.openTime));
}

/** Merges overlapping/adjacent blocks (in slot space) so a day never shows duplicate ranges. */
function mergeSlotBlocks(blocks: { open: number; close: number }[]): { open: number; close: number }[] {
  const sorted = [...blocks].sort((a, b) => a.open - b.open);
  const merged: { open: number; close: number }[] = [];
  for (const block of sorted) {
    const last = merged[merged.length - 1];
    if (last && block.open <= last.close) {
      last.close = Math.max(last.close, block.close);
    } else {
      merged.push({ ...block });
    }
  }
  return merged;
}

function rulesToRows(rules: WeeklyAvailabilityRule[]): DayRow[] {
  return DAYS_ORDER.map((dayOfWeek) => ({
    dayOfWeek,
    blocks: sortBlocks(
      rules
        .filter((item) => item.dayOfWeek === dayOfWeek)
        .map((rule) => ({ openTime: toTimeInput(rule.openTime), closeTime: toTimeInput(rule.closeTime) })),
    ),
  }));
}

function rowsToRules(rows: DayRow[]): WeeklyAvailabilityRule[] {
  return rows.flatMap((row) =>
    row.blocks.map((block) => ({
      dayOfWeek: row.dayOfWeek,
      openTime: toApiTime(block.openTime),
      closeTime: toApiTime(block.closeTime),
    })),
  );
}

function rowsSignature(rows: DayRow[]): string {
  return DAYS_ORDER.map((day) => {
    const row = rows.find((item) => item.dayOfWeek === day);
    return `${day}:${sortBlocks(row?.blocks ?? []).map((b) => `${b.openTime}-${b.closeTime}`).join(",")}`;
  }).join("|");
}

const HOUR_MARKS = Array.from({ length: END_HOUR - START_HOUR + 1 }, (_, index) => START_HOUR + index);

function formatHourLabel(hour: number): string {
  if (hour === 0 || hour === 24) return "12 AM";
  if (hour === 12) return "12 PM";
  return hour > 12 ? `${hour - 12} PM` : `${hour} AM`;
}

export function BusinessAvailabilityEditor() {
  const { run } = useActionLoading();
  const toast = useToast();
  const { formatApiError } = useApiErrorMessage();
  const [rows, setRows] = useState<DayRow[]>(buildDefaultRows);
  const [savedSignature, setSavedSignature] = useState(() => rowsSignature(buildDefaultRows()));
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dragPreview, setDragPreview] = useState<{ day: number; open: number; close: number } | null>(null);
  const dragRef = useRef<{ day: number; anchor: number; rectTop: number; rectHeight: number } | null>(null);

  const isDirty = useMemo(() => rowsSignature(rows) !== savedSignature, [rows, savedSignature]);
  useUnsavedChangesGuard(isDirty);

  const loadAvailability = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch("/api/business/availability");
      if (!response.ok) {
        throw new Error("Could not load availability.");
      }

      const payload = (await response.json()) as { items: WeeklyAvailabilityRule[] };
      const nextRows = rulesToRows(payload.items ?? []);
      setRows(nextRows);
      setSavedSignature(rowsSignature(nextRows));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load availability.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadAvailability();
  }, [loadAvailability]);

  async function handleSave() {
    if (!isDirty) return;
    setSaving(true);
    setError(null);

    const rules = rowsToRules(rows);

    try {
      await run("Saving weekly hours…", async () => {
        const response = await fetch("/api/business/availability", {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ items: rules }),
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(formatApiError(payload, "Could not save availability."));
        }

        const nextRows = rulesToRows((payload as { items: WeeklyAvailabilityRule[] }).items ?? []);
        setRows(nextRows);
        setSavedSignature(rowsSignature(nextRows));
        toast.success("Weekly hours saved");
      });
    } catch (err) {
      const messageText = err instanceof Error ? err.message : "Could not save availability.";
      setError(messageText);
      toast.error(messageText);
    } finally {
      setSaving(false);
    }
  }

  function slotFromClientY(clientY: number, rectTop: number, rectHeight: number) {
    const ratio = (clientY - rectTop) / rectHeight;
    return Math.min(TOTAL_SLOTS, Math.max(0, Math.round(ratio * TOTAL_SLOTS)));
  }

  function handlePointerDown(day: number, event: ReactPointerEvent<HTMLDivElement>) {
    event.preventDefault();
    const rect = event.currentTarget.getBoundingClientRect();
    const startSlot = slotFromClientY(event.clientY, rect.top, rect.height);
    dragRef.current = { day, anchor: startSlot, rectTop: rect.top, rectHeight: rect.height };
    setDragPreview({ day, open: startSlot, close: Math.min(TOTAL_SLOTS, startSlot + 1) });

    const handleMove = (moveEvent: PointerEvent) => {
      const drag = dragRef.current;
      if (!drag) return;
      const slot = slotFromClientY(moveEvent.clientY, drag.rectTop, drag.rectHeight);
      setDragPreview({ day: drag.day, open: Math.min(drag.anchor, slot), close: Math.max(drag.anchor, slot) + 1 });
    };

    const handleUp = () => {
      const drag = dragRef.current;
      if (drag) {
        setDragPreview((preview) => {
          if (preview) {
            setRows((current) =>
              current.map((row) => {
                if (row.dayOfWeek !== drag.day) return row;
                const existing = row.blocks.map((block) => ({
                  open: timeToSlot(block.openTime),
                  close: timeToSlot(block.closeTime),
                }));
                const merged = mergeSlotBlocks([...existing, { open: preview.open, close: preview.close }]);
                return {
                  ...row,
                  blocks: merged.map((block) => ({
                    openTime: slotToTime(block.open),
                    closeTime: slotToTime(block.close),
                  })),
                };
              }),
            );
          }
          return null;
        });
        dragRef.current = null;
      }
      window.removeEventListener("pointermove", handleMove);
      window.removeEventListener("pointerup", handleUp);
    };

    window.addEventListener("pointermove", handleMove);
    window.addEventListener("pointerup", handleUp);
  }

  function removeBlock(day: number, blockIndex: number, event: React.MouseEvent) {
    event.stopPropagation();
    setRows((current) =>
      current.map((row) =>
        row.dayOfWeek === day
          ? { ...row, blocks: row.blocks.filter((_, index) => index !== blockIndex) }
          : row,
      ),
    );
  }

  function copyMondayToWeekdays() {
    const monday = rows.find((row) => row.dayOfWeek === 1);
    if (!monday) return;
    setRows((current) =>
      current.map((row) =>
        [2, 3, 4, 5].includes(row.dayOfWeek)
          ? { ...row, blocks: monday.blocks.map((block) => ({ ...block })) }
          : row,
      ),
    );
    toast.info("Copied Monday's hours to Tue–Fri");
  }

  function clearAll() {
    setRows((current) => current.map((row) => ({ ...row, blocks: [] })));
  }

  if (loading) {
    return <SkeletonList count={2} />;
  }

  return (
    <div className="space-y-6">
      {error ? <Callout tone="error">{error}</Callout> : null}

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="flex items-center gap-2 text-sm text-muted">
          <CalendarRange className="h-4 w-4 shrink-0 text-accent" aria-hidden />
          Click and drag to add open hours. Drag again on the same day to add a break — e.g. 9–12, then 2–5.
        </p>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" size="sm" onClick={copyMondayToWeekdays} className="gap-1.5">
            <Copy className="h-3.5 w-3.5" aria-hidden />
            Copy Mon → Tue–Fri
          </Button>
          <Button variant="secondary" size="sm" onClick={clearAll} className="gap-1.5">
            <RotateCcw className="h-3.5 w-3.5" aria-hidden />
            Clear all
          </Button>
        </div>
      </div>

      <div className="overflow-hidden rounded-2xl border border-border bg-surface shadow-sm">
        <div className="grid" style={{ gridTemplateColumns: `56px repeat(7, minmax(0, 1fr))` }}>
          <div className="border-b border-border" />
          {DAYS_ORDER.map((day) => {
            const row = rows.find((item) => item.dayOfWeek === day);
            return (
              <div
                key={day}
                className="border-b border-l border-border px-2 py-3 text-center"
              >
                <p className="text-xs font-bold uppercase tracking-wide text-foreground">
                  {DAY_OF_WEEK_LABELS[day].slice(0, 3)}
                </p>
                {!row?.blocks.length && !(dragPreview?.day === day) ? (
                  <p className="mt-0.5 text-[11px] font-medium text-muted-foreground">Closed</p>
                ) : null}
              </div>
            );
          })}
        </div>

        <div className="relative flex" style={{ height: TOTAL_SLOTS * SLOT_HEIGHT }}>
          <div className="relative w-14 shrink-0 border-r border-border">
            {HOUR_MARKS.map((hour) => (
              <span
                key={hour}
                className="absolute right-2 -translate-y-1/2 text-[11px] font-medium text-muted-foreground"
                style={{ top: (hour - START_HOUR) * 2 * SLOT_HEIGHT }}
              >
                {formatHourLabel(hour)}
              </span>
            ))}
          </div>

          {DAYS_ORDER.map((day) => {
            const row = rows.find((item) => item.dayOfWeek === day);
            const isDragging = dragPreview?.day === day;
            const blocks = (row?.blocks ?? []).map((block) => ({
              open: timeToSlot(block.openTime),
              close: timeToSlot(block.closeTime),
            }));

            return (
              <div
                key={day}
                onPointerDown={(event) => handlePointerDown(day, event)}
                className="relative flex-1 cursor-crosshair select-none border-r border-border last:border-r-0"
                style={{ touchAction: "none" }}
              >
                {HOUR_MARKS.map((hour) => (
                  <div
                    key={hour}
                    className="absolute inset-x-0 border-t border-border/60"
                    style={{ top: (hour - START_HOUR) * 2 * SLOT_HEIGHT }}
                    aria-hidden
                  />
                ))}

                {blocks.map((block, index) => (
                  <div
                    key={`${day}-${index}`}
                    className="group absolute inset-x-1 flex flex-col items-center justify-center overflow-hidden rounded-lg border border-accent/40 bg-accent/15 px-1 text-center transition-colors hover:border-accent/70 hover:bg-accent/20"
                    style={{
                      top: block.open * SLOT_HEIGHT,
                      height: Math.max(SLOT_HEIGHT, (block.close - block.open) * SLOT_HEIGHT),
                    }}
                  >
                    {block.close - block.open >= 3 ? (
                      <p className="pointer-events-none text-[11px] font-semibold leading-tight text-accent">
                        {formatTime12h(slotToTime(block.open))}
                        <br />– {formatTime12h(slotToTime(block.close))}
                      </p>
                    ) : block.close - block.open >= 2 ? (
                      <p className="pointer-events-none whitespace-nowrap text-[10px] font-semibold leading-tight text-accent">
                        {formatTime12h(slotToTime(block.open))}
                      </p>
                    ) : null}
                    <button
                      type="button"
                      onClick={(event) => removeBlock(day, index, event)}
                      aria-label={`Remove ${DAY_OF_WEEK_LABELS[day]} block ${formatTime12h(
                        slotToTime(block.open),
                      )}–${formatTime12h(slotToTime(block.close))}`}
                      className="absolute right-0.5 top-0.5 rounded-full p-0.5 text-accent opacity-0 transition-opacity hover:bg-accent/20 group-hover:opacity-100"
                    >
                      <X className="h-3 w-3" aria-hidden />
                    </button>
                  </div>
                ))}

                {isDragging ? (
                  <div
                    className="pointer-events-none absolute inset-x-1 flex flex-col items-center justify-center overflow-hidden rounded-lg border border-accent bg-accent/25 px-1 text-center"
                    style={{
                      top: dragPreview.open * SLOT_HEIGHT,
                      height: Math.max(SLOT_HEIGHT, (dragPreview.close - dragPreview.open) * SLOT_HEIGHT),
                    }}
                  >
                    {dragPreview.close - dragPreview.open >= 3 ? (
                      <p className="text-[11px] font-semibold leading-tight text-accent">
                        {formatTime12h(slotToTime(dragPreview.open))}
                        <br />– {formatTime12h(slotToTime(dragPreview.close))}
                      </p>
                    ) : null}
                  </div>
                ) : null}
              </div>
            );
          })}
        </div>
      </div>

      <div className="flex justify-end">
        <Button onClick={() => void handleSave()} loading={saving} loadingLabel="Saving…" disabled={!isDirty}>
          Save weekly hours
        </Button>
      </div>
    </div>
  );
}
