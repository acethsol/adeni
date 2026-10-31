import type { AdeniBreadcrumb } from "@adeni/shared";

/** Router `history.state` key for {@link AdeniPageBreadcrumbService} extras. */
export const ADENI_BREADCRUMB_EXTRAS_STATE = "adeniBreadcrumbExtras" as const;

export type BookingIntakeBreadcrumbSource = {
  customerName?: string | null;
  tenantCustomerId?: string | null;
};

export function bookingIntakeBreadcrumbRouterState(
  booking: BookingIntakeBreadcrumbSource,
  guestLabel: string,
): { [ADENI_BREADCRUMB_EXTRAS_STATE]: AdeniBreadcrumb[] } {
  const label = booking.customerName?.trim() || guestLabel;
  const path = booking.tenantCustomerId ? `/customers/${booking.tenantCustomerId}` : undefined;
  return { [ADENI_BREADCRUMB_EXTRAS_STATE]: [{ label, path }] };
}

export function readBreadcrumbExtrasFromHistory(): AdeniBreadcrumb[] | null {
  if (typeof history === "undefined") {
    return null;
  }
  const raw = (history.state as Record<string, unknown> | null)?.[ADENI_BREADCRUMB_EXTRAS_STATE];
  if (!Array.isArray(raw) || raw.length === 0) {
    return null;
  }
  const crumbs: AdeniBreadcrumb[] = [];
  for (const entry of raw) {
    if (typeof entry !== "object" || entry === null || typeof (entry as AdeniBreadcrumb).label !== "string") {
      return null;
    }
    const crumb = entry as AdeniBreadcrumb;
    crumbs.push({
      label: crumb.label,
      path: typeof crumb.path === "string" ? crumb.path : undefined,
    });
  }
  return crumbs;
}
