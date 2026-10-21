import type { SubscriptionUsage, TenantBillingStatus } from "./schemas";
import { PLAN_COMPARISON, planComparisonCell } from "./plan-entitlements";

export type { PlanFeature } from "./plan-entitlements";
export { PLAN_COMPARISON, planComparisonCell };

export function isNearBookingLimit(usage: SubscriptionUsage, threshold = 0.8): boolean {
  if (usage.bookingsLimitThisMonth == null) return false;
  if (usage.bookingsLimitThisMonth === 0) return false;
  return usage.bookingsUsedThisMonth / usage.bookingsLimitThisMonth >= threshold;
}

const BILLING_STATUS_LABELS: Record<TenantBillingStatus, string> = {
  trialing: "Trial",
  active: "Active",
  past_due: "Past due",
  suspended: "Suspended",
  canceled: "Canceled",
};

export function formatTenantBillingStatus(status: TenantBillingStatus | undefined): string {
  if (!status) return "—";
  return BILLING_STATUS_LABELS[status] ?? status;
}

export function formatBillingChargeStatus(status: string): string {
  const normalized = status.trim().toLowerCase();
  if (normalized === "active" || normalized === "paid" || normalized === "success") {
    return "Paid";
  }
  if (normalized === "past_due" || normalized === "failed") {
    return "Failed";
  }
  return status;
}

export function formatBookingUsage(usage: SubscriptionUsage): string {
  if (usage.bookingsLimitThisMonth == null) {
    return `${usage.bookingsUsedThisMonth} bookings this month`;
  }
  return `${usage.bookingsUsedThisMonth} / ${usage.bookingsLimitThisMonth} bookings this month`;
}

/** Compact metric line for plan usage rows (bookings omit "this month" — heading provides context). */
export function formatBookingUsageMetric(usage: SubscriptionUsage): string {
  if (usage.bookingsLimitThisMonth == null) {
    return String(usage.bookingsUsedThisMonth);
  }
  return `${usage.bookingsUsedThisMonth} / ${usage.bookingsLimitThisMonth}`;
}

export function formatResourceUsageMetric(
  active: number,
  max: number | null | undefined,
  labelPlural: string,
  labelSingular?: string,
): string {
  const singular = labelSingular ?? labelPlural.replace(/s$/, "");
  const unit = active === 1 ? singular : labelPlural;
  if (max == null) {
    return `${active} ${unit}`;
  }
  return `${active} / ${max} ${labelPlural}`;
}

export function formatLocationUsageMetric(usage: SubscriptionUsage): string {
  return formatResourceUsageMetric(
    usage.locationsActive,
    usage.entitlements.maxLocations,
    "locations",
    "location",
  );
}

export function formatTeamUsageMetric(usage: SubscriptionUsage): string {
  return formatResourceUsageMetric(
    usage.practitionersActive,
    usage.entitlements.maxPractitioners,
    "team members",
    "team member",
  );
}

export function usagePercentForLimit(active: number, max: number | null | undefined): number {
  if (max == null || max <= 0) {
    return 0;
  }
  return Math.min(100, Math.round((active / max) * 100));
}

export function bookingsUsagePercent(usage: SubscriptionUsage): number {
  return usagePercentForLimit(usage.bookingsUsedThisMonth, usage.bookingsLimitThisMonth);
}

export function isNearEntitlementLimit(
  active: number,
  max: number | null | undefined,
  threshold = 0.8,
): boolean {
  if (max == null || max <= 0) {
    return false;
  }
  return active / max >= threshold;
}

export function isPlanUsageNearLimit(usage: SubscriptionUsage, threshold = 0.8): boolean {
  return (
    isNearBookingLimit(usage, threshold) ||
    isNearEntitlementLimit(usage.locationsActive, usage.entitlements.maxLocations, threshold) ||
    isNearEntitlementLimit(
      usage.practitionersActive,
      usage.entitlements.maxPractitioners,
      threshold,
    )
  );
}
