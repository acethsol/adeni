import { AdeniRoles } from "@adeni/shared";
import { AdminSubscriptionPanel } from "@/components/admin-subscription-panel";
import { AdminCustomerPrivacyPanel } from "@/components/admin-customer-privacy-panel";
import { AdminMarketsPanel } from "@/components/admin-markets-panel";
import { AdminVerificationQueue } from "@/components/admin-verification-queue";
import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { PortalShell } from "@/components/portal-shell";
import { createAuthenticatedApiClient } from "@/lib/adeni";
import { isAuth0Configured } from "@/lib/auth/config";
import { requireRole } from "@/lib/auth/session";

export default async function AdminPortalPage() {
  if (!isAuth0Configured()) {
    return (
      <PortalShell
        title="Admin portal"
        description="Verification queue and moderation — admin role and MFA required in production."
      >
        <AuthSetupCallout />
      </PortalShell>
    );
  }

  const session = await requireRole(AdeniRoles.Admin, "/admin");

  let pending: Awaited<
    ReturnType<Awaited<ReturnType<typeof createAuthenticatedApiClient>>["getPendingBusinesses"]>
  > = [];
  let markets: Awaited<
    ReturnType<Awaited<ReturnType<typeof createAuthenticatedApiClient>>["getAdminMarkets"]>
  > = [];
  let queueError: string | null = null;
  let marketsError: string | null = null;
  let businesses: Awaited<
    ReturnType<Awaited<ReturnType<typeof createAuthenticatedApiClient>>["getAdminBusinesses"]>
  > = [];
  let businessesError: string | null = null;

  try {
    const client = await createAuthenticatedApiClient();
    const [pendingResult, marketsResult, businessesResult] = await Promise.allSettled([
      client.getPendingBusinesses(),
      client.getAdminMarkets(),
      client.getAdminBusinesses(),
    ]);

    if (pendingResult.status === "fulfilled") {
      pending = pendingResult.value;
    } else {
      queueError =
        "Could not load the verification queue. Ensure Auth0 is enabled on the API and your admin token includes the admin role (+ MFA if required).";
    }

    if (marketsResult.status === "fulfilled") {
      markets = marketsResult.value;
    } else {
      marketsError =
        "Could not load markets. Ensure your admin token includes the admin role (+ MFA if required).";
    }

    if (businessesResult.status === "fulfilled") {
      businesses = businessesResult.value;
    } else {
      businessesError =
        "Could not load businesses for subscription overrides. Ensure your admin token includes the admin role.";
    }
  } catch {
    queueError =
      "Could not load the verification queue. Ensure Auth0 is enabled on the API and your admin token includes the admin role (+ MFA if required).";
    marketsError =
      "Could not load markets. Ensure your admin token includes the admin role (+ MFA if required).";
    businessesError =
      "Could not load businesses for subscription overrides. Ensure your admin token includes the admin role.";
  }

  return (
    <PortalShell
      title="Admin portal"
      description="Verification queue, customer privacy tools — Auth0 admin role required."
    >
      <div className="rounded-xl border border-[#1b4332]/10 bg-white p-6 shadow-sm">
        <h2 className="text-lg font-semibold">Signed in as admin</h2>
        <p className="mt-2 text-sm text-[#1b4332]/70">
          {session.name ?? session.email ?? "Admin user"} · roles:{" "}
          {session.roles.join(", ")}
        </p>
      </div>

      <section className="mt-8">
        <h2 className="text-lg font-semibold">Pending verifications</h2>
        <AdminVerificationQueue initialItems={pending} initialError={queueError} />
      </section>

      <AdminMarketsPanel initialItems={markets} initialError={marketsError} />

      <section className="mt-8">
        <h2 className="text-lg font-semibold">Subscription tiers (pilots)</h2>
        <p className="mt-1 text-sm text-[#1b4332]/70">
          Override a business plan for pilot programs before Paystack billing is live.
        </p>
        <AdminSubscriptionPanel initialItems={businesses} initialError={businessesError} />
      </section>

      <AdminCustomerPrivacyPanel />
    </PortalShell>
  );
}
