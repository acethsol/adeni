import type { Capability } from "@adeni/shared";

export type PortalNavIconName =
  | "overview"
  | "bookings"
  | "quotes"
  | "messages"
  | "services"
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

export type PortalNavGroupId = "today" | "listing" | "business";

export type PortalTab = {
  id: string;
  label: string;
};

export type PortalNavItem = {
  path: string;
  label: string;
  icon: PortalNavIconName;
  exact?: boolean;
  capability?: Capability;
  badge?: "bookings" | "messages";
  /** Shown only before a business profile exists. */
  unregisteredOnly?: boolean;
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
    label: "Today",
    items: [
      { path: "/dashboard", label: "Overview", icon: "overview", exact: true },
      {
        path: "/bookings",
        label: "Bookings",
        icon: "bookings",
        capability: "calendar",
        badge: "bookings",
        tabs: [
          { id: "pending", label: "Pending" },
          { id: "upcoming", label: "Upcoming" },
          { id: "past", label: "Past" },
        ],
      },
      { path: "/quotes", label: "Quotes", icon: "quotes", capability: "quotes" },
      {
        path: "/messages",
        label: "Messages",
        icon: "messages",
        badge: "messages",
        tabs: [
          { id: "inbox", label: "Inbox" },
          { id: "whatsapp", label: "WhatsApp" },
        ],
      },
      { path: "/register", label: "Register", icon: "register", unregisteredOnly: true },
    ],
  },
  {
    id: "listing",
    label: "Listing",
    items: [
      { path: "/services", label: "Services", icon: "services" },
      { path: "/availability", label: "Hours", icon: "availability", capability: "calendar" },
      { path: "/locations", label: "Locations", icon: "locations" },
      { path: "/public-page", label: "Public page", icon: "public" },
    ],
  },
  {
    id: "business",
    label: "Business",
    items: [
      {
        path: "/profile",
        label: "Profile",
        icon: "profile",
        tabs: [
          { id: "details", label: "Details" },
          { id: "reviews", label: "Reviews" },
          { id: "verification", label: "Verification" },
        ],
      },
      { path: "/payments", label: "Payments", icon: "payments", capability: "deposits", tabs: [
        { id: "links", label: "Links" },
        { id: "ledger", label: "Ledger" },
      ] },
      { path: "/plan", label: "Plan", icon: "plan" },
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
