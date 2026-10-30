import {
  CUSTOMER_NAV,
  type CustomerNavId,
  type CustomerNavItem,
} from "./customer-nav";

export type CustomerNavGroupId = "main" | "account";

export type CustomerNavGroup = {
  id: CustomerNavGroupId;
  labelKey: string;
  items: CustomerNavItem[];
};

export const CUSTOMER_NAV_GROUPS: readonly CustomerNavGroup[] = [
  {
    id: "main",
    labelKey: "customer.app.shell.groupMain",
    items: CUSTOMER_NAV.filter((item) => item.id !== "profile"),
  },
  {
    id: "account",
    labelKey: "customer.app.shell.groupAccount",
    items: CUSTOMER_NAV.filter((item) => item.id === "profile"),
  },
];

export function findCustomerNavItem(
  url: string,
): { group: CustomerNavGroup; item: CustomerNavItem } | null {
  const path = url.split(/[?#]/)[0] ?? url;
  for (const group of CUSTOMER_NAV_GROUPS) {
    for (const item of group.items) {
      if (path === item.path || path.startsWith(`${item.path}/`)) {
        return { group, item };
      }
    }
  }
  return null;
}

export function customerNavIdForPath(url: string): CustomerNavId | null {
  return findCustomerNavItem(url)?.item.id ?? null;
}
