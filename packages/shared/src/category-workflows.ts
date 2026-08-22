import catalog from "./data/category-workflows.json";
import type { BusinessType } from "./business-types";

type CategoryWorkflowEntry = {
  defaultBusinessType?: BusinessType;
  capabilities?: string[];
};

type BusinessTypeWorkflowEntry = {
  discoveryCta: string;
  capabilities?: string[];
};

const workflowCatalog = catalog as {
  defaultBusinessType: BusinessType;
  businessTypes: Record<string, BusinessTypeWorkflowEntry>;
  categories: Record<string, CategoryWorkflowEntry>;
  appointmentFallbackCapabilities: string[];
};

export function defaultBusinessTypeForCategory(categorySlug: string): BusinessType {
  const normalized = categorySlug.trim().toLowerCase();
  return (
    workflowCatalog.categories[normalized]?.defaultBusinessType ??
    workflowCatalog.defaultBusinessType
  );
}

export function capabilitiesForBusiness(
  businessType: BusinessType | string | undefined,
  categorySlug: string,
): readonly string[] {
  const normalizedType =
    businessType === "quote_request" ? "quote_request" : "scheduled_appointment";

  if (normalizedType === "quote_request") {
    return workflowCatalog.businessTypes.quote_request.capabilities ?? [];
  }

  const normalizedCategory = categorySlug.trim().toLowerCase();
  return (
    workflowCatalog.categories[normalizedCategory]?.capabilities ??
    workflowCatalog.appointmentFallbackCapabilities
  );
}

export function discoveryCtaForBusinessType(businessType: BusinessType | string | undefined): string {
  const normalizedType =
    businessType === "quote_request" ? "quote_request" : "scheduled_appointment";
  return workflowCatalog.businessTypes[normalizedType]?.discoveryCta ?? "book_now";
}

/** Prefer API `discoveryCta` / `capabilities`; fall back to business type only when needed. */
export function shouldShowQuoteFlow(profile: {
  businessType?: string | null;
  discoveryCta?: string | null;
  capabilities?: readonly string[] | null;
}): boolean {
  if (profile.discoveryCta === "get_quote") {
    return true;
  }

  if (profile.capabilities?.includes("quotes")) {
    return true;
  }

  return profile.businessType === "quote_request";
}
