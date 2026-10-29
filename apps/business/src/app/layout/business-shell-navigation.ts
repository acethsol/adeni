import type { TenantEntitlements } from "@adeni/shared";
import { filterBusinessTabs, isBusinessNavItemVisible, type BusinessAccessContext } from "../core/business-access";
import { BUSINESS_NAV_GROUPS, type BusinessNavGroup, type BusinessNavItem } from "../core/business-nav";

export type SearchHit = {
  id: string;
  label: string;
  path: string;
  icon: BusinessNavItem["icon"];
  external?: boolean;
};

export function businessNavigationGroups(context: BusinessAccessContext): BusinessNavGroup[] {
  return BUSINESS_NAV_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => isBusinessNavItemVisible(item, context)),
  })).filter((group) => group.items.length > 0);
}

export function businessSearchResults(options: {
  query: string;
  groups: BusinessNavGroup[];
  entitlements: TenantEntitlements | null;
  navLabel: (item: BusinessNavItem) => string;
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
    const tabs = filterBusinessTabs(item.tabs, entitlements);
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
