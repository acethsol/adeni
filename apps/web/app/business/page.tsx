import Link from "next/link";
import { formatTenantStatus } from "@adeni/shared";
import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import {
  canAccessBusinessPortal,
  requireBusinessPortalAccess,
} from "@/lib/business-access";
import { createBusinessApiClient } from "@/lib/business-api";

export default async function BusinessPortalPage() {
  if (!canAccessBusinessPortal()) {
    return (
      <BusinessPortalShell
        title="Business portal"
        description="Manage your profile, services, bookings, and availability."
      >
        <AuthSetupCallout />
        <p className="mt-6 text-sm text-[#1b4332]/70">
          For local dev without Auth0, set{" "}
          <code className="text-xs">DEV_BUSINESS_AUTH0_SUB=auth0|local-business</code> in{" "}
          <code className="text-xs">.env.local</code> (linked to{" "}
          <code className="text-xs">lekki-cuts</code> in dev seed).
        </p>
      </BusinessPortalShell>
    );
  }

  const access = await requireBusinessPortalAccess("/business");

  let profile = null;
  let bookingsCount = 0;
  let servicesCount = 0;
  let loadError: string | null = null;

  try {
    const client = await createBusinessApiClient();
    profile = await client.getTenantProfile();
    const [bookings, services] = await Promise.all([
      client.getTenantBookings(),
      client.getTenantServices(),
    ]);
    bookingsCount = bookings.filter((item) => item.status === 0).length;
    servicesCount = services.filter((item) => item.isActive).length;
  } catch {
    loadError =
      "Could not load business data. Ensure the API is running and your business account is linked.";
  }

  return (
    <BusinessPortalShell
      title="Overview"
      description="Manage bookings, services, and your public business profile."
      devMode={access.mode === "dev"}
    >
      {access.session ? (
        <p className="mb-6 text-sm text-[#1b4332]/70">
          Signed in as {access.session.name ?? access.session.email ?? "business user"}
        </p>
      ) : null}

      {loadError ? (
        <p className="text-sm text-[#1b4332]/70">{loadError}</p>
      ) : profile ? (
        <>
          <div className="grid gap-4 sm:grid-cols-3">
            <StatCard label="Status" value={formatTenantStatus(profile.status)} />
            <StatCard label="Pending bookings" value={String(bookingsCount)} />
            <StatCard label="Active services" value={String(servicesCount)} />
          </div>

          <div className="mt-8 rounded-xl border border-[#1b4332]/10 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold">{profile.businessName}</h2>
            <p className="mt-2 text-sm text-[#1b4332]/70">
              {profile.categorySlug.replace(/-/g, " ")} · {profile.phone}
            </p>
            {profile.locations[0] ? (
              <p className="mt-2 text-sm text-[#1b4332]/60">
                Primary location: {profile.locations[0].name} ({profile.locations[0].slug})
              </p>
            ) : null}
          </div>

          <div className="mt-8 flex flex-wrap gap-3">
            <Link
              href="/business/bookings"
              className="rounded-full bg-[#1b4332] px-5 py-2.5 text-sm font-medium text-white"
            >
              Open booking inbox
            </Link>
            <Link
              href="/business/services"
              className="rounded-full border border-[#1b4332]/20 px-5 py-2.5 text-sm font-medium"
            >
              Manage services
            </Link>
            <Link
              href="/business/profile"
              className="rounded-full border border-[#1b4332]/20 px-5 py-2.5 text-sm font-medium"
            >
              Edit profile
            </Link>
          </div>
        </>
      ) : null}
    </BusinessPortalShell>
  );
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-[#1b4332]/10 bg-white p-5 shadow-sm">
      <p className="text-xs font-bold uppercase tracking-wider text-[#40916c]">{label}</p>
      <p className="mt-2 text-2xl font-bold">{value}</p>
    </div>
  );
}
