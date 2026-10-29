import type { BookingDeliveryType } from "@adeni/shared";

export type ServiceDraft = {
  name: string;
  description: string;
  priceAmount: string;
  currency: string;
  durationMinutes: string;
  categorySlug: string;
  catalogServiceId: string;
  bookingDeliveryType: BookingDeliveryType;
  menuGroupId: string;
  sortOrder: string;
  isAddOn: boolean;
};

export function emptyServiceDraft(defaults: {
  currency: string;
  categorySlug: string;
}): ServiceDraft {
  return {
    name: "",
    description: "",
    priceAmount: "",
    currency: defaults.currency,
    durationMinutes: "30",
    categorySlug: defaults.categorySlug,
    catalogServiceId: "",
    bookingDeliveryType: "appointment",
    menuGroupId: "",
    sortOrder: "0",
    isAddOn: false,
  };
}
