import type { TenantSubscriptionCharge } from "@adeni/shared";
import {
  formatAmountMinor,
  formatBillingChargeStatus,
  formatSubscriptionSku,
} from "@adeni/shared";
import type { AdeniPillTone } from "@adeni/ui";

export function formatChargeAmount(row: {
  amountMinor?: number | null;
  currency?: string | null;
}): string {
  if (row.amountMinor != null && row.currency) {
    return formatAmountMinor(row.amountMinor, row.currency);
  }
  return "—";
}

export function chargeStatusTone(status: string): AdeniPillTone {
  const normalized = status.toLowerCase();
  if (normalized.includes("success") || normalized.includes("paid") || normalized === "completed") {
    return "success";
  }
  if (normalized.includes("fail") || normalized.includes("void")) {
    return "danger";
  }
  if (normalized.includes("pend")) {
    return "warning";
  }
  return "neutral";
}

export function chargeMatchesSearch(row: TenantSubscriptionCharge, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (!q) {
    return true;
  }
  const haystack = [
    row.providerReference,
    row.status,
    row.subscriptionSku,
    row.subscriptionSku ? formatSubscriptionSku(row.subscriptionSku) : "",
    formatBillingChargeStatus(row.status),
    formatChargeAmount(row),
    row.occurredAt,
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();
  return haystack.includes(q);
}
