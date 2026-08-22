import { BusinessPortalShell } from "@/components/business-portal-shell";
import { BusinessServicesManager } from "@/components/business-services-manager";
import { createBusinessApiClient } from "@/lib/business-api";

export default async function BusinessServicesPage() {
  let services: Awaited<
    ReturnType<Awaited<ReturnType<typeof createBusinessApiClient>>["getTenantServices"]>
  > = [];
  let defaultCurrency = "NGN";
  let loadError: string | null = null;

  try {
    const client = await createBusinessApiClient();
    const [serviceItems, profile] = await Promise.all([
      client.getTenantServices(),
      client.getTenantProfile().catch(() => null),
    ]);
    services = serviceItems;
    if (profile?.locations[0]?.marketId) {
      const marketId = profile.locations[0].marketId.toLowerCase();
      defaultCurrency =
        marketId === "lagos" || marketId === "abuja"
          ? "NGN"
          : marketId === "ottawa" || marketId === "toronto"
            ? "CAD"
            : "USD";
    } else if (services[0]?.currency) {
      defaultCurrency = services[0].currency;
    }
  } catch {
    loadError = "Could not load services.";
  }

  return (
    <BusinessPortalShell
      title="Services"
      description="Add, edit, and deactivate bookable services on your public profile."
    >
      {loadError ? (
        <p className="text-sm text-muted">{loadError}</p>
      ) : (
        <BusinessServicesManager
          initialServices={services}
          defaultCurrency={defaultCurrency}
        />
      )}
    </BusinessPortalShell>
  );
}
