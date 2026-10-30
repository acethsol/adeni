import type { AdeniPortalNavIconName } from "@adeni/ui";

/**
 * Config-driven navigation for the signed-in customer app (`/app/*`).
 *
 * Ids are frozen for Flutter parity (`docs/specs/flutter-consumer-app.md`, C2).
 * Sidebar glyphs use the same Carbon 32px set as the business portal (`AdeniPortalNavIconComponent`).
 */
export type CustomerNavId =
  | "dashboard"
  | "calendar"
  | "discover"
  | "bookings"
  | "messages"
  | "benefits"
  | "profile";

export type CustomerNavBadge = "messages";

export type CustomerNavItem = {
  id: CustomerNavId;
  /** Absolute router path. */
  path: string;
  /** i18n key (EN/FR in `@adeni/shared`). */
  labelKey: string;
  icon: AdeniPortalNavIconName;
  /** Shown in the mobile bottom tab bar. */
  tab: boolean;
  /** Shown in the desktop top-bar primary nav. */
  desktop: boolean;
  badge?: CustomerNavBadge;
};

export const CUSTOMER_NAV: readonly CustomerNavItem[] = [
  {
    id: "dashboard",
    path: "/app/dashboard",
    labelKey: "customer.app.nav.dashboard",
    icon: "overview",
    tab: true,
    desktop: true,
  },
  {
    id: "calendar",
    path: "/app/calendar",
    labelKey: "customer.app.nav.calendar",
    icon: "calendar",
    tab: true,
    desktop: true,
  },
  {
    id: "discover",
    path: "/app/discover",
    labelKey: "customer.app.nav.discover",
    icon: "search",
    tab: true,
    desktop: true,
  },
  {
    id: "bookings",
    path: "/app/bookings",
    labelKey: "customer.app.nav.bookings",
    icon: "bookings",
    tab: false,
    desktop: true,
  },
  {
    id: "messages",
    path: "/app/messages",
    labelKey: "customer.app.nav.messages",
    icon: "messages",
    tab: true,
    desktop: true,
    badge: "messages",
  },
  {
    id: "benefits",
    path: "/app/benefits",
    labelKey: "customer.app.nav.benefits",
    icon: "plan",
    tab: false,
    desktop: true,
  },
  {
    id: "profile",
    path: "/app/profile",
    labelKey: "customer.app.nav.profile",
    icon: "profile",
    tab: true,
    desktop: false,
  },
];

export const CUSTOMER_TAB_NAV: readonly CustomerNavItem[] = CUSTOMER_NAV.filter((item) => item.tab);
export const CUSTOMER_DESKTOP_NAV: readonly CustomerNavItem[] = CUSTOMER_NAV.filter((item) => item.desktop);

/** Where the signed-in customer lands by default (post-login, "Open app"). */
export const CUSTOMER_APP_HOME = "/app/dashboard";
