import type { Capability } from "./capabilities";
import { hasCapability, resolveCapabilities } from "./capabilities";
import type { DashboardRangePreset } from "./dashboard-range";
import { hasTenantEntitlement, type TenantEntitlementFlag } from "./entitlements";
import type { PortalPermission } from "./portal-permissions";
import type { BusinessProfile, TenantEntitlements } from "./schemas";

/** Stable widget ids for portal dashboard layout (Sprint 32). */
export type DashboardWidgetId =
  | "plan_usage"
  | "kpis_row"
  | "today_schedule"
  | "pending_inbox"
  | "week_appointments"
  | "booking_mix"
  | "collected_revenue"
  | "booked_value"
  | "service_mix"
  | "rating_summary"
  | "queue_now"
  | "messages_unread"
  | "quote_pipeline"
  | "staff_leaderboard"
  | "verification_status"
  | "quick_actions";

export type DashboardLayoutWidget = {
  id: DashboardWidgetId;
  /** Grid column span (12-col layout). */
  w: number;
};

export type DashboardLayout = {
  version: 1;
  range: DashboardRangePreset;
  widgets: DashboardLayoutWidget[];
};

export type DashboardWidgetDefinition = {
  id: DashboardWidgetId;
  title: string;
  requiredEntitlements?: TenantEntitlementFlag[];
  requiredCapabilities?: Capability[];
  requiredPortalPermissions?: PortalPermission[];
  ownerOnly?: boolean;
};

export const DASHBOARD_WIDGET_DEFINITIONS: DashboardWidgetDefinition[] = [
  { id: "plan_usage", title: "Plan & usage", ownerOnly: true },
  { id: "kpis_row", title: "Overview" },
  { id: "today_schedule", title: "Today's schedule", requiredCapabilities: ["calendar"] },
  { id: "pending_inbox", title: "Needs response", requiredCapabilities: ["calendar"], requiredPortalPermissions: ["portal.bookings"] },
  { id: "week_appointments", title: "Appointments over time", requiredCapabilities: ["calendar"] },
  {
    id: "booking_mix",
    title: "Booking mix",
    requiredCapabilities: ["calendar"],
  },
  { id: "collected_revenue", title: "Collected", requiredEntitlements: ["analytics"], requiredCapabilities: ["deposits"], ownerOnly: true },
  { id: "booked_value", title: "Booked value", requiredEntitlements: ["analytics"], ownerOnly: true },
  { id: "service_mix", title: "Top services", requiredEntitlements: ["analytics"], ownerOnly: true },
  { id: "staff_leaderboard", title: "Team bookings", requiredEntitlements: ["analytics"], requiredPortalPermissions: ["portal.staff"], ownerOnly: true },
  { id: "rating_summary", title: "Reputation" },
  { id: "queue_now", title: "Walk-in queue", requiredCapabilities: ["walk_in_queue"] },
  { id: "messages_unread", title: "Unread messages", requiredEntitlements: ["messaging"], requiredPortalPermissions: ["portal.messages"] },
  { id: "quote_pipeline", title: "Open quotes", requiredCapabilities: ["quotes"], requiredPortalPermissions: ["portal.quotes"] },
  { id: "verification_status", title: "Verification", requiredPortalPermissions: ["portal.profile"], ownerOnly: true },
  { id: "quick_actions", title: "Shortcuts" },
];

export type DashboardWidgetVisibilityContext = {
  isPractitionerView: boolean;
  entitlements: TenantEntitlements | null | undefined;
  capabilities: Capability[];
  canPortal: (permission: PortalPermission) => boolean;
};

export function resolveDashboardCapabilities(profile: BusinessProfile | null | undefined): Capability[] {
  if (!profile) {
    return [];
  }
  return resolveCapabilities(profile.businessType, profile.categorySlug, profile.capabilities);
}

/** Default owner layout before per-user persistence (32c). */
export const DEFAULT_OWNER_DASHBOARD_LAYOUT: DashboardLayout = {
  version: 1,
  range: "7d",
  widgets: [
    { id: "plan_usage", w: 12 },
    { id: "kpis_row", w: 12 },
    { id: "week_appointments", w: 8 },
    { id: "today_schedule", w: 4 },
    { id: "booking_mix", w: 6 },
    { id: "service_mix", w: 6 },
  ],
};

export const DEFAULT_PRACTITIONER_DASHBOARD_LAYOUT: DashboardLayout = {
  version: 1,
  range: "today",
  widgets: [
    { id: "kpis_row", w: 12 },
    { id: "today_schedule", w: 12 },
    { id: "week_appointments", w: 12 },
  ],
};

