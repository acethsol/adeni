import {
  resolveSubscriptionSku,
  skuToSubscriptionTier,
  type AdminBusinessSummary,
  type AdminTenantCommercialTerms,
  type SetTenantCommercialTermsRequest,
  type SubscriptionSku,
  type SubscriptionTier,
  type TenantBillingStatus,
} from "@adeni/shared";

export type BusinessSubscriptionDraft = {
  subscriptionSku: SubscriptionSku;
  initialSubscriptionSku: SubscriptionSku;
  trialDays: number | null;
  trialSku: SubscriptionSku;
  endTrial: boolean;
  billingStatus: TenantBillingStatus;
  discountPercent: number | null;
  discountFixedAmount: number | null;
  discountCurrency: string;
  discountCyclesRemaining: number | null;
  customSaasMonthlyAmount: number | null;
  customSaasCurrency: string;
  customSaasSku: SubscriptionSku | "";
  billingCompReason: string;
  clearDiscounts: boolean;
  clearCustomSaasPrice: boolean;
};

export function draftFromCommercialTerms(
  terms: AdminTenantCommercialTerms,
  business: AdminBusinessSummary,
): BusinessSubscriptionDraft {
  const subscriptionSku = resolveSubscriptionSku(terms.subscriptionSku, business.subscriptionTier);
  return {
    subscriptionSku,
    initialSubscriptionSku: subscriptionSku,
    trialDays: 30,
    trialSku: terms.trialSku ?? "professional",
    endTrial: terms.billingStatus === "active" && !terms.trialEndsAt,
    billingStatus: terms.billingStatus,
    discountPercent: terms.discountPercent ?? null,
    discountFixedAmount: terms.discountFixedAmount ?? null,
    discountCurrency: terms.discountCurrency ?? "",
    discountCyclesRemaining: terms.discountCyclesRemaining ?? null,
    customSaasMonthlyAmount: terms.customSaasMonthlyAmount ?? null,
    customSaasCurrency: terms.customSaasCurrency ?? "",
    customSaasSku: terms.customSaasSku ?? "",
    billingCompReason: terms.billingCompReason ?? "",
    clearDiscounts: false,
    clearCustomSaasPrice: false,
  };
}

export function legacyTierForSku(sku: SubscriptionSku): SubscriptionTier {
  return skuToSubscriptionTier(sku);
}

export function buildCommercialTermsRequest(
  draft: BusinessSubscriptionDraft,
): SetTenantCommercialTermsRequest {
  const body: SetTenantCommercialTermsRequest = {
    subscriptionSku: draft.subscriptionSku,
    billingStatus: draft.billingStatus,
    billingCompReason: draft.billingCompReason.trim() || null,
  };

  if (draft.endTrial) {
    body.endTrial = true;
  } else if (draft.trialDays != null) {
    body.trialDays = draft.trialDays;
    body.trialSku = draft.trialSku;
  }

  if (draft.clearDiscounts) {
    body.clearDiscounts = true;
  } else {
    if (draft.discountPercent != null) {
      body.discountPercent = draft.discountPercent;
    }
    if (draft.discountFixedAmount != null) {
      body.discountFixedAmount = draft.discountFixedAmount;
    }
    if (draft.discountCurrency.trim()) {
      body.discountCurrency = draft.discountCurrency.trim().toUpperCase();
    }
    if (draft.discountCyclesRemaining != null) {
      body.discountCyclesRemaining = draft.discountCyclesRemaining;
    }
  }

  if (draft.clearCustomSaasPrice) {
    body.clearCustomSaasPrice = true;
  } else if (draft.customSaasMonthlyAmount != null && draft.customSaasCurrency.trim()) {
    body.customSaasMonthlyAmount = draft.customSaasMonthlyAmount;
    body.customSaasCurrency = draft.customSaasCurrency.trim().toUpperCase();
    if (draft.customSaasSku) {
      body.customSaasSku = draft.customSaasSku;
    }
  }

  return body;
}
