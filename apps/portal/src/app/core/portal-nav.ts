import type { Capability, PortalPermission, TenantEntitlementFlag } from "@adeni/shared";

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
  exact?: boolean;
  capability?: Capability;
  /** Subscription plan — hide route when not entitled (API enforces the same). */
  entitlement?: TenantEntitlementFlag;
  /** Portal RBAC — any listed permission grants access. */
  permission?: PortalPermission | readonly PortalPermission[];
  /**
   * Hide this item when the user also has this permission
   * (e.g. “My calendar” for practitioners only — not owners with full staff).
   */
  excludeIfPermission?: PortalPermission;
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
        permission: "portal.overview",
      },
      {
        path: "/bookings",
        label: "Bookings",
        icon: "bookings",
        capability: "calendar",
        permission: ["portal.bookings", "portal.bookings.self"],
        badge: "bookings",
        tabs: [
          { id: "pending", label: "Pending" },
          { id: "upcoming", label: "Upcoming" },
          { id: "past", label: "Past" },
        ],
      },
      {
        path: "/queue",
        label: "Queue",
        icon: "queue",
        capability: "walk_in_queue",
        permission: ["portal.bookings", "portal.bookings.self"],
      },
      {
        path: "/kiosk",
        label: "Check-in",
        icon: "bookings",
        permission: ["portal.bookings", "portal.bookings.self"],
        shellHidden: true,
      },
      {
        path: "/calendar",
        label: "Calendar",
        icon: "calendar",
        capability: "calendar",
        entitlement: "staffManagement",
        permission: "portal.staff",
      },
      {
        path: "/my-calendar",
        label: "My calendar",
        icon: "calendar",
        capability: "calendar",
        permission: "portal.staff.self",
        excludeIfPermission: "portal.staff",
      },
      {
        path: "/quotes",
        label: "Quotes",
        icon: "quotes",
        capability: "quotes",
        permission: "portal.quotes",
      },
      {
        path: "/messages",
        label: "Messages",
        icon: "messages",
        permission: "portal.messages",
        badge: "messages",
        tabs: [
          { id: "inbox", label: "Inbox", entitlement: "messaging" },
          { id: "whatsapp", label: "WhatsApp" },
        ],
      },
      { path: "/register", label: "Register", icon: "register", unregisteredOnly: true },
    ],
  },
  {
    id: "listing",
    label: "Your business",
    items: [
      { path: "/services", label: "Services", icon: "services", permission: "portal.services" },
      {
        path: "/staff",
        label: "Staff",
        icon: "staff",
        capability: "calendar",
        entitlement: "staffManagement",
        permission: "portal.staff",
      },
      {
        path: "/customers",
        label: "Customers",
        icon: "customers",
        permission: ["portal.bookings", "portal.bookings.self"],
      },
      {
        path: "/availability",
        label: "Hours",
        icon: "availability",
        capability: "calendar",
        permission: "portal.hours",
      },
      { path: "/locations", label: "Locations", icon: "locations", permission: "portal.locations" },
      {
        path: "/public-page",
        label: "Public page",
        icon: "public",
        permission: "portal.public_page",
      },
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
        permission: "portal.profile",
        tabs: [
          { id: "overview", label: "Overview" },
          { id: "business", label: "Business details" },
          { id: "reviews", label: "Reviews" },
          { id: "verification", label: "Verification" },
        ],
      },
      {
        path: "/payments",
        label: "Payments",
        icon: "payments",
        capability: "deposits",
        permission: "portal.payments",
        tabs: [
          { id: "links", label: "Links" },
          { id: "ledger", label: "Ledger" },
        ],
      },
      {
        path: "/settings",
        label: "Settings",
        icon: "settings",
        permission: "portal.profile",
        tabs: [
          { id: "bookings", label: "Bookings" },
          { id: "notifications", label: "Notifications" },
          { id: "policies", label: "Policies" },
          { id: "messaging", label: "Messaging", entitlement: "messaging" },
        ],
      },
      { path: "/plan", label: "Plan", icon: "plan", permission: "portal.plan" },
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
