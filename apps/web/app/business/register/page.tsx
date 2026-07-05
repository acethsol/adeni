import Link from "next/link";
import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import { BusinessRegisterForm } from "@/components/business-register-form";
import {
  canAccessBusinessPortal,
  requireBusinessPortalAccess,
} from "@/lib/business-access";
import { createApiClient } from "@/lib/adeni";
import { createBusinessApiClient } from "@/lib/business-api";

export default async function BusinessRegisterPage() {
  if (!canAccessBusinessPortal()) {
    return (
      <BusinessPortalShell
        title="Register your business"
        description="Create your Adeni business profile and submit for verification."
      >
        <AuthSetupCallout />
      </BusinessPortalShell>
    );
  }

  await requireBusinessPortalAccess("/business/register");

  const categoriesClient = createApiClient();
  const categories = await categoriesClient.getCategories();

  let hasBusiness = false;
  try {
    const client = await createBusinessApiClient();
    await client.getTenantProfile();
    hasBusiness = true;
  } catch {
    hasBusiness = false;
  }

  if (hasBusiness) {
    return (
      <BusinessPortalShell
        title="Register your business"
        description="You already have a business linked to this account."
      >
        <div className="rounded-xl border border-[#1b4332]/10 bg-white p-8 text-center shadow-sm">
          <p className="font-medium">Business account found</p>
          <p className="mt-2 text-sm text-[#1b4332]/70">
            Continue to your portal to manage profile, services, and bookings.
          </p>
          <Link
            href="/business"
            className="mt-5 inline-block rounded-full bg-[#1b4332] px-5 py-2.5 text-sm font-medium text-white"
          >
            Go to business portal
          </Link>
        </div>
      </BusinessPortalShell>
    );
  }

  return (
    <BusinessPortalShell
      title="Register your business"
      description="Tell customers who you are, where you are, and what you offer. You can submit verification documents after registering."
    >
      <BusinessRegisterForm categories={categories} />
    </BusinessPortalShell>
  );
}
