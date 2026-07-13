import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { BusinessAlreadyRegistered } from "@/components/business-already-registered";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import { BusinessRegisterForm } from "@/components/business-register-form";
import {
  canAccessBusinessPortal,
  requireBusinessPortalAccess,
} from "@/lib/business-access";
import { createApiClient } from "@/lib/adeni";
import { createBusinessApiClient } from "@/lib/business-api";
import { getMarkets } from "@/lib/markets-api";

export default async function BusinessRegisterPage() {
  if (!canAccessBusinessPortal()) {
    return (
      <BusinessPortalShell
        title="Join Adeni"
        description="Create your business profile and start accepting bookings in your city."
      >
        <AuthSetupCallout />
      </BusinessPortalShell>
    );
  }

  await requireBusinessPortalAccess("/business/register");

  const categoriesClient = createApiClient();
  const [categories, markets] = await Promise.all([
    categoriesClient.getCategories(),
    getMarkets(),
  ]);

  let profile = null;
  try {
    const client = await createBusinessApiClient();
    profile = await client.getTenantProfile();
  } catch {
    profile = null;
  }

  if (profile) {
    const primaryLocation =
      profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];

    return (
      <BusinessPortalShell
        title="Register your business"
        description="Your account is already linked to a business on Adeni."
        hasBusiness
      >
        <BusinessAlreadyRegistered
          businessName={profile.businessName}
          categorySlug={profile.categorySlug}
          status={profile.status}
          primarySlug={primaryLocation?.slug}
          primaryLocationName={primaryLocation?.name}
        />
      </BusinessPortalShell>
    );
  }

  return (
    <BusinessPortalShell
      title="Register your business"
      description="Tell customers who you are, where you are, and what you offer. Verification comes next."
    >
      <BusinessRegisterForm categories={categories} markets={markets} />
    </BusinessPortalShell>
  );
}
