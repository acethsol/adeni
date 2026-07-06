import Link from "next/link";
import { AuthNav } from "@/components/auth-nav";
import { GlobalSearchBar } from "@/components/global-search-bar";
import { canAccessMyBookings } from "@/lib/customer-access";
import { cn } from "@/lib/cn";

const navLinkClass =
  "rounded-full px-3 py-1.5 text-sm font-medium text-foreground transition-colors hover:bg-background hover:text-accent";

export function PublicHeader() {
  const showMyBookings = canAccessMyBookings();

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-surface/95 backdrop-blur">
      <div className="mx-auto max-w-5xl space-y-3 px-6 py-4">
        <div className="flex items-center justify-between gap-4">
          <Link href="/" className="shrink-0 text-xl font-bold tracking-tight text-foreground">
            Adeni
          </Link>
          <nav className="flex shrink-0 items-center gap-1 sm:gap-2">
            <Link href="/discover" className={cn(navLinkClass, "hidden sm:inline-flex")}>
              Discover
            </Link>
            {showMyBookings ? (
              <Link href="/my-bookings" className={cn(navLinkClass, "hidden md:inline-flex")}>
                My bookings
              </Link>
            ) : null}
            <Link href="/business" className={cn(navLinkClass, "hidden lg:inline-flex")}>
              For business
            </Link>
            <AuthNav />
          </nav>
        </div>
        <GlobalSearchBar />
      </div>
    </header>
  );
}
