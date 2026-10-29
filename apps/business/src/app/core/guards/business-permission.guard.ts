import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { hasCapability, hasTenantEntitlement, type BusinessPermission } from "@adeni/shared";
import {
  filterBusinessTabs,
  firstBusinessTabId,
  businessCapabilities,
  businessEntitlements,
} from "../business-access";
import { findBusinessNavItem } from "../business-nav";
import { BusinessApiService } from "../services/business-api.service";
import { BusinessSessionService } from "../services/business-session.service";
/** Blocks routes the current role, plan, or business capabilities do not allow. */
export const businessPermissionGuard: CanActivateFn = async (route) => {
  const session = inject(BusinessSessionService);
  const router = inject(Router);
  const businessApi = inject(BusinessApiService);

  await session.ensureLoaded();

  const path = "/" + (route.routeConfig?.path ?? "").split("/:")[0];
  const navPath = path === "/" ? "/dashboard" : path;
  const match = findBusinessNavItem(navPath);
  if (!match) {
    return router.createUrlTree(["/forbidden"]);
  }

  const required = match.item.permission as BusinessPermission | readonly BusinessPermission[] | undefined;

  if (!session.can(required)) {
    if (session.can("business.overview")) {
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

  const capabilities = businessCapabilities(profile);
  const entitlements = businessEntitlements(profile);
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
    const allowedTabs = filterBusinessTabs(item.tabs, entitlements);
    const tab = item.tabs.find((entry) => entry.id === tabId);
    if (tab?.entitlement && !hasTenantEntitlement(entitlements, tab.entitlement)) {
      const fallback = firstBusinessTabId(item, entitlements);
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
