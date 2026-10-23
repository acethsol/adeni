import { t } from "./i18n/index";
import { defaultLocale } from "./i18n/types";

export type DashboardRangePreset = "today" | "7d" | "30d" | "mtd";

export const DASHBOARD_RANGE_PRESETS: { id: DashboardRangePreset; label: string }[] = [
  { id: "today", label: "Today" },
  { id: "7d", label: "7 days" },
  { id: "30d", label: "30 days" },
  { id: "mtd", label: "Month to date" },
];

export function formatDashboardRangePresetLabel(
  locale: string | undefined,
  preset: DashboardRangePreset,
): string {
  const loc = locale ?? defaultLocale;
  const key = `portal.dashboard.rangePreset.${preset}`;
  const translated = t(loc, key);
  if (translated !== key) {
    return translated;
  }
  return DASHBOARD_RANGE_PRESETS.find((entry) => entry.id === preset)?.label ?? t(loc, "portal.dashboard.range");
}

/** UTC range [from, to) for dashboard aggregate APIs. */
export function resolveDashboardRange(
  preset: DashboardRangePreset,
  now = new Date(),
): { from: string; to: string } {
  const end = new Date(now);
  end.setUTCHours(0, 0, 0, 0);
  end.setUTCDate(end.getUTCDate() + 1);

  const start = new Date(end);
  switch (preset) {
    case "today":
      start.setUTCDate(end.getUTCDate() - 1);
      break;
    case "30d":
      start.setUTCDate(end.getUTCDate() - 30);
      break;
    case "mtd":
      start.setUTCDate(1);
      break;
    case "7d":
    default:
      start.setUTCDate(end.getUTCDate() - 7);
      break;
  }

  return { from: start.toISOString(), to: end.toISOString() };
}

/** Inclusive calendar span for the active preset (UTC-aligned with resolveDashboardRange). */
export function formatDashboardRangeDateSpan(
  preset: DashboardRangePreset,
  now = new Date(),
  locale = "en-CA",
): string {
  const { from, to } = resolveDashboardRange(preset, now);
  const fromDate = new Date(from);
  const exclusiveEnd = new Date(to);
  const inclusiveEnd = new Date(exclusiveEnd);
  inclusiveEnd.setUTCDate(inclusiveEnd.getUTCDate() - 1);

  const utc: Intl.DateTimeFormatOptions = { timeZone: "UTC" };

  if (preset === "today") {
    return new Intl.DateTimeFormat(locale, {
      ...utc,
      weekday: "long",
      month: "short",
      day: "numeric",
    }).format(fromDate);
  }

  const monthDay = new Intl.DateTimeFormat(locale, { ...utc, month: "short", day: "numeric" });
  const monthDayYear = new Intl.DateTimeFormat(locale, {
    ...utc,
    month: "short",
    day: "numeric",
    year: "numeric",
  });
  const dayOnly = new Intl.DateTimeFormat(locale, { ...utc, day: "numeric" });

  const fromLabel = monthDay.format(fromDate);
  const sameMonth =
    fromDate.getUTCFullYear() === inclusiveEnd.getUTCFullYear()
    && fromDate.getUTCMonth() === inclusiveEnd.getUTCMonth();
  const toLabel = sameMonth
    ? dayOnly.format(inclusiveEnd)
    : fromDate.getUTCFullYear() === inclusiveEnd.getUTCFullYear()
      ? monthDay.format(inclusiveEnd)
      : monthDayYear.format(inclusiveEnd);

  if (fromLabel === toLabel || (sameMonth && fromDate.getUTCDate() === inclusiveEnd.getUTCDate())) {
    return fromLabel;
  }

  return sameMonth ? `${fromLabel}–${toLabel}` : `${fromLabel}–${toLabel}`;
}

/** Overview widget + chart hints — names the preset and the concrete dates. */
export function formatDashboardRangeOverviewHint(
  preset: DashboardRangePreset,
  now = new Date(),
  locale: string = defaultLocale,
): string {
  const dateLocale = locale === "fr" ? "fr-CA" : "en-CA";
  const span = formatDashboardRangeDateSpan(preset, now, dateLocale);
  const presetLabel = formatDashboardRangePresetLabel(locale, preset);
  return `${presetLabel} · ${span}`;
}

/** Short phrase for stat subtitles, e.g. "Oct 14–21". */
export function formatDashboardRangeStatHint(
  preset: DashboardRangePreset,
  now = new Date(),
  locale: string = defaultLocale,
): string {
  const dateLocale = locale === "fr" ? "fr-CA" : "en-CA";
  if (preset === "today") {
    return formatDashboardRangePresetLabel(locale, "today");
  }
  return formatDashboardRangeDateSpan(preset, now, dateLocale);
}
