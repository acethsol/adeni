import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { hasCapability, hasTenantEntitlement, type PortalPermission } from "@adeni/shared";
import {
  filterPortalTabs,
  firstPortalTabId,
  portalCapabilities,
  portalEntitlements,
} from "../portal-access";
import { findPortalNavItem } from "../portal-nav";
import { BusinessApiService } from "../services/business-api.service";
import { PortalSessionService } from "../services/portal-session.service";
/** Blocks routes the current role, plan, or business capabilities do not allow. */
export const portalPermissionGuard: CanActivateFn = async (route) => {
  const session = inject(PortalSessionService);
  const router = inject(Router);
  const businessApi = inject(BusinessApiService);

  await session.ensureLoaded();

  const path = "/" + (route.routeConfig?.path ?? "").split("/:")[0];
  const navPath = path === "/" ? "/dashboard" : path;
  const match = findPortalNavItem(navPath);
  if (!match) {
    return router.createUrlTree(["/forbidden"]);
  }

  const required = match.item.permission as PortalPermission | readonly PortalPermission[] | undefined;

  if (!session.can(required)) {
    if (session.can("portal.overview")) {
      return router.createUrlTree(["/dashboard"]);
    }
    return router.createUrlTree(["/forbidden"]);
  }

  let profile = null;
  try {
    profile = await businessApi.getTenantProfile();
  } catch {
    profile = null;
  }

  const capabilities = portalCapabilities(profile);
  const entitlements = portalEntitlements(profile);
  const item = match?.item;

  if (item?.capability) {
    if (!capabilities || !hasCapability(capabilities, item.capability)) {
      return router.createUrlTree(["/dashboard"]);
    }
  }

  if (item?.entitlement && !hasTenantEntitlement(entitlements, item.entitlement)) {
    return router.createUrlTree(["/dashboard"]);
  }

  const tabId = route.queryParamMap.get("tab");
  if (item?.tabs?.length && tabId) {
    const allowedTabs = filterPortalTabs(item.tabs, entitlements);
    const tab = item.tabs.find((entry) => entry.id === tabId);
    if (tab?.entitlement && !hasTenantEntitlement(entitlements, tab.entitlement)) {
      const fallback = firstPortalTabId(item, entitlements);
      return fallback
        ? router.createUrlTree([item.path], { queryParams: { tab: fallback } })
        : router.createUrlTree(["/dashboard"]);
    }
    if (!allowedTabs.some((t) => t.id === tabId)) {
      const fallback = allowedTabs[0]?.id;
      return fallback
        ? router.createUrlTree([item.path], { queryParams: { tab: fallback } })
        : router.createUrlTree(["/dashboard"]);
    }
  }

  return true;
};
