import type { TenantEntitlements } from "@adeni/shared";
import { filterPortalTabs, isPortalNavItemVisible, type PortalAccessContext } from "../core/portal-access";
import { PORTAL_NAV_GROUPS, type PortalNavGroup, type PortalNavItem } from "../core/portal-nav";

export type SearchHit = {
  id: string;
  label: string;
  path: string;
  icon: PortalNavItem["icon"];
  external?: boolean;
};

export function portalNavigationGroups(context: PortalAccessContext): PortalNavGroup[] {
  return PORTAL_NAV_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => isPortalNavItemVisible(item, context)),
  })).filter((group) => group.items.length > 0);
}

export function portalSearchResults(options: {
  query: string;
  groups: PortalNavGroup[];
  entitlements: TenantEntitlements | null;
  navLabel: (item: PortalNavItem) => string;
  tabLabel: (id: string) => string;
  discoverLabel: string;
  discoverUrl: string;
}): SearchHit[] {
  const query = options.query.trim().toLowerCase();
  const entitlements = options.entitlements;
  const navItems = options.groups.flatMap((group) => group.items);
  const pages = navItems
    .filter((item) => !query || options.navLabel(item).toLowerCase().includes(query) || item.label.toLowerCase().includes(query))
    .map((item) => ({
      id: item.path,
      label: options.navLabel(item),
      path: item.path,
      icon: item.icon,
    }));
  const tabPages = navItems.flatMap((item) => {
    const tabs = filterPortalTabs(item.tabs, entitlements);
    return tabs
      .filter(
        (tab) =>
          !query
          || tab.label.toLowerCase().includes(query)
          || options.tabLabel(tab.id).toLowerCase().includes(query)
          || options.navLabel(item).toLowerCase().includes(query),
      )
      .map((tab) => ({
        id: `${item.path}?tab=${tab.id}`,
        label: `${options.navLabel(item)} — ${options.tabLabel(tab.id)}`,
        path: `${item.path}?tab=${tab.id}`,
        icon: item.icon,
      }));
  });
  const discoverLabel = options.discoverLabel;
  const discover =
    !query || discoverLabel.toLowerCase().includes(query) || "discover".includes(query)
      ? [{ id: "discover", label: discoverLabel, path: options.discoverUrl, icon: "launch" as const, external: true }]
      : [];
  const merged = [...tabPages, ...pages, ...discover];
  const seen = new Set<string>();
  return merged.filter((hit) => {
    if (seen.has(hit.path)) {
      return false;
    }
    seen.add(hit.path);
    return true;
  }).slice(0, 10);
}
