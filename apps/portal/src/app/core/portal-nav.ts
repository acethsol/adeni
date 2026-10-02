export type PortalNavItem = {
  path: string;
  label: string;
};

export const PORTAL_NAV: PortalNavItem[] = [
  { path: "/dashboard", label: "Dashboard" },
  { path: "/bookings", label: "Bookings" },
  { path: "/messages", label: "Messages" },
  { path: "/services", label: "Services" },
  { path: "/locations", label: "Locations" },
  { path: "/availability", label: "Availability" },
  { path: "/profile", label: "Profile" },
  { path: "/register", label: "Register" },
  { path: "/payments", label: "Payments" },
  { path: "/plan", label: "Plan" },
];
