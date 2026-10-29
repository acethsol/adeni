import catalog from "./data/plan-entitlements.json";
import type { SubscriptionSku } from "./country-pricing";

export type PlanEntitlementSeed = {
  maxLocations: number | null;
  maxPractitioners: number | null;
  additionalLocationsAllowed: boolean;
  additionalPractitionersAllowed: boolean;
  unlimitedServices: boolean;
  unlimitedBookings: boolean;
  publicProfile: boolean;
  discoverListing: boolean;
  messaging: boolean;
  basicReminders: boolean;
  configurableReminders: boolean;
  followUpAutomation: boolean;
  smsEnabled: boolean;
  smsUsageBilling: boolean;
  basicAnalytics: boolean;
  advancedAnalytics: boolean;
  customerRetentionAnalytics: boolean;
  staffPerformanceAnalytics: boolean;
  basicExports: boolean;
  advancedReports: boolean;
  excelExport: boolean;
  waitlist: boolean;
  multiLocationReporting: boolean;
  advancedPermissions: boolean;
  prioritySupport: boolean;
  /** Sprint 37.1 — prepaid packages & benefit ledger (Retention). */
  packages: boolean;
  /** Sprint 37.2 — membership plans & recurring memberships (Retention). */
  memberships: boolean;
};

export type PlanEntitlementsCatalog = Record<SubscriptionSku, PlanEntitlementSeed>;

export const PLAN_ENTITLEMENTS: PlanEntitlementsCatalog = catalog;

export function getPlanEntitlements(sku: SubscriptionSku): PlanEntitlementSeed {
  return PLAN_ENTITLEMENTS[sku];
}

export function planAllowsMultipleLocations(seed: PlanEntitlementSeed): boolean {
  return seed.maxLocations == null || seed.maxLocations > 1;
}

/** Legacy API flag: true when configurable or follow-up reminder automation is included. */
export function legacyAdvancedReminders(seed: PlanEntitlementSeed): boolean {
  return seed.configurableReminders || seed.followUpAutomation;
}

export type PlanFeature = {
  label: string;
  starter: string | boolean;
  professional: string | boolean;
  business: string | boolean;
};

type PlanComparisonRowDef =
  | { label: string; kind: "limit"; key: "maxLocations" | "maxPractitioners" }
  | { label: string; kind: "flag"; key: keyof PlanEntitlementSeed };

export const PLAN_COMPARISON_ROW_DEFS: PlanComparisonRowDef[] = [
  { label: "Bookings per month", kind: "flag", key: "unlimitedBookings" },
  { label: "Services", kind: "flag", key: "unlimitedServices" },
  { label: "Public profile", kind: "flag", key: "publicProfile" },
  { label: "Discover listing", kind: "flag", key: "discoverListing" },
  { label: "Adeni messaging", kind: "flag", key: "messaging" },
  { label: "Team members", kind: "limit", key: "maxPractitioners" },
  { label: "Business locations", kind: "limit", key: "maxLocations" },
  { label: "SMS notifications", kind: "flag", key: "smsEnabled" },
  { label: "Advanced analytics", kind: "flag", key: "advancedAnalytics" },
  { label: "Customer retention analytics", kind: "flag", key: "customerRetentionAnalytics" },
  { label: "Staff performance analytics", kind: "flag", key: "staffPerformanceAnalytics" },
  { label: "Waitlist", kind: "flag", key: "waitlist" },
  { label: "Advanced permissions", kind: "flag", key: "advancedPermissions" },
  { label: "Priority support", kind: "flag", key: "prioritySupport" },
];

/** Full seed matrix for admin ops (includes deferred module keys). */
export type PlanAdminMatrixRowDef = {
  key: keyof PlanEntitlementSeed;
  label: string;
  kind: "limit" | "flag";
  adminNote?: string;
};

