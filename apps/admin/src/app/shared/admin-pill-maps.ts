import type { AdminPillVariant } from "./admin-pill.component";
import { formatTenantStatus, VERIFICATION_BADGE_LABELS } from "@adeni/shared";
import {
  formatSubscriptionSku,
  formatTenantBillingStatus,
  resolveSubscriptionSku,
  type AdminBusinessSummary,
  type PendingBadgeRequest,
  type SubscriptionSku,
  type TenantBillingStatus,
} from "@adeni/shared";

export function tenantStatusPill(status: number): { label: string; variant: AdminPillVariant } {
  const label = formatTenantStatus(status);
  const variant: AdminPillVariant =
    status === 2
      ? "success"
      : status === 1
        ? "warning"
        : status === 3
          ? "danger"
          : status === 4
            ? "danger"
            : "neutral";
  return { label, variant };
}

export function billingStatusPill(
  status: TenantBillingStatus | null | undefined,
): { label: string; variant: AdminPillVariant } {
  if (!status) {
    return { label: "—", variant: "neutral" };
  }
  const label = formatTenantBillingStatus(status);
  const variant: AdminPillVariant =
    status === "active"
      ? "success"
      : status === "trialing"
        ? "info"
        : status === "past_due"
          ? "warning"
          : status === "suspended" || status === "canceled"
            ? "danger"
            : "neutral";
  return { label, variant };
}

export function skuPill(sku: SubscriptionSku): { label: string; variant: AdminPillVariant } {
  const variant: AdminPillVariant =
    sku === "business" ? "brand" : sku === "professional" ? "accent" : "neutral";
  return { label: formatSubscriptionSku(sku), variant };
}

export function businessPlanPill(
  business: Pick<AdminBusinessSummary, "subscriptionSku">,
): { label: string; variant: AdminPillVariant } {
  return skuPill(resolveSubscriptionSku(business.subscriptionSku));
}

export function badgeTypePill(
  type: PendingBadgeRequest["badgeType"],
): { label: string; variant: AdminPillVariant } {
  const variant: AdminPillVariant =
    type === "license"
      ? "brand"
      : type === "cac"
        ? "accent"
        : type === "phone"
          ? "info"
          : "neutral";
  return { label: VERIFICATION_BADGE_LABELS[type] ?? type, variant };
}

export function reviewVisibilityPill(isHidden: boolean): { label: string; variant: AdminPillVariant } {
  return isHidden
    ? { label: "Hidden", variant: "danger" }
    : { label: "Public", variant: "success" };
}

export function ratingPill(rating: number): { label: string; variant: AdminPillVariant } {
  const variant: AdminPillVariant =
    rating >= 4 ? "success" : rating === 3 ? "neutral" : rating >= 1 ? "warning" : "neutral";
  return { label: `${rating}/5`, variant };
}

export function marketLivePill(isLive: boolean): { label: string; variant: AdminPillVariant } {
  return isLive
    ? { label: "Live", variant: "success" }
    : { label: "Offline", variant: "neutral" };
}

export function erasurePill(requested: boolean): { label: string; variant: AdminPillVariant } | null {
  if (!requested) {
    return null;
  }
  return { label: "Erasure requested", variant: "danger" };
}

export function docCountPill(count: number): { label: string; variant: AdminPillVariant } {
  return {
    label: count === 1 ? "1 document" : `${count} documents`,
    variant: count > 0 ? "info" : "neutral",
  };
}
