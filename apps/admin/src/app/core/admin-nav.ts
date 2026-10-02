export type AdminNavItem = {
  path: string;
  label: string;
};

export const ADMIN_NAV: AdminNavItem[] = [
  { path: "/dashboard", label: "Dashboard" },
  { path: "/pending", label: "Pending verifications" },
  { path: "/markets", label: "Markets" },
  { path: "/businesses", label: "Businesses" },
];
