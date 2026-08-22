import { BusinessLocationsManager } from "@/components/business-locations-manager";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import { createBusinessApiClient } from "@/lib/business-api";
import { getMarkets } from "@/lib/markets-api";

export default async function BusinessLocationsPage() {
  let locations: Awaited<
    ReturnType<Awaited<ReturnType<typeof createBusinessApiClient>>["getTenantLocations"]>
  > = [];
  let defaultMarketId = "lagos";
  let loadError: string | null = null;
  const markets = await getMarkets();

  try {
    const client = await createBusinessApiClient();
    locations = await client.getTenantLocations();
    const primary = locations.find((item) => item.isPrimary) ?? locations[0];
    if (primary?.marketId) {
      defaultMarketId = primary.marketId;
    }
  } catch {
    loadError = "Could not load locations.";
  }

  return (
    <BusinessPortalShell
      title="Locations"
      description="Add branches, set your primary location, and manage public profile URLs."
    >
      {loadError ? (
        <p className="text-sm text-muted">{loadError}</p>
      ) : (
        <BusinessLocationsManager
          initialLocations={locations}
          defaultMarketId={defaultMarketId}
          markets={markets}
        />
      )}
    </BusinessPortalShell>
  );
}
