import type { DashboardLayout } from "./dashboard-layout";
import { cloneDashboardLayout, DEFAULT_OWNER_DASHBOARD_LAYOUT, DEFAULT_PRACTITIONER_DASHBOARD_LAYOUT } from "./dashboard-layout";

export type DashboardLayoutPresetId =
  | "owner"
  | "manager"
  | "front_desk"
  | "practitioner"
  | "minimal";

/** Carbon icon names for preset picker cards (must exist in @adeni/ui carbon-icon). */
export const DASHBOARD_LAYOUT_PRESET_ICONS: Record<DashboardLayoutPresetId, string> = {
  owner: "chart--line",
  manager: "task",
  front_desk: "screen",
  practitioner: "user--avatar",
  minimal: "template",
};

export type DashboardLayoutPreset = {
  id: DashboardLayoutPresetId;
  label: string;
  /** Short role hook on preset cards. */
  focus: string;
  description: string;
  layout: DashboardLayout;
};

const MANAGER_DASHBOARD_LAYOUT: DashboardLayout = {
  version: 1,
  range: "7d",
  widgets: [
    { id: "plan_usage", w: 12 },
    { id: "kpis_row", w: 12 },
    { id: "pending_inbox", w: 4 },
    { id: "messages_unread", w: 4 },
    { id: "quote_pipeline", w: 4 },
    { id: "week_appointments", w: 6 },
    { id: "today_schedule", w: 6 },
    { id: "staff_leaderboard", w: 12 },
  ],
};

const MINIMAL_DASHBOARD_LAYOUT: DashboardLayout = {
  version: 1,
  range: "today",
  widgets: [{ id: "kpis_row", w: 12 }],
};

const FRONT_DESK_DASHBOARD_LAYOUT: DashboardLayout = {
  version: 1,
  range: "today",
  widgets: [
    { id: "pending_inbox", w: 6 },
    { id: "messages_unread", w: 6 },
    { id: "queue_now", w: 6 },
    { id: "today_schedule", w: 6 },
    { id: "quote_pipeline", w: 12 },
  ],
};

export const DASHBOARD_LAYOUT_PRESETS: DashboardLayoutPreset[] = [
  {
    id: "owner",
    label: "Owner",
    focus: "Growth & plan health",
    description: "Plan usage, KPIs, charts, and service mix for the week.",
    layout: DEFAULT_OWNER_DASHBOARD_LAYOUT,
  },
  {
    id: "manager",
    label: "Manager",
    focus: "Inbox & team load",
    description: "Pending work, messaging, quotes, and staff leaderboard.",
    layout: MANAGER_DASHBOARD_LAYOUT,
  },
  {
    id: "front_desk",
    label: "Front desk",
    focus: "Today on the floor",
    description: "Live queue, today’s chair, and fast replies.",
    layout: FRONT_DESK_DASHBOARD_LAYOUT,
  },
  {
    id: "practitioner",
    label: "Practitioner",
    focus: "My chair today",
    description: "Personal KPIs and schedule — no owner-only tiles.",
    layout: DEFAULT_PRACTITIONER_DASHBOARD_LAYOUT,
  },
  {
    id: "minimal",
    label: "Minimal",
    focus: "Clean slate",
    description: "Overview KPIs only — add widgets as you go.",
    layout: MINIMAL_DASHBOARD_LAYOUT,
  },
];

export function resolveDashboardLayoutPreset(presetId: DashboardLayoutPresetId): DashboardLayout {
  const preset = DASHBOARD_LAYOUT_PRESETS.find((entry) => entry.id === presetId);
  return cloneDashboardLayout(preset?.layout ?? DEFAULT_OWNER_DASHBOARD_LAYOUT);
}
