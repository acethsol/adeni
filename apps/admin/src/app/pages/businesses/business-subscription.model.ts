import {
  resolveSubscriptionSku,
  type AdminBusinessSummary,
  type AdminTenantCommercialTerms,
  type SetTenantCommercialTermsRequest,
  type SubscriptionSku,
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
  changeReason: string;
  clearDiscounts: boolean;
  clearCustomSaasPrice: boolean;
  initialTerms: AdminTenantCommercialTerms;
};

export function draftFromCommercialTerms(
  terms: AdminTenantCommercialTerms,
  business: AdminBusinessSummary,
): BusinessSubscriptionDraft {
  const subscriptionSku = resolveSubscriptionSku(terms.subscriptionSku ?? business.subscriptionSku);
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
    changeReason: "",
    clearDiscounts: false,
    clearCustomSaasPrice: false,
    initialTerms: terms,
  };
}

export function hasCommercialTermsDelta(draft: BusinessSubscriptionDraft): boolean {
  const initial = draft.initialTerms;
  if (draft.subscriptionSku !== initial.subscriptionSku) {
    return true;
  }
  if (draft.billingStatus !== initial.billingStatus) {
    return true;
  }
  if (draft.endTrial) {
    return true;
  }
  if (draft.trialDays != null) {
    return true;
  }
  if (draft.clearDiscounts) {
    return true;
  }
  if (draft.clearCustomSaasPrice) {
    return true;
  }
  if (draft.discountPercent !== (initial.discountPercent ?? null)) {
    return true;
  }
  if (draft.discountFixedAmount !== (initial.discountFixedAmount ?? null)) {
    return true;
  }
  const initialDiscountCurrency = initial.discountCurrency ?? "";
  if (draft.discountCurrency.trim().toUpperCase() !== initialDiscountCurrency.toUpperCase()) {
    return true;
  }
  if (draft.discountCyclesRemaining !== (initial.discountCyclesRemaining ?? null)) {
    return true;
  }
  if (draft.customSaasMonthlyAmount !== (initial.customSaasMonthlyAmount ?? null)) {
    return true;
  }
  const initialCustomCurrency = initial.customSaasCurrency ?? "";
  if (draft.customSaasCurrency.trim().toUpperCase() !== initialCustomCurrency.toUpperCase()) {
    return true;
  }
  const initialCustomSku = initial.customSaasSku ?? "";
  if ((draft.customSaasSku || "") !== initialCustomSku) {
    return true;
  }
  return false;
}

export function resolveCommercialChangeReason(draft: BusinessSubscriptionDraft): string | null {
  const changeReason = draft.changeReason.trim();
  if (changeReason) {
    return changeReason;
  }
  const billingCompReason = draft.billingCompReason.trim();
  return billingCompReason || null;
}

export function buildCommercialTermsRequest(
  draft: BusinessSubscriptionDraft,
): SetTenantCommercialTermsRequest {
  const changeReason = resolveCommercialChangeReason(draft);
  const body: SetTenantCommercialTermsRequest = {
    subscriptionSku: draft.subscriptionSku,
    billingStatus: draft.billingStatus,
    billingCompReason: draft.billingCompReason.trim() || null,
    changeReason: changeReason ?? undefined,
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
