export type DashboardRangePreset = "today" | "7d" | "30d" | "mtd";

export const DASHBOARD_RANGE_PRESETS: { id: DashboardRangePreset; label: string }[] = [
  { id: "today", label: "Today" },
  { id: "7d", label: "7 days" },
  { id: "30d", label: "30 days" },
  { id: "mtd", label: "Month to date" },
];

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
