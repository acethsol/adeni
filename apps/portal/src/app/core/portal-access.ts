import {
  hasCapability,
  hasPortalPermission,
  hasTenantEntitlement,
  resolveCapabilities,
  type BusinessProfile,
  type PortalPermission,
  type TenantEntitlements,
} from "@adeni/shared";
import type { PortalNavItem, PortalTab } from "./portal-nav";

export type PortalAccessContext = {
  profile: BusinessProfile | null;
  profileLoaded: boolean;
  permissions: readonly PortalPermission[];
};

export function portalEntitlements(profile: BusinessProfile | null): TenantEntitlements | null {
  return profile?.entitlements ?? null;
}

export function portalCapabilities(profile: BusinessProfile | null) {
  if (!profile) {
    return null;
  }
  return resolveCapabilities(profile.businessType, profile.categorySlug, profile.capabilities);
}

export function filterPortalTabs(
  tabs: PortalTab[] | undefined,
  entitlements: TenantEntitlements | null,
): PortalTab[] {
  if (!tabs?.length) {
    return [];
  }
  return tabs.filter(
    (tab) => !tab.entitlement || hasTenantEntitlement(entitlements, tab.entitlement),
  );
}

export function isPortalNavItemVisible(item: PortalNavItem, ctx: PortalAccessContext): boolean {
  if (item.shellHidden) {
    return false;
  }
  if (item.unregisteredOnly) {
    return ctx.profileLoaded && !ctx.profile;
  }
  if (item.permission && !hasPortalPermission(ctx.permissions, item.permission)) {
    return false;
  }
  if (
    item.excludeIfPermission &&
    hasPortalPermission(ctx.permissions, item.excludeIfPermission)
  ) {
    return false;
  }

  const capabilities = portalCapabilities(ctx.profile);
  const entitlements = portalEntitlements(ctx.profile);

  if (item.capability) {
    if (!capabilities) {
      return false;
    }
    if (!hasCapability(capabilities, item.capability)) {
      return false;
    }
  }

  if (item.entitlement && !hasTenantEntitlement(entitlements, item.entitlement)) {
    return false;
  }

  return true;
}

export function firstPortalTabId(item: PortalNavItem, entitlements: TenantEntitlements | null): string | null {
  const tabs = filterPortalTabs(item.tabs, entitlements);
  return tabs[0]?.id ?? null;
}
