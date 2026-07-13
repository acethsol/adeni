import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { MyBookingsList } from "@/components/my-bookings-list";
import { PublicHeader } from "@/components/public-header";
import { PageHeader } from "@/components/ui/page-header";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { t } from "@adeni/shared";
import {
  canAccessMyBookings,
  hasMyBookingsSession,
} from "@/lib/customer-access";
import { isCustomerDevMode } from "@/lib/customer-api";
import { publicContainerClass } from "@/lib/layout-classes";
import { getLocale } from "@/lib/locale";
import { getActiveMarketConfig } from "@/lib/market";

export default async function MyBookingsPage() {
  const [market, locale] = await Promise.all([getActiveMarketConfig(), getLocale()]);

  if (!canAccessMyBookings()) {
    return (
      <div className="flex flex-1 flex-col">
        <PublicHeader
          searchMode="none"
          marketId={market.id}
          marketName={market.name}
          currency={market.currency}
          countryCode={market.countryCode}
        />
        <main className={`${publicContainerClass} py-12 lg:py-14`}>
          <div className="mx-auto max-w-4xl">
            <PageHeader
              title={t(locale, "bookings.title")}
              description={t(locale, "bookings.signInDescription")}
            />
            <div className="mt-8">
              <AuthSetupCallout />
            </div>
          </div>
        </main>
      </div>
    );
  }

  const hasSession = await hasMyBookingsSession();

  return (
    <div className="flex flex-1 flex-col">
      <PublicHeader
        searchMode="none"
        marketId={market.id}
        marketName={market.name}
        currency={market.currency}
        countryCode={market.countryCode}
        showBookingsNav={canAccessMyBookings()}
      />

      <main className={`${publicContainerClass} py-12 lg:py-14`}>
        <div className="mx-auto w-full max-w-4xl">
          <PageHeader
            title={t(locale, "bookings.title")}
            description={t(locale, "bookings.description")}
            actions={
              <Button href="/discover" variant="secondary" size="sm">
                {t(locale, "bookings.bookAnother")}
              </Button>
            }
          />

          {isCustomerDevMode() ? (
            <Callout tone="info" className="mt-6">
              {t(locale, "bookings.devMode")}
            </Callout>
          ) : null}

          {!hasSession ? (
            <div className="mt-8 rounded-2xl border border-border bg-surface p-8 text-center shadow-sm">
              <p className="font-semibold text-foreground">{t(locale, "bookings.signInTitle")}</p>
              <p className="mt-2 text-sm text-muted">{t(locale, "bookings.signInHint")}</p>
              <Button href="/auth/login?returnTo=/my-bookings" className="mt-5">
                {t(locale, "bookings.logIn")}
              </Button>
            </div>
          ) : (
            <div className="mt-8">
              <MyBookingsList />
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
