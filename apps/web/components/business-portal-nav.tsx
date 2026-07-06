"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/cn";

const NAV_ITEMS = [
  { href: "/business", label: "Overview", exact: true },
  { href: "/business/bookings", label: "Bookings" },
  { href: "/business/services", label: "Services" },
  { href: "/business/availability", label: "Availability" },
  { href: "/business/locations", label: "Locations" },
  { href: "/business/profile", label: "Profile" },
  { href: "/business/register", label: "Register" },
];

export function BusinessPortalNav() {
  const pathname = usePathname();

  return (
    <nav className="mt-8 flex flex-wrap gap-2 border-b border-border pb-4">
      {NAV_ITEMS.map((item) => {
        const active = item.exact
          ? pathname === item.href
          : pathname.startsWith(item.href);

        return (
          <Link
            key={item.href}
            href={item.href}
            className={cn(
              "rounded-full px-4 py-2 text-sm font-semibold transition-colors",
              active
                ? "bg-primary text-primary-foreground shadow-sm"
                : "bg-surface text-muted ring-1 ring-border hover:text-foreground hover:ring-accent/40",
            )}
          >
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
