import type { AdeniCarbonIconName } from "@adeni/ui";

/**
 * Config-driven navigation for the signed-in customer app (`/app/*`).
 *
 * Ids are intentionally 1:1 with the future Flutter consumer app destinations
 * (`docs/specs/flutter-consumer-app.md`) — treat them as frozen.
 * Benefits is deliberately NOT a bottom tab: it is reachable from Home, Profile and the desktop nav.
 */
export type CustomerNavId = "home" | "discover" | "bookings" | "messages" | "benefits" | "profile";

export type CustomerNavBadge = "messages";

export type CustomerNavItem = {
  id: CustomerNavId;
  /** Absolute router path. */
  path: string;
  /** i18n key (EN/FR in `@adeni/shared`). */
  labelKey: string;
  icon: AdeniCarbonIconName;
  /** Shown in the mobile bottom tab bar. */
  tab: boolean;
  /** Shown in the desktop top-bar primary nav. */
  desktop: boolean;
  badge?: CustomerNavBadge;
};

export const CUSTOMER_NAV: readonly CustomerNavItem[] = [
  { id: "home", path: "/app/home", labelKey: "customer.app.nav.home", icon: "home", tab: true, desktop: true },
  { id: "discover", path: "/app/discover", labelKey: "customer.app.nav.discover", icon: "search", tab: true, desktop: true },
  { id: "bookings", path: "/app/bookings", labelKey: "customer.app.nav.bookings", icon: "calendar", tab: true, desktop: true },
  { id: "messages", path: "/app/messages", labelKey: "customer.app.nav.messages", icon: "chat", tab: true, desktop: true, badge: "messages" },
  { id: "benefits", path: "/app/benefits", labelKey: "customer.app.nav.benefits", icon: "gift", tab: false, desktop: true },
  { id: "profile", path: "/app/profile", labelKey: "customer.app.nav.profile", icon: "user--avatar", tab: true, desktop: false },
];

export const CUSTOMER_TAB_NAV: readonly CustomerNavItem[] = CUSTOMER_NAV.filter((item) => item.tab);
export const CUSTOMER_DESKTOP_NAV: readonly CustomerNavItem[] = CUSTOMER_NAV.filter((item) => item.desktop);

/** Where the signed-in customer lands by default (post-login, "Open app"). */
export const CUSTOMER_APP_HOME = "/app/home";
