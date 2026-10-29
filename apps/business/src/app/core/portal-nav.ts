import type { Capability, BusinessPermission, TenantEntitlementFlag } from "@adeni/shared";

export type PortalNavIconName =
  | "overview"
  | "bookings"
  | "customers"
  | "queue"
  | "calendar"
  | "quotes"
  | "messages"
  | "services"
  | "staff"
  | "locations"
  | "availability"
  | "profile"
  | "public"
  | "register"
  | "payments"
  | "plan"
  | "launch"
  | "portal"
  | "logout"
  | "collapse"
  | "expand"
  | "settings"
  | "light"
  | "asleep"
  | "chevronDown"
  | "search";

/** Group ids are stable; display labels live in portal chrome i18n. */
export type PortalNavGroupId = "today" | "listing" | "business";

export type PortalTab = {
  id: string;
  label: string;
  /** Hide tab when the tenant's plan does not include this entitlement. */
  entitlement?: TenantEntitlementFlag;
};

export type PortalNavItem = {
  path: string;
  label: string;
  icon: PortalNavIconName;
  /** Opens the public Discover app in a new tab (browse as a customer). */
  openDiscover?: boolean;
  exact?: boolean;
  capability?: Capability;
  /** Subscription plan — hide route when not entitled (API enforces the same). */
  entitlement?: TenantEntitlementFlag;
  /** Portal RBAC — any listed permission grants access. */
  permission?: BusinessPermission | readonly BusinessPermission[];
  /**
   * Hide this item when the user also has this permission
   * (e.g. “My calendar” for practitioners only — not owners with full staff).
   */
  excludeIfPermission?: BusinessPermission;
  badge?: "bookings" | "messages";
  /** Shown only before a business profile exists. */
  unregisteredOnly?: boolean;
  /** Keep for permission routing but omit from sidebar chrome. */
  shellHidden?: boolean;
  tabs?: PortalTab[];
};

export type PortalNavGroup = {
  id: PortalNavGroupId;
  label: string;
  items: PortalNavItem[];
};

export const PORTAL_NAV_GROUPS: PortalNavGroup[] = [
  {
    id: "today",
    label: "Operations",
    items: [
      {
        path: "/dashboard",
        label: "Dashboard",
        icon: "overview",
        exact: true,
        permission: "business.overview",
      },
      {
        path: "/bookings",
        label: "Bookings",
        icon: "bookings",
        capability: "calendar",
        permission: ["business.bookings", "business.bookings.self"],
        badge: "bookings",
        tabs: [
          { id: "pending", label: "Pending" },
          { id: "upcoming", label: "Upcoming" },
          { id: "past", label: "Past" },
        ],
      },
      {
        path: "/customers",
        label: "Customers",
        icon: "customers",
        permission: ["business.bookings", "business.bookings.self"],
      },
      {
        path: "/retention",
        label: "Packages",
        icon: "payments",
        permission: "business.retention",
        entitlement: "packages",
        exact: true,
      },
      {
        path: "/retention/memberships",
        label: "Memberships",
        icon: "payments",
        permission: "business.retention",
        entitlement: "memberships",
      },
      {
        path: "/retention/loyalty",
        label: "Loyalty",
        icon: "payments",
        permission: "business.retention",
        entitlement: "loyaltyBasic",
      },
      {
        path: "/queue",
        label: "Queue",
        icon: "queue",
        capability: "walk_in_queue",
        permission: ["business.bookings", "business.bookings.self"],
      },
      {
        path: "/kiosk",
        label: "Check-in",
        icon: "bookings",
        permission: ["business.bookings", "business.bookings.self"],
        shellHidden: true,
      },
      {
        path: "/calendar",
        label: "Calendar",
        icon: "calendar",
        capability: "calendar",
        permission: "business.staff",
      },
      {
        path: "/my-calendar",
        label: "My calendar",
        icon: "calendar",
        capability: "calendar",
        permission: "business.staff.self",
        excludeIfpermission: "business.staff",
      },
      {
        path: "/quotes",
        label: "Quotes",
        icon: "quotes",
        capability: "quotes",
        permission: "business.quotes",
      },
      {
        path: "/messages",
        label: "Messages",
        icon: "messages",
        permission: "business.messages",
        badge: "messages",
        tabs: [{ id: "inbox", label: "Inbox" }],
      },
      { path: "/register", label: "Register", icon: "register", unregisteredOnly: true },
    ],
  },
  {
    id: "listing",
    label: "Setup",
    items: [
      { path: "/services", label: "Services", icon: "services", permission: "business.services" },
      {
        path: "/availability",
        label: "Hours",
        icon: "availability",
        capability: "calendar",
        permission: "business.hours",
      },
      {
        path: "/staff",
        label: "Staff",
        icon: "staff",
        capability: "calendar",
        permission: "business.staff",
      },
      { path: "/locations", label: "Locations", icon: "locations", permission: "business.locations" },
    ],
  },
  {
    id: "business",
    label: "Account",
    items: [
      {
        path: "/profile",
        label: "Profile",
        icon: "profile",
        permission: "business.profile",
        tabs: [
          { id: "overview", label: "Overview" },
          { id: "business", label: "Business details" },
          { id: "reviews", label: "Reviews" },
          { id: "verification", label: "Verification" },
        ],
      },
      {
        path: "/public-page",
        label: "Public page",
        icon: "public",
        permission: "business.public_page",
      },
      {
        path: "/settings",
        label: "Settings",
        icon: "settings",
        permission: "business.profile",
        tabs: [
          { id: "bookings", label: "Bookings" },
          { id: "notifications", label: "Notifications" },
          { id: "policies", label: "Policies" },
          { id: "messaging", label: "Messaging", entitlement: "messaging" },
        ],
      },
      {
        path: "/finance",
        label: "Finance",
        icon: "payments",
        capability: "deposits",
        permission: "business.finance",
        tabs: [
          { id: "links", label: "Links" },
          { id: "ledger", label: "Ledger" },
        ],
      },
      { path: "/plan", label: "Plan", icon: "plan", permission: "business.plan" },
      {
        path: "/__discover",
        label: "Discover",
        icon: "launch",
        openDiscover: true,
        permission: "business.overview",
      },
    ],
  },
];

export const PORTAL_NAV: PortalNavItem[] = PORTAL_NAV_GROUPS.flatMap((group) => group.items);

export function findPortalNavItem(path: string): { group: PortalNavGroup; item: PortalNavItem } | null {
  const clean = path.split("?")[0] ?? path;
  for (const group of PORTAL_NAV_GROUPS) {
    const item = group.items.find(
      (entry) => clean === entry.path || (!entry.exact && clean.startsWith(`${entry.path}/`)),
    );
    if (item) {
      return { group, item };
    }
  }
  return null;
}

export function activePortalTab(url: string, tabs: PortalTab[] | undefined): string | null {
  if (!tabs?.length) {
    return null;
  }
  const requested = new URLSearchParams(url.split("?")[1] ?? "").get("tab");
  if (requested && tabs.some((tab) => tab.id === requested)) {
    return requested;
  }
  return tabs[0].id;
}
