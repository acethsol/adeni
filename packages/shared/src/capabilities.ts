import type { BusinessType } from "./business-types";

import {

  capabilitiesForBusiness,

  defaultBusinessTypeForCategory,

  discoveryCtaForBusinessType,

  shouldShowQuoteFlow,

} from "./category-workflows";



export const CAPABILITIES = [

  "calendar",

  "fixed_pricing",

  "quotes",

  "on_site_address",

  "photo_upload",

  "license_badge",

  "deposits",

  "reviews",

  "multi_service",

  "service_area",

] as const;



export type Capability = (typeof CAPABILITIES)[number];



export { capabilitiesForBusiness, defaultBusinessTypeForCategory, discoveryCtaForBusinessType, shouldShowQuoteFlow };



export function hasCapability(

  capabilities: readonly string[] | undefined,

  capability: Capability,

): boolean {

  return capabilities?.includes(capability) ?? false;

}



export type PortalNavItem = {

  href: string;

  label: string;

  capability?: Capability;

};



export const PORTAL_NAV_ITEMS: PortalNavItem[] = [

  { href: "/business", label: "Overview" },

  { href: "/business/bookings", label: "Bookings", capability: "calendar" },

  { href: "/business/services", label: "Services" },

  { href: "/business/availability", label: "Availability", capability: "calendar" },

  { href: "/business/locations", label: "Locations" },

  { href: "/business/profile", label: "Profile" },

];



export function filterPortalNavItems(capabilities: readonly string[] | undefined): PortalNavItem[] {

  return PORTAL_NAV_ITEMS.filter(

    (item) => !item.capability || hasCapability(capabilities, item.capability),

  );

}



/** Client-side fallback when API fields are missing (e.g. tests). Prefer API `capabilities` in UI. */

export function resolveCapabilities(

  businessType: BusinessType | string | undefined,

  categorySlug: string,

  apiCapabilities?: readonly string[] | null,

): Capability[] {

  if (apiCapabilities && apiCapabilities.length > 0) {

    return apiCapabilities as Capability[];

  }



  return [...capabilitiesForBusiness(businessType, categorySlug)] as Capability[];
}

