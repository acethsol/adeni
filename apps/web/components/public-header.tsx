import Link from "next/link";
import { AuthNav } from "@/components/auth-nav";
import { canAccessMyBookings } from "@/lib/customer-access";
import { cn } from "@/lib/cn";

const navLinkClass =
  "rounded-full px-3 py-1.5 text-sm font-medium text-foreground transition-colors hover:bg-background hover:text-accent";

export function PublicHeader() {
  const showMyBookings = canAccessMyBookings();

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-surface/95 backdrop-blur">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <Link href="/" className="text-xl font-bold tracking-tight text-foreground">
          Adeni
        </Link>
        <nav className="flex items-center gap-1 sm:gap-2">
          <Link href="/discover" className={navLinkClass}>
            Discover
          </Link>
          {showMyBookings ? (
            <Link href="/my-bookings" className={navLinkClass}>
              My bookings
            </Link>
          ) : null}
          <Link href="/business" className={cn(navLinkClass, "hidden sm:inline-flex")}>
            For business
          </Link>
          <Link href="/admin" className={cn(navLinkClass, "hidden md:inline-flex")}>
            Admin
          </Link>
          <AuthNav />
        </nav>
      </div>
    </header>
  );
}
