"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  CalendarDays,
  Clock3,
  LayoutDashboard,
  MapPin,
  PlusCircle,
  Scissors,
  UserCircle,
} from "lucide-react";
import { cn } from "@/lib/cn";

const NAV_ITEMS = [
  { href: "/business", label: "Overview", exact: true, icon: LayoutDashboard },
  { href: "/business/bookings", label: "Bookings", icon: CalendarDays },
  { href: "/business/services", label: "Services", icon: Scissors },
  { href: "/business/availability", label: "Availability", icon: Clock3 },
  { href: "/business/locations", label: "Locations", icon: MapPin },
  { href: "/business/profile", label: "Profile", icon: UserCircle },
] as const;

const REGISTER_ITEM = {
  href: "/business/register",
  label: "Register",
  icon: PlusCircle,
} as const;

type Props = {
  showRegister?: boolean;
};

export function BusinessPortalNav({ showRegister = true }: Props) {
  const pathname = usePathname();
  const items = showRegister ? [...NAV_ITEMS, REGISTER_ITEM] : NAV_ITEMS;

  return (
    <>
      <nav
        className="hidden lg:flex lg:flex-col lg:gap-1"
        aria-label="Business portal"
      >
        {items.map((item) => {
          const active = "exact" in item && item.exact
            ? pathname === item.href
            : pathname.startsWith(item.href);
          const Icon = item.icon;

          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold transition-colors",
                active
                  ? "bg-primary text-primary-foreground shadow-sm"
                  : "text-muted hover:bg-subtle hover:text-foreground",
              )}
            >
              <Icon className="h-4 w-4 shrink-0" aria-hidden />
              {item.label}
            </Link>
          );
        })}
      </nav>

      <nav
        className="flex gap-2 overflow-x-auto pb-1 lg:hidden"
        aria-label="Business portal"
      >
        {items.map((item) => {
          const active = "exact" in item && item.exact
            ? pathname === item.href
            : pathname.startsWith(item.href);

          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "shrink-0 rounded-full px-4 py-2 text-sm font-semibold transition-colors",
                active
                  ? "bg-primary text-primary-foreground shadow-sm"
                  : "bg-surface text-muted ring-1 ring-border hover:text-foreground",
              )}
            >
              {item.label}
            </Link>
          );
        })}
      </nav>
    </>
  );
}
