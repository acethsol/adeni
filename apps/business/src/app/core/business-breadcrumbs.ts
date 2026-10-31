import type { AdeniBreadcrumb } from "@adeni/shared";
import { breadcrumbPathname, mergeBreadcrumbTrails } from "@adeni/shared";
import {
  activeBusinessTab,
  findBusinessNavItem,
  BUSINESS_NAV,
  type BusinessNavGroup,
  type BusinessNavItem,
} from "./business-nav";

export type BusinessBreadcrumbLabels = {
  groupLabel: (group: BusinessNavGroup) => string;
  navLabel: (item: BusinessNavItem) => string;
  tabLabel: (tabId: string) => string;
  tr: (key: string) => string;
  fallbackGroupLabel: () => string;
};

export function crumbsForBusinessUrl(url: string, labels: BusinessBreadcrumbLabels): AdeniBreadcrumb[] {
  const pathname = breadcrumbPathname(url);
  const match = findBusinessNavItem(pathname) ?? findBusinessNavItem(BUSINESS_NAV[0]?.path ?? "/dashboard");
  if (!match) {
    return [{ label: labels.fallbackGroupLabel() }];
  }

  const onListRoute = pathname === match.item.path;
  const crumbs: AdeniBreadcrumb[] = [
    { label: labels.groupLabel(match.group) },
    {
      label: labels.navLabel(match.item),
      path: !onListRoute || match.item.tabs?.length ? match.item.path : undefined,
    },
  ];

  appendRouteSpecificCrumbs(pathname, crumbs, labels);

  const tabId = activeBusinessTab(url, match.item.tabs);
  const tab = match.item.tabs?.find((entry) => entry.id === tabId);
  const onTabbedListSurface =
    pathname === match.item.path || /^\/retention\/(packages|memberships|loyalty)$/.test(pathname);
  if (tab && onTabbedListSurface) {
    crumbs.push({ label: labels.tabLabel(tab.id) });
  }

  appendCustomerDetailTab(pathname, url, crumbs, labels);
  appendServiceDetailTab(pathname, url, crumbs, labels);

  return crumbs;
}

function appendRouteSpecificCrumbs(
  pathname: string,
  crumbs: AdeniBreadcrumb[],
  labels: BusinessBreadcrumbLabels,
): void {
  if (/^\/bookings\/[^/]+\/intake$/.test(pathname)) {
    crumbs.push({ label: labels.tr("intake.title") });
    return;
  }

  if (pathname === "/settings/calendars") {
    crumbs.push({ label: labels.tr("calendarUi.title") });
    return;
  }

  if (pathname === "/public-page/booking-links") {
    crumbs.push({ label: labels.tr("calendarUi.bookWith") });
    return;
  }

  if (pathname === "/finance/quick-pay") {
    crumbs.push({ label: labels.tr("businessApp.breadcrumbs.quickPay") });
    return;
  }

  if (pathname === "/services/intake") {
    crumbs.push({ label: labels.tr("intake.title") });
    return;
  }

  if (/^\/staff\/[^/]+\/calendar$/.test(pathname)) {
    crumbs.push({ label: labels.tr("businessApp.breadcrumbs.staffCalendar") });
    return;
  }

  if (pathname === "/kiosk") {
    crumbs.push({ label: labels.tr("businessApp.breadcrumbs.checkIn") });
  }
}

function appendCustomerDetailTab(
  pathname: string,
  url: string,
  crumbs: AdeniBreadcrumb[],
  labels: BusinessBreadcrumbLabels,
): void {
  if (!/^\/customers\/[^/]+$/.test(pathname)) {
    return;
  }
  const tab = new URLSearchParams(url.split("?")[1] ?? "").get("tab") ?? "overview";
  if (tab === "overview") {
    return;
  }
  crumbs.push({ label: customerDetailTabLabel(tab, labels.tr) });
}

function appendServiceDetailTab(
  pathname: string,
  url: string,
  crumbs: AdeniBreadcrumb[],
  labels: BusinessBreadcrumbLabels,
): void {
  if (!/^\/services\/[^/]+$/.test(pathname)) {
    return;
  }
  const tab = new URLSearchParams(url.split("?")[1] ?? "").get("tab") ?? "details";
  if (tab === "details") {
    return;
  }
  if (tab === "intake") {
    crumbs.push({ label: labels.tr("businessApp.services.consultationFormTab") });
  }
}

/**
 * Page extras (customer/service name) must sit before query-tab leaves on detail routes,
 * not after them — e.g. Customers → Dev Customer → Bookings.
 */
export function mergeBusinessDetailBreadcrumbExtras(
  url: string,
  base: readonly AdeniBreadcrumb[],
  extras: readonly AdeniBreadcrumb[],
): AdeniBreadcrumb[] {
  if (!extras.length) {
    return [...base];
  }

  const pathname = breadcrumbPathname(url);
  const customerTab = customerDetailTabId(pathname, url);
  const insertExtrasBeforeLeaf =
    (customerTab !== null && customerTab !== "overview") ||
    serviceDetailTabId(pathname, url) === "intake" ||
    /^\/bookings\/[^/]+\/intake$/.test(pathname);

  if (insertExtrasBeforeLeaf && base.length >= 3) {
    const leaf = base[base.length - 1]!;
    return [...base.slice(0, -1), ...extras, leaf];
  }

  return mergeBreadcrumbTrails(base, extras);
}

function customerDetailTabId(pathname: string, url: string): string | null {
  if (!/^\/customers\/[^/]+$/.test(pathname)) {
    return null;
  }
  return new URLSearchParams(url.split("?")[1] ?? "").get("tab") ?? "overview";
}

function serviceDetailTabId(pathname: string, url: string): string | null {
  if (!/^\/services\/[^/]+$/.test(pathname)) {
    return null;
  }
  return new URLSearchParams(url.split("?")[1] ?? "").get("tab") ?? "details";
}

function customerDetailTabLabel(tab: string, tr: (key: string) => string): string {
  switch (tab) {
    case "bookings":
      return tr("common.bookings");
    case "queue":
      return tr("common.queue");
    case "activity":
      return tr("customerProfile.activity");
    case "packages":
      return tr("businessApp.retention.customerTab");
    case "memberships":
      return tr("businessApp.memberships.customerTab");
    case "loyalty":
      return tr("businessApp.loyalty.customerTab");
    default:
      return tr("businessApp.customers.overview");
  }
}