export const PLAN_ADMIN_MATRIX_ROW_DEFS: PlanAdminMatrixRowDef[] = [
  { key: "maxLocations", label: "Business locations", kind: "limit" },
  { key: "maxPractitioners", label: "Team / practitioners", kind: "limit" },
  { key: "additionalLocationsAllowed", label: "Additional locations (sales)", kind: "flag" },
  { key: "additionalPractitionersAllowed", label: "Additional practitioners (sales)", kind: "flag" },
  { key: "unlimitedBookings", label: "Unlimited bookings", kind: "flag" },
  { key: "unlimitedServices", label: "Unlimited services", kind: "flag" },
  { key: "publicProfile", label: "Public profile", kind: "flag" },
  { key: "discoverListing", label: "Discover listing", kind: "flag" },
  { key: "messaging", label: "Adeni messaging", kind: "flag" },
  {
    key: "basicReminders",
    label: "Basic reminders",
    kind: "flag",
    adminNote: "Deferred — Sprint 38 Notifications",
  },
  {
    key: "configurableReminders",
    label: "Configurable reminders",
    kind: "flag",
    adminNote: "Deferred — Sprint 38 Notifications",
  },
  {
    key: "followUpAutomation",
    label: "Follow-up automation",
    kind: "flag",
    adminNote: "Deferred — Sprint 38 Notifications",
  },
  { key: "smsEnabled", label: "SMS notifications", kind: "flag" },
  {
    key: "smsUsageBilling",
    label: "SMS usage billing",
    kind: "flag",
    adminNote: "Deferred — no metered SMS billing",
  },
  { key: "basicAnalytics", label: "Basic analytics", kind: "flag" },
  { key: "advancedAnalytics", label: "Advanced analytics", kind: "flag" },
  { key: "customerRetentionAnalytics", label: "Customer retention analytics", kind: "flag" },
  { key: "staffPerformanceAnalytics", label: "Staff performance analytics", kind: "flag" },
  {
    key: "basicExports",
    label: "Basic exports",
    kind: "flag",
    adminNote: "Deferred — Sprint 39 Reporting",
  },
  {
    key: "advancedReports",
    label: "Advanced reports",
    kind: "flag",
    adminNote: "Deferred — Sprint 39 Reporting",
  },
  {
    key: "excelExport",
    label: "Excel export",
    kind: "flag",
    adminNote: "Deferred — Sprint 39 Reporting",
  },
  { key: "waitlist", label: "Waitlist", kind: "flag" },
  {
    key: "multiLocationReporting",
    label: "Multi-location reporting",
    kind: "flag",
    adminNote: "Deferred — Sprint 39 Reporting",
  },
  { key: "advancedPermissions", label: "Advanced permissions", kind: "flag" },
  { key: "prioritySupport", label: "Priority support", kind: "flag" },
  { key: "packages", label: "Prepaid packages", kind: "flag" },
  { key: "memberships", label: "Memberships", kind: "flag" },
];

function formatPlanLimit(key: "maxLocations" | "maxPractitioners", value: number | null): string {
  if (value == null) {
    return key === "maxLocations" ? "Multiple" : "Unlimited";
  }
  if (key === "maxPractitioners") {
    return `Up to ${value}`;
  }
  return String(value);
}

function formatPlanComparisonCell(
  sku: SubscriptionSku,
  def: PlanComparisonRowDef,
): string | boolean {
  const seed = PLAN_ENTITLEMENTS[sku];
  if (def.kind === "limit") {
    return formatPlanLimit(def.key, seed[def.key]);
  }
  const flag = seed[def.key];
  if (typeof flag !== "boolean") {
    return "—";
  }
  if (def.key === "unlimitedBookings" || def.key === "unlimitedServices") {
    return flag ? "Unlimited" : "—";
  }
  return flag;
}

export function buildPlanComparison(): PlanFeature[] {
  return PLAN_COMPARISON_ROW_DEFS.map((def) => ({
    label: def.label,
    starter: formatPlanComparisonCell("starter", def),
    professional: formatPlanComparisonCell("professional", def),
    business: formatPlanComparisonCell("business", def),
  }));
}

export const PLAN_COMPARISON = buildPlanComparison();

export function planComparisonCell(row: PlanFeature, sku: SubscriptionSku): string {
  const value = row[sku];
  if (value === true) return "Yes";
  if (value === false) return "—";
  return value;
}
