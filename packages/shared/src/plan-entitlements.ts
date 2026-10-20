import catalog from "./data/plan-entitlements.json";
import type { SubscriptionSku } from "./country-pricing";

export type PlanEntitlementSeed = {
  maxLocations: number | null;
  maxPractitioners: number | null;
  unlimitedServices: boolean;
  unlimitedBookings: boolean;
  publicProfile: boolean;
  discoverListing: boolean;
  messaging: boolean;
  advancedReminders: boolean;
  whatsappIntegration: boolean;
  invoicesReceipts: boolean;
  onlinePayments: boolean;
  deposits: boolean;
  advancedAnalytics: boolean;
  customerRetentionAnalytics: boolean;
  staffPerformanceAnalytics: boolean;
  waitlist: boolean;
  multiLocationReporting: boolean;
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