export function resolveDashboardLayout(isPractitionerView: boolean): DashboardLayout {
  return isPractitionerView
    ? DEFAULT_PRACTITIONER_DASHBOARD_LAYOUT
    : DEFAULT_OWNER_DASHBOARD_LAYOUT;
}

export function isDashboardWidgetVisible(
  widgetId: DashboardWidgetId,
  opts: DashboardWidgetVisibilityContext,
): boolean {
  const widget = DASHBOARD_WIDGET_DEFINITIONS.find((entry) => entry.id === widgetId);
  if (!widget) {
    return false;
  }
  if (widget.ownerOnly && opts.isPractitionerView) {
    return false;
  }
  if (widget.requiredEntitlements?.some((key) => !hasTenantEntitlement(opts.entitlements, key))) {
    return false;
  }
  if (widget.requiredCapabilities?.some((capability) => !hasCapability(opts.capabilities, capability))) {
    return false;
  }
  if (widget.requiredPortalPermissions?.some((permission) => !opts.canPortal(permission))) {
    return false;
  }
  return true;
}

export function layoutIncludesWidget(layout: DashboardLayout, widgetId: DashboardWidgetId): boolean {
  return layout.widgets.some((entry) => entry.id === widgetId);
}

export function dashboardWidgetTitle(widgetId: DashboardWidgetId): string {
  return DASHBOARD_WIDGET_DEFINITIONS.find((entry) => entry.id === widgetId)?.title ?? widgetId;
}

const DASHBOARD_WIDGET_IDS = new Set<DashboardWidgetId>(
  DASHBOARD_WIDGET_DEFINITIONS.map((entry) => entry.id),
);

/** Parse stored layout JSON; invalid entries fall back to role default. */
export function coerceDashboardLayout(raw: unknown, fallback: DashboardLayout): DashboardLayout {
  if (!raw || typeof raw !== "object") {
    return cloneDashboardLayout(fallback);
  }
  const candidate = raw as Partial<DashboardLayout>;
  if (candidate.version !== 1 || !Array.isArray(candidate.widgets)) {
    return cloneDashboardLayout(fallback);
  }

  const widgets: DashboardLayoutWidget[] = [];
  const seen = new Set<DashboardWidgetId>();
  for (const entry of candidate.widgets) {
    if (!entry || typeof entry !== "object") {
      continue;
    }
    const id = (entry as DashboardLayoutWidget).id;
    if (!DASHBOARD_WIDGET_IDS.has(id) || seen.has(id)) {
      continue;
    }
    const w = (entry as DashboardLayoutWidget).w;
    widgets.push({
      id,
      w: w === 4 || w === 6 || w === 8 ? w : 12,
    });
    seen.add(id);
  }

  if (widgets.length === 0) {
    return cloneDashboardLayout(fallback);
  }

  const range = candidate.range;
  const allowedRange =
    range === "today" || range === "7d" || range === "30d" || range === "mtd" ? range : fallback.range;

  return { version: 1, range: allowedRange, widgets };
}

export function cloneDashboardLayout(layout: DashboardLayout): DashboardLayout {
  return {
    version: layout.version,
    range: layout.range,
    widgets: layout.widgets.map((entry) => ({ ...entry })),
  };
}

export function removeDashboardWidget(
  layout: DashboardLayout,
  widgetId: DashboardWidgetId,
): DashboardLayout {
  return {
    ...layout,
    widgets: layout.widgets.filter((entry) => entry.id !== widgetId),
  };
}

export function addDashboardWidget(
  layout: DashboardLayout,
  widgetId: DashboardWidgetId,
  width = 12,
): DashboardLayout {
  if (layoutIncludesWidget(layout, widgetId)) {
    return layout;
  }
  return {
    ...layout,
    widgets: [...layout.widgets, { id: widgetId, w: width }],
  };
}

export function reorderDashboardWidgets(
  layout: DashboardLayout,
  previousIndex: number,
  currentIndex: number,
): DashboardLayout {
  const widgets = [...layout.widgets];
  if (
    previousIndex < 0
    || currentIndex < 0
    || previousIndex >= widgets.length
    || currentIndex >= widgets.length
  ) {
    return layout;
  }
  const [moved] = widgets.splice(previousIndex, 1);
  widgets.splice(currentIndex, 0, moved);
  return { ...layout, widgets };
}
