import {
  hasCapability,
  hasBusinessPermission,
  hasTenantEntitlement,
  resolveCapabilities,
  type BusinessProfile,
  type BusinessPermission,
  type TenantEntitlements,
} from "@adeni/shared";
import type { BusinessNavItem, BusinessTab } from "./business-nav";

export type BusinessAccessContext = {
  profile: BusinessProfile | null;
  profileLoaded: boolean;
  permissions: readonly BusinessPermission[];
};

export function businessEntitlements(profile: BusinessProfile | null): TenantEntitlements | null {
  return profile?.entitlements ?? null;
}

export function businessCapabilities(profile: BusinessProfile | null) {
  if (!profile) {
    return null;
  }
  return resolveCapabilities(profile.businessType, profile.categorySlug, profile.capabilities);
}

export function filterBusinessTabs(
  tabs: BusinessTab[] | undefined,
  entitlements: TenantEntitlements | null,
): BusinessTab[] {
  if (!tabs?.length) {
    return [];
  }
  return tabs.filter(
    (tab) => !tab.entitlement || hasTenantEntitlement(entitlements, tab.entitlement),
  );
}

export function isBusinessNavItemVisible(item: BusinessNavItem, ctx: BusinessAccessContext): boolean {
  if (item.shellHidden) {
    return false;
  }
  if (item.unregisteredOnly) {
    return ctx.profileLoaded && !ctx.profile;
  }
  if (item.permission && !hasBusinessPermission(ctx.permissions, item.permission)) {
    return false;
  }
  if (
    item.excludeIfPermission &&
    hasBusinessPermission(ctx.permissions, item.excludeIfPermission)
  ) {
    return false;
  }

  const capabilities = businessCapabilities(ctx.profile);
  const entitlements = businessEntitlements(ctx.profile);

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

export function firstBusinessTabId(item: BusinessNavItem, entitlements: TenantEntitlements | null): string | null {
  const tabs = filterBusinessTabs(item.tabs, entitlements);
  return tabs[0]?.id ?? null;
}
