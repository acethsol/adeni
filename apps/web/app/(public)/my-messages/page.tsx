import { Suspense } from "react";
import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { CustomerMessagesPageClient } from "@/components/customer-messages-page-client";
import { PublicHeader } from "@/components/public-header";
import { PageHeader } from "@/components/ui/page-header";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { LoadingPanel } from "@/components/loading-panel";
import {
  canAccessMyBookings,
  hasMyBookingsSession,
} from "@/lib/customer-access";
import { isCustomerDevMode } from "@/lib/customer-api";
import { publicContainerClass } from "@/lib/layout-classes";
import { getActiveMarketConfig } from "@/lib/market";

export default async function MyMessagesPage() {
  const market = await getActiveMarketConfig();

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
        <main id="main-content" className={`${publicContainerClass} py-12 lg:py-14`}>
          <div className="mx-auto max-w-4xl">
            <PageHeader
              title="Messages"
              description="Sign in to message businesses on Adeni."
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
        showMessagesNav={canAccessMyBookings()}
      />

      <main id="main-content" className={`${publicContainerClass} py-12 lg:py-14`}>
        <div className="mx-auto w-full max-w-5xl">
          <PageHeader
            title="Messages"
            description="Chat with businesses you have booked or contacted on Adeni."
            actions={
              <Button href="/discover" variant="secondary" size="sm">
                Find a business
              </Button>
            }
          />

          {isCustomerDevMode() ? (
            <Callout tone="info" className="mt-6">
              Dev customer mode — messages use your configured dev Auth0 sub.
            </Callout>
          ) : null}

          {!hasSession ? (
            <div className="mt-8 rounded-2xl border border-border bg-surface p-8 text-center shadow-sm">
              <p className="font-semibold text-foreground">Sign in to view messages</p>
              <p className="mt-2 text-sm text-muted">
                Your in-app conversations with businesses appear here.
              </p>
              <Button href="/auth/login?returnTo=/my-messages" className="mt-5">
                Log in
              </Button>
            </div>
          ) : (
            <div className="mt-8">
              <Suspense fallback={<LoadingPanel message="Loading messages…" variant="card" />}>
                <CustomerMessagesPageClient />
              </Suspense>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
