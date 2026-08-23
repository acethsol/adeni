import { BusinessPlanComparison } from "@/components/business-plan-comparison";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import { SubscriptionUsageMeter } from "@/components/subscription-usage-meter";
import { createBusinessApiClient } from "@/lib/business-api";
import type { SubscriptionTier } from "@adeni/shared";

export default async function BusinessPlanPage() {
  let currentTier: SubscriptionTier = "free";
  let usage = null;
  let loadError: string | null = null;

  try {
    const client = await createBusinessApiClient();
    const [profile, subscriptionUsage] = await Promise.all([
      client.getTenantProfile(),
      client.getTenantSubscriptionUsage(),
    ]);
    currentTier = profile.subscriptionTier ?? subscriptionUsage.tier;
    usage = subscriptionUsage;
  } catch {
    loadError = "Could not load plan details.";
  }

  return (
    <BusinessPortalShell
      title="Plans & billing"
      description="Compare Adeni plans, track usage, and upgrade when you're ready."
    >
      {loadError ? (
        <p className="text-sm text-muted">{loadError}</p>
      ) : (
        <div className="space-y-8">
          {usage ? <SubscriptionUsageMeter usage={usage} /> : null}
          <BusinessPlanComparison currentTier={currentTier} />
        </div>
      )}
    </BusinessPortalShell>
  );
}
