"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Briefcase, CalendarDays, Compass, MapPin } from "lucide-react";
import { AuthNavClient } from "@/components/auth-nav-client";
import { HeaderDiscoverySearch } from "@/components/header-discovery-search";
import { LocaleCurrencySwitcher } from "@/components/locale-currency-switcher";
import { useHeroSearchPinned } from "@/components/use-hero-search-pinned";
import { useTranslation } from "@/components/locale-provider";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/cn";
import { publicContainerClass } from "@/lib/layout-classes";

export type PublicHeaderSearchMode = "hero-handoff" | "compact" | "inline";

type Props = {
  searchMode?: PublicHeaderSearchMode;
  marketId?: string;
  marketName?: string;
  currency?: string;
  countryCode?: string;
  showBookingsNav?: boolean;
};

function NavLink({
  href,
  label,
  icon: Icon,
  active,
  className,
}: {
  href: string;
  label: string;
  icon: typeof Compass;
  active: boolean;
  className?: string;
}) {
  return (
    <Link
      href={href}
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full px-3 py-2 text-sm font-semibold transition-colors",
        active
          ? "bg-accent/10 text-accent"
          : "text-foreground hover:bg-subtle hover:text-accent",
        className,
      )}
    >
      <Icon className="h-4 w-4 shrink-0" aria-hidden />
      <span>{label}</span>
    </Link>
  );
}

export function PublicHeader({
  searchMode = "inline",
  marketId = "lagos",
  marketName = "Lagos",
  currency = "NGN",
  countryCode = "NG",
  showBookingsNav = false,
}: Props) {
  const pathname = usePathname();
  const { t } = useTranslation();

  const usesHeroHandoff = searchMode === "hero-handoff";
  const searchPinned = useHeroSearchPinned(usesHeroHandoff);
  const showCompactSearch =
    searchMode === "compact" || (usesHeroHandoff && searchPinned);
  const showInlineSearch = searchMode === "inline";

  const isDiscover = pathname === "/discover" || pathname.startsWith("/discover/");
  const isBookings = pathname === "/my-bookings";
  const isBusiness = pathname.startsWith("/business");

  return (
    <header
      className={cn(
        "sticky top-0 z-40 border-b border-border bg-surface/95 backdrop-blur-md transition-shadow duration-300",
        showCompactSearch && usesHeroHandoff && "shadow-md",
      )}
    >
      <div
        className={cn(
          publicContainerClass,
          showInlineSearch ? "space-y-3 py-4" : "py-3",
        )}
      >
        <div
          className={cn(
            "relative flex items-center gap-3",
            showCompactSearch && usesHeroHandoff && "min-h-12",
          )}
        >
          <div
            className={cn(
              "flex shrink-0 items-center gap-2 sm:gap-3",
              showCompactSearch && usesHeroHandoff && "relative z-10",
            )}
          >
            <Link href="/" className="group flex items-center gap-2.5">
              <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-primary text-sm font-bold text-primary-foreground shadow-sm transition-transform group-hover:scale-[1.02]">
                A
              </span>
              <span
                className={cn(
                  "hidden text-lg font-bold tracking-tight sm:block",
                  showCompactSearch && usesHeroHandoff && "lg:hidden",
                )}
              >
                <span className="text-foreground">Ad</span>
                <span className="text-accent">eni</span>
              </span>
            </Link>

            <LocaleCurrencySwitcher
              currentMarketId={marketId}
              currentCurrency={currency}
              countryCode={countryCode}
              mode="trigger"
              trigger={({ open }) => (
                <button
                  type="button"
                  onClick={open}
                  className="inline-flex items-center gap-1 rounded-full px-2 py-1.5 text-sm font-semibold text-foreground transition-colors hover:bg-subtle"
                  aria-label={`${t("location.changeMarket")} — ${marketName}`}
                  title={t("location.changeMarket")}
                >
                  <MapPin className="h-4 w-4 text-accent" aria-hidden />
                  <span
                    className={cn(
                      showCompactSearch && usesHeroHandoff && "hidden xl:inline",
                    )}
                  >
                    {marketName}
                  </span>
                </button>
              )}
            />
          </div>

          {(usesHeroHandoff || searchMode === "compact") && (
            <div
              className={cn(
                "pointer-events-none absolute left-1/2 top-1/2 z-30 w-[min(100%,52rem)] -translate-x-1/2 -translate-y-1/2 px-2 transition-all duration-300 ease-out sm:px-4",
                showCompactSearch
                  ? "pointer-events-auto scale-100 opacity-100"
                  : "scale-[0.98] opacity-0",
              )}
              aria-hidden={!showCompactSearch}
            >
              <HeaderDiscoverySearch variant="compact" marketName={marketName} />
            </div>
          )}

          <nav className="relative z-10 ml-auto flex shrink-0 items-center gap-1 sm:gap-2">
            <div
              className={cn(
                "hidden items-center gap-1 sm:flex sm:gap-2",
                showCompactSearch && usesHeroHandoff && "lg:hidden",
              )}
            >
              <NavLink
                href="/discover"
                label={t("nav.discover")}
                icon={Compass}
                active={isDiscover}
                className="hidden md:inline-flex"
              />
              {showBookingsNav ? (
                <NavLink
                  href="/my-bookings"
                  label={t("nav.bookings")}
                  icon={CalendarDays}
                  active={isBookings}
                  className="hidden lg:inline-flex"
                />
              ) : null}
              <Button
                href="/business/register"
                variant="secondary"
                size="sm"
                className={cn(
                  "hidden gap-1.5 lg:inline-flex",
                  isBusiness && "border-accent/30 bg-accent/10 text-accent",
                )}
              >
                <Briefcase className="h-3.5 w-3.5" aria-hidden />
                {t("nav.listBusiness")}
              </Button>
            </div>
            {showCompactSearch && usesHeroHandoff ? (
              <Link
                href="/discover"
                className="inline-flex h-9 w-9 items-center justify-center rounded-full text-foreground transition-colors hover:bg-subtle lg:hidden"
                aria-label={t("nav.discover")}
              >
                <Compass className="h-4 w-4" aria-hidden />
              </Link>
            ) : (
              <Link
                href="/discover"
                className="inline-flex h-9 w-9 items-center justify-center rounded-full text-foreground transition-colors hover:bg-subtle md:hidden"
                aria-label={t("nav.discover")}
              >
                <Compass className="h-4 w-4" aria-hidden />
              </Link>
            )}
            <AuthNavClient />
          </nav>
        </div>

        {showInlineSearch ? (
          <div className="transition-opacity duration-200">
            <HeaderDiscoverySearch variant="default" />
          </div>
        ) : null}
      </div>
    </header>
  );
}
