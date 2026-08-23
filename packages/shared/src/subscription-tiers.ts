import type { SubscriptionTier, SubscriptionUsage } from "./schemas";

export const SUBSCRIPTION_TIERS = ["free", "pro", "business"] as const;

export type PlanFeature = {
  label: string;
  free: string | boolean;
  pro: string | boolean;
  business: string | boolean;
};

export const PLAN_COMPARISON: PlanFeature[] = [
  {
    label: "Bookings per month",
    free: "20",
    pro: "Unlimited",
    business: "Unlimited",
  },
  {
    label: "Booking calendar",
    free: true,
    pro: true,
    business: true,
  },
  {
    label: "Customer messaging",
    free: false,
    pro: true,
    business: true,
  },
  {
    label: "Reminders",
    free: false,
    pro: true,
    business: true,
  },
  {
    label: "Analytics",
    free: false,
    pro: true,
    business: true,
  },
  {
    label: "Multiple locations",
    free: false,
    pro: false,
    business: true,
  },
  {
    label: "Staff management",
    free: false,
    pro: false,
    business: "Coming soon",
  },
  {
    label: "Priority support",
    free: false,
    pro: false,
    business: true,
  },
];

export const PLAN_PRICING: Record<
  SubscriptionTier,
  { name: string; priceLabel: string; description: string }
> = {
  free: {
    name: "Free",
    priceLabel: "₦0",
    description: "Profile, services, and basic calendar for getting started.",
  },
  pro: {
    name: "Pro",
    priceLabel: "₦5k–15k/mo",
    description: "Unlimited bookings, messaging, reminders, and analytics.",
  },
  business: {
    name: "Business",
    priceLabel: "₦25k+/mo",
    description: "Multi-location, staff tools, and priority support.",
  },
};

export function isNearBookingLimit(usage: SubscriptionUsage, threshold = 0.8): boolean {
  if (usage.bookingsLimitThisMonth == null) return false;
  if (usage.bookingsLimitThisMonth === 0) return false;
  return usage.bookingsUsedThisMonth / usage.bookingsLimitThisMonth >= threshold;
}

export function formatBookingUsage(usage: SubscriptionUsage): string {
  if (usage.bookingsLimitThisMonth == null) {
    return `${usage.bookingsUsedThisMonth} bookings this month`;
  }
  return `${usage.bookingsUsedThisMonth} / ${usage.bookingsLimitThisMonth} bookings this month`;
}
