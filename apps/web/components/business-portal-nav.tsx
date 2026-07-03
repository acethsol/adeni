"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const NAV_ITEMS = [
  { href: "/business", label: "Overview", exact: true },
  { href: "/business/bookings", label: "Bookings" },
  { href: "/business/services", label: "Services" },
  { href: "/business/profile", label: "Profile" },
];

export function BusinessPortalNav() {
  const pathname = usePathname();

  return (
    <nav className="mt-8 flex flex-wrap gap-2 border-b border-[#1b4332]/10 pb-4">
      {NAV_ITEMS.map((item) => {
        const active = item.exact
          ? pathname === item.href
          : pathname.startsWith(item.href);

        return (
          <Link
            key={item.href}
            href={item.href}
            className={`rounded-full px-4 py-2 text-sm font-medium transition ${
              active
                ? "bg-[#1b4332] text-white"
                : "bg-white text-[#1b4332]/80 ring-1 ring-[#1b4332]/10 hover:ring-[#40916c]/40"
            }`}
          >
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
