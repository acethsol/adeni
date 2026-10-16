export type AdminNavIconName =
  | "dashboard"
  | "verifications"
  | "badges"
  | "reviews"
  | "insights"
  | "transactions"
  | "markets"
  | "businesses"
  | "privacy"
  | "logout"
  | "collapse"
  | "expand"
  | "settings"
  | "light"
  | "asleep"
  | "chevronDown"
  | "notification"
  | "portal"
  | "search";

export type AdminNavGroupId = "operations" | "platform" | "compliance";

export type AdminNavItem = {
  path: string;
  label: string;
  icon: AdminNavIconName;
  exact?: boolean;
  badge?: "pending" | "pendingBadges";
};

export type AdminNavGroup = {
  id: AdminNavGroupId;
  label: string;
  items: AdminNavItem[];
};

export const ADMIN_NAV_GROUPS: AdminNavGroup[] = [
  {
    id: "operations",
    label: "Operations",
    items: [
      { path: "/dashboard", label: "Dashboard", icon: "dashboard", exact: true },
      { path: "/pending", label: "Pending verifications", icon: "verifications", badge: "pending" },
      { path: "/trust-badges", label: "Trust badges", icon: "badges", badge: "pendingBadges" },
      { path: "/reviews", label: "Reviews", icon: "reviews" },
    ],
  },
  {
    id: "platform",
    label: "Platform",
    items: [
      { path: "/markets", label: "Markets", icon: "markets" },
      { path: "/businesses", label: "Businesses", icon: "businesses" },
      { path: "/insights", label: "Revenue", icon: "insights" },
      { path: "/transactions", label: "Transactions", icon: "transactions" },
    ],
  },
  {
    id: "compliance",
    label: "Compliance",
    items: [{ path: "/customers", label: "Customers", icon: "privacy" }],
  },
];

export const ADMIN_NAV: AdminNavItem[] = ADMIN_NAV_GROUPS.flatMap((group) => group.items);

export function findAdminNavItem(url: string): { group: AdminNavGroup; item: AdminNavItem } | null {
  const path = url.split("?")[0] ?? "";
  for (const group of ADMIN_NAV_GROUPS) {
    for (const item of group.items) {
      if (item.exact ? path === item.path : path === item.path || path.startsWith(`${item.path}/`)) {
        return { group, item };
      }
    }
  }
  return null;
}
