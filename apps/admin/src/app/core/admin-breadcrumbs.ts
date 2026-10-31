import type { AdeniBreadcrumb } from "@adeni/shared";
import { breadcrumbPathname } from "@adeni/shared";
import {
  ADMIN_NAV,
  ADMIN_NAV_GROUPS,
  findAdminNavItem,
  type AdminNavGroup,
  type AdminNavItem,
} from "./admin-nav";

export type AdminBreadcrumbLabels = {
  groupLabel: (group: AdminNavGroup) => string;
  navLabel: (item: AdminNavItem) => string;
  /** Localized overlay/catalog keys (e.g. `admin.breadcrumbs.*`). */
  tr: (key: string) => string;
  /** Shell chrome copy keyed by route path (`/dashboard`, `groupPlatform`, …). */
  chrome: (key: string) => string;
};

export function crumbsForAdminUrl(url: string, labels: AdminBreadcrumbLabels): AdeniBreadcrumb[] {
  const pathname = breadcrumbPathname(url);
  const pricing = resolvePricingTrail(pathname, labels);
  if (pricing) {
    return pricing;
  }

  const businessDetail = resolveBusinessDetailTrail(pathname, labels);
  if (businessDetail) {
    return businessDetail;
  }

  const match = findAdminNavItem(pathname) ?? findAdminNavItem(ADMIN_NAV[0]?.path ?? "/dashboard");
  if (!match) {
    return [{ label: labels.chrome("groupOperations") }, { label: labels.chrome("/dashboard") }];
  }

  const onListRoute = pathname === match.item.path;
  return [
    { label: labels.groupLabel(match.group) },
    {
      label: labels.navLabel(match.item),
      path: onListRoute ? undefined : match.item.path,
    },
  ];
}

function resolvePricingTrail(pathname: string, labels: AdminBreadcrumbLabels): AdeniBreadcrumb[] | null {
  if (!pathname.startsWith("/pricing/")) {
    return null;
  }
  const platform = ADMIN_NAV_GROUPS.find((g) => g.id === "platform");
  const catalogItem = platform?.items.find((i) => i.path === "/pricing/catalog");
  if (!platform || !catalogItem) {
    return null;
  }

  const crumbs: AdeniBreadcrumb[] = [
    { label: labels.groupLabel(platform) },
    { label: labels.navLabel(catalogItem), path: "/pricing/catalog" },
  ];

  if (pathname === "/pricing/catalog") {
    return crumbs;
  }

  if (pathname === "/pricing/gmv") {
    crumbs.push({ label: labels.tr("admin.breadcrumbs.gmvFees") });
    return crumbs;
  }

  if (pathname === "/pricing/entitlements") {
    const entitlements = platform.items.find((i) => i.path === "/pricing/entitlements");
    crumbs.push({ label: entitlements ? labels.navLabel(entitlements) : labels.chrome("/pricing/entitlements") });
    return crumbs;
  }

  return crumbs;
}

function resolveBusinessDetailTrail(pathname: string, labels: AdminBreadcrumbLabels): AdeniBreadcrumb[] | null {
  if (!/^\/businesses\/[^/]+/.test(pathname)) {
    return null;
  }
  const platform = ADMIN_NAV_GROUPS.find((g) => g.id === "platform");
  const businessesItem = platform?.items.find((i) => i.path === "/businesses");
  if (!platform || !businessesItem) {
    return null;
  }

  return [
    { label: labels.groupLabel(platform) },
    { label: labels.navLabel(businessesItem), path: "/businesses" },
  ];
}

/** Call after merging {@link AdeniPageBreadcrumbService} extras (business name). */
export function appendAdminBusinessDetailTab(
  url: string,
  crumbs: readonly AdeniBreadcrumb[],
  text: (key: string) => string,
): AdeniBreadcrumb[] {
  const pathname = breadcrumbPathname(url);
  const tab = pathname.match(/^\/businesses\/[^/]+\/([^/]+)$/)?.[1];
  if (!tab || tab === "overview") {
    return [...crumbs];
  }
  return [...crumbs, { label: adminBusinessTabLabel(tab, text) }];
}

function adminBusinessTabLabel(tab: string, text: (key: string) => string): string {
  switch (tab) {
    case "terms":
      return text("admin.businesses.commercialTerms");
    case "billing":
      return text("admin.businesses.billing2");
    case "audit":
      return text("common.commercialAudit");
    default:
      return text("businessApp.customers.overview");
  }
}
