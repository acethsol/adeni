export type AdminNavIconName =
  | "dashboard"
  | "verifications"
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

export type AdminNavItem = {
  path: string;
  label: string;
  icon: AdminNavIconName;
};

export const ADMIN_NAV: AdminNavItem[] = [
  { path: "/dashboard", label: "Dashboard", icon: "dashboard" },
  { path: "/pending", label: "Pending verifications", icon: "verifications" },
  { path: "/markets", label: "Markets", icon: "markets" },
  { path: "/businesses", label: "Businesses", icon: "businesses" },
  { path: "/customers", label: "Customer privacy", icon: "privacy" },
];
