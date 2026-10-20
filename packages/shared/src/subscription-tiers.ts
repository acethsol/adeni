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
