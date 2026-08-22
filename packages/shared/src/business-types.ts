export const BUSINESS_TYPES = ["scheduled_appointment", "quote_request"] as const;

export type BusinessType = (typeof BUSINESS_TYPES)[number];

export const DISCOVERY_CTAS = ["book_now", "get_quote"] as const;

export type DiscoveryCta = (typeof DISCOVERY_CTAS)[number];

export function isQuoteBusinessType(businessType?: string | null): boolean {
  return businessType === "quote_request";
}

export function isAppointmentBusinessType(businessType?: string | null): boolean {
  return !businessType || businessType === "scheduled_appointment";
}

export function discoveryCtaLabel(cta?: string | null): string {
  return cta === "get_quote" ? "Get a quote" : "Book now";
}
