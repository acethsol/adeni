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
  invoicesReceipts: boolean;
  onlinePayments: boolean;
  deposits: boolean;
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
};

export type PlanEntitlementsCatalog = Record<SubscriptionSku, PlanEntitlementSeed>;

export const PLAN_ENTITLEMENTS = catalog as PlanEntitlementsCatalog;

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
  { label: "Basic reminders", kind: "flag", key: "basicReminders" },
  { label: "Configurable reminders", kind: "flag", key: "configurableReminders" },
  { label: "Follow-up automation", kind: "flag", key: "followUpAutomation" },
  { label: "SMS notifications", kind: "flag", key: "smsEnabled" },
  { label: "Basic analytics", kind: "flag", key: "basicAnalytics" },
  { label: "Advanced analytics", kind: "flag", key: "advancedAnalytics" },
  { label: "Customer retention analytics", kind: "flag", key: "customerRetentionAnalytics" },
  { label: "Staff performance analytics", kind: "flag", key: "staffPerformanceAnalytics" },
  { label: "Basic exports", kind: "flag", key: "basicExports" },
  { label: "Advanced reports", kind: "flag", key: "advancedReports" },
  { label: "Excel export", kind: "flag", key: "excelExport" },
  { label: "Waitlist", kind: "flag", key: "waitlist" },
  { label: "Multi-location reporting", kind: "flag", key: "multiLocationReporting" },
  { label: "Advanced permissions", kind: "flag", key: "advancedPermissions" },
  { label: "Priority support", kind: "flag", key: "prioritySupport" },
  { label: "Online payments", kind: "flag", key: "onlinePayments" },
  { label: "Deposits", kind: "flag", key: "deposits" },
  { label: "Invoices & receipts", kind: "flag", key: "invoicesReceipts" },
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
