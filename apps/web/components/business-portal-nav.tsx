"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Banknote,
  CalendarDays,
  Clock3,
  CreditCard,
  FileText,
  LayoutDashboard,
  MapPin,
  PlusCircle,
  Scissors,
  UserCircle,
  type LucideIcon,
} from "lucide-react";
import { hasCapability, type Capability } from "@adeni/shared";
import { cn } from "@/lib/cn";
import { useSidebarTooltip } from "@/lib/use-sidebar-tooltip";

const NAV_CAPABILITIES: Partial<Record<string, Capability>> = {
  "/business/bookings": "calendar",
  "/business/availability": "calendar",
  "/business/payments": "deposits",
};

export const BUSINESS_NAV_ITEMS = [
  { href: "/business", label: "Overview", exact: true, icon: LayoutDashboard },
  { href: "/business/bookings", label: "Bookings", icon: CalendarDays },
  { href: "/business/quotes", label: "Quotes", icon: FileText },
  { href: "/business/services", label: "Services", icon: Scissors },
  { href: "/business/availability", label: "Availability", icon: Clock3 },
  { href: "/business/payments", label: "Payments", icon: Banknote },
  { href: "/business/locations", label: "Locations", icon: MapPin },
  { href: "/business/profile", label: "Profile", icon: UserCircle },
  { href: "/business/plan", label: "Plan", icon: CreditCard },
] as const;

export const BUSINESS_REGISTER_ITEM = {
  href: "/business/register",
  label: "Register",
  icon: PlusCircle,
} as const;

/** Resolves the current nav item's label for use in topbar breadcrumbs. */
export function useBusinessPortalPageLabel() {
  const pathname = usePathname();
  const items = [...BUSINESS_NAV_ITEMS, BUSINESS_REGISTER_ITEM];
  const match = items.find((item) =>
    "exact" in item && item.exact ? pathname === item.href : pathname.startsWith(item.href),
  );
  return match?.label;
}

type Props = {
  showRegister?: boolean;
  onNavigate?: () => void;
  collapsed?: boolean;
  className?: string;
  capabilities?: readonly string[];
};

function filterNavItems<T extends { href: string }>(items: readonly T[], capabilities?: readonly string[]) {
  return items.filter((item) => {
    const capability = NAV_CAPABILITIES[item.href];
    return !capability || hasCapability(capabilities, capability);
  });
}

export function BusinessPortalNavLinks({
  showRegister = true,
  onNavigate,
  collapsed = false,
  className,
  capabilities,
}: Props) {
  const pathname = usePathname();
  const visibleNav = filterNavItems(BUSINESS_NAV_ITEMS, capabilities);
  const items = showRegister ? [...visibleNav, BUSINESS_REGISTER_ITEM] : visibleNav;

  return (
    <nav className={cn("flex flex-col gap-1", className)} aria-label="Business portal">
      {items.map((item) => {
        const active =
          "exact" in item && item.exact ? pathname === item.href : pathname.startsWith(item.href);

        return (
          <NavLinkItem
            key={item.href}
            href={item.href}
            label={item.label}
            icon={item.icon}
            active={active}
            collapsed={collapsed}
            onNavigate={onNavigate}
          />
        );
      })}
    </nav>
  );
}

function NavLinkItem({
  href,
  label,
  icon: Icon,
  active,
  collapsed,
  onNavigate,
}: {
  href: string;
  label: string;
  icon: LucideIcon;
  active: boolean;
  collapsed: boolean;
  onNavigate?: () => void;
}) {
  const tooltip = useSidebarTooltip<HTMLAnchorElement>(label, collapsed);

  return (
    <>
      <Link
        ref={tooltip.ref}
        href={href}
        onClick={onNavigate}
        onMouseEnter={tooltip.onMouseEnter}
        onMouseLeave={tooltip.onMouseLeave}
        onFocus={tooltip.onFocus}
        onBlur={tooltip.onBlur}
        aria-current={active ? "page" : undefined}
        className={cn(
          "group relative flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold transition-colors",
          collapsed && "justify-center px-0",
          active ? "bg-white/15 text-white shadow-sm" : "text-white/65 hover:bg-white/8 hover:text-white",
        )}
      >
        {active ? (
          <span
            className="absolute inset-y-1.5 left-0 w-1 rounded-full bg-accent"
            aria-hidden
          />
        ) : null}
        <Icon
          className={cn(
            "h-4 w-4 shrink-0 transition-colors",
            active ? "text-accent" : "text-white/40 group-hover:text-white/70",
          )}
          aria-hidden
        />
        {collapsed ? null : (
          <>
            {label}
            {active ? <span className="ml-auto h-1.5 w-1.5 shrink-0 rounded-full bg-accent" aria-hidden /> : null}
          </>
        )}
      </Link>
      {tooltip.tooltip}
    </>
  );
}
