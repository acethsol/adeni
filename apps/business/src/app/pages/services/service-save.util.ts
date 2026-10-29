import type { ServiceOffering } from "@adeni/shared";
import { emptyServiceDraft, type ServiceDraft } from "./service-draft";

export function draftFromService(service: ServiceOffering, primaryCategorySlug: string): ServiceDraft {
  return {
    name: service.name,
    description: service.description ?? "",
    priceAmount: String(service.priceAmount),
    currency: service.currency,
    durationMinutes: String(service.durationMinutes),
    categorySlug: service.categorySlug ?? primaryCategorySlug,
    catalogServiceId: service.catalogServiceId ?? "",
    bookingDeliveryType: service.bookingDeliveryType ?? "appointment",
    menuGroupId: service.menuGroupId ?? "",
    sortOrder: String(service.sortOrder ?? 0),
    isAddOn: service.isAddOn === true,
  };
}

export function serviceDraftToUpdateBody(draft: ServiceDraft): {
  name: string;
  description: string | undefined;
  priceAmount: number;
  currency: string;
  durationMinutes: number;
  categorySlug: string | undefined;
  catalogServiceId: string | undefined;
  bookingDeliveryType: ServiceDraft["bookingDeliveryType"];
  menuGroupId: string | null;
  sortOrder: number;
  isAddOn: boolean;
} | null {
  const price = Number(draft.priceAmount);
  const duration = Number(draft.durationMinutes);
  const sortOrder = Number(draft.sortOrder);
  if (!draft.name.trim() || Number.isNaN(price) || Number.isNaN(duration)) {
    return null;
  }
  return {
    name: draft.name.trim(),
    description: draft.description.trim() || undefined,
    priceAmount: price,
    currency: draft.currency.trim().toUpperCase(),
    durationMinutes: Math.round(duration),
    categorySlug: draft.categorySlug.trim() || undefined,
    catalogServiceId: draft.catalogServiceId.trim() || undefined,
    bookingDeliveryType: draft.bookingDeliveryType || "appointment",
    menuGroupId: draft.menuGroupId.trim() || null,
    sortOrder: Number.isNaN(sortOrder) ? 0 : Math.round(sortOrder),
    isAddOn: draft.isAddOn,
  };
}

export function serviceDraftToCreateBody(
  draft: ServiceDraft,
): ReturnType<typeof serviceDraftToUpdateBody> {
  return serviceDraftToUpdateBody(draft);
}

export function resetEmptyDraft(defaultCurrency: string, categorySlug: string): ServiceDraft {
  return emptyServiceDraft({ currency: defaultCurrency, categorySlug });
}
