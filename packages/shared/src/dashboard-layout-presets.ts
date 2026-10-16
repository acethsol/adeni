import type { DashboardLayout } from "./dashboard-layout";
import { cloneDashboardLayout, DEFAULT_OWNER_DASHBOARD_LAYOUT, DEFAULT_PRACTITIONER_DASHBOARD_LAYOUT } from "./dashboard-layout";

export type DashboardLayoutPresetId = "owner" | "manager" | "front_desk" | "practitioner";

export type DashboardLayoutPreset = {
  id: DashboardLayoutPresetId;
  label: string;
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
    { id: "week_appointments", w: 8 },
    { id: "today_schedule", w: 4 },
    { id: "staff_leaderboard", w: 12 },
    { id: "quick_actions", w: 12 },
  ],
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
    { id: "quick_actions", w: 12 },
  ],
};

export const DASHBOARD_LAYOUT_PRESETS: DashboardLayoutPreset[] = [
  {
    id: "owner",
    label: "Owner",
    description: "Plan, revenue signals, and floor overview.",
    layout: DEFAULT_OWNER_DASHBOARD_LAYOUT,
  },
  {
    id: "manager",
    label: "Manager",
    description: "Inbox, messaging, quotes, and team load.",
    layout: MANAGER_DASHBOARD_LAYOUT,
  },
  {
    id: "front_desk",
    label: "Front desk",
    description: "Today’s chair, queue, and quick replies.",
    layout: FRONT_DESK_DASHBOARD_LAYOUT,
  },
  {
    id: "practitioner",
    label: "Practitioner",
    description: "Personal schedule and shortcuts.",
    layout: DEFAULT_PRACTITIONER_DASHBOARD_LAYOUT,
  },
];

export function resolveDashboardLayoutPreset(presetId: DashboardLayoutPresetId): DashboardLayout {
  const preset = DASHBOARD_LAYOUT_PRESETS.find((entry) => entry.id === presetId);
  return cloneDashboardLayout(preset?.layout ?? DEFAULT_OWNER_DASHBOARD_LAYOUT);
}
