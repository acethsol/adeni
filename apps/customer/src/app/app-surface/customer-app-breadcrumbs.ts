import { findCustomerNavItem } from "./customer-nav-groups";

export type CustomerCrumb = { label: string; path?: string };

/** Top-bar breadcrumb trail for nested `/app/*` routes. */
export function crumbsForCustomerUrl(tr: (key: string) => string, url: string): CustomerCrumb[] {
  const path = (url.split(/[?#]/)[0] ?? url).replace(/\/+$/, "") || "/app/dashboard";
  const match = findCustomerNavItem(path);
  if (!match) {
    return [{ label: tr("customer.app.nav.dashboard") }];
  }

  const base: CustomerCrumb[] = [{ label: tr(match.group.labelKey) }];

  const bookingNested = path.match(/^\/app\/bookings\/([^/]+)(?:\/(.*))?$/);
  if (bookingNested && match.item.id === "bookings") {
    const bookingId = bookingNested[1]!;
    const tail = bookingNested[2];
    base.push({ label: tr("customer.app.nav.bookings"), path: "/app/bookings" });
    if (tail?.startsWith("intake")) {
      base.push({
        label: tr("customer.app.breadcrumbs.bookingDetail"),
        path: `/app/bookings/${bookingId}`,
      });
      const intakeTail = tail.slice("intake".length).replace(/^\//, "");
      if (intakeTail) {
        base.push({
          label: tr("intake.title"),
          path: `/app/bookings/${bookingId}/intake`,
        });
        base.push({ label: tr("customer.app.breadcrumbs.consultationForm") });
      } else {
        base.push({ label: tr("intake.title") });
      }
    } else {
      base.push({ label: tr("customer.app.breadcrumbs.bookingDetail") });
    }
    return base;
  }

  if (path.startsWith("/app/benefits")) {
    base.push({ label: tr("customer.app.nav.benefits"), path: "/app/benefits/packages" });
    if (path.includes("/memberships")) {
      base.push({ label: tr("discover.memberships.myLink") });
    } else if (path.includes("/gift-cards")) {
      base.push({ label: tr("discover.packages.giftCardsLink") });
    } else {
      base.push({ label: tr("discover.packages.myLink") });
    }
    return base;
  }

  base.push({ label: tr(match.item.labelKey) });
  return base;
}
