import type { IntakeStatus } from "@adeni/shared";
import type { AdeniPillTone } from "@adeni/ui";

export function intakeFormsForBooking(
  rows: Record<string, IntakeStatus[]>,
  bookingId: string,
): IntakeStatus[] | undefined {
  if (!(bookingId in rows)) {
    return undefined;
  }
  return rows[bookingId] ?? [];
}

export function intakePillTone(forms: readonly IntakeStatus[]): AdeniPillTone {
  if (forms.length === 0) {
    return "neutral";
  }
  if (forms.some((form) => form.state === "pending")) {
    return "warning";
  }
  if (forms.every((form) => form.state === "completed")) {
    return "success";
  }
  if (forms.some((form) => form.state === "closedUncompleted")) {
    return "danger";
  }
  return "info";
}

export function intakePrimaryState(forms: readonly IntakeStatus[]): string | null {
  const pending = forms.find((form) => form.state === "pending");
  if (pending) {
    return pending.state;
  }
  const completed = forms.find((form) => form.state === "completed");
  if (completed) {
    return completed.state;
  }
  return forms[0]?.state ?? null;
}
