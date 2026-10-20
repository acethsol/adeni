import type { SubscriptionSku } from "./country-pricing";
import type { SubscriptionUsage, TenantBillingStatus } from "./schemas";

export type PlanFeature = {
  label: string;
  starter: string | boolean;
  professional: string | boolean;
  business: string | boolean;
};

export const PLAN_COMPARISON: PlanFeature[] = [
  {
    label: "Bookings per month",
    starter: "Unlimited",
    professional: "Unlimited",
    business: "Unlimited",
  },
  {
    label: "Adeni inbox",
    starter: true,
    professional: true,
    business: true,
  },
  {
    label: "Team members",
    starter: "Up to 3",
    professional: "Up to 10",
    business: "Unlimited",
  },
  {
    label: "Business locations",
    starter: "1",
    professional: "1",
    business: "Multiple",
  },
  {
    label: "Automated reminders",
    starter: false,
    professional: true,
    business: true,
  },
  {
    label: "Advanced analytics",
    starter: false,
    professional: true,
    business: true,
  },
  {
    label: "Waitlist",
    starter: false,
    professional: true,
    business: true,
  },
  {
    label: "WhatsApp integration",
    starter: false,
    professional: true,
    business: true,
  },
  {
    label: "Priority support",
    starter: false,
    professional: false,
    business: true,
  },
];

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

export function planComparisonCell(row: PlanFeature, sku: SubscriptionSku): string {
  const value = row[sku];
  if (value === true) return "Yes";
  if (value === false) return "—";
  return value;
}
