import { AdeniRoles } from "@adeni/shared";
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

  try {
    const client = await createAuthenticatedApiClient();
    pending = await client.getPendingBusinesses();
  } catch {
    queueError =
      "Could not load the verification queue. Ensure Auth0 is enabled on the API and your admin token includes the admin role (+ MFA if required).";
  }

  try {
    const client = await createAuthenticatedApiClient();
    markets = await client.getAdminMarkets();
  } catch {
    marketsError =
      "Could not load markets. Ensure your admin token includes the admin role (+ MFA if required).";
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

      <AdminCustomerPrivacyPanel />
    </PortalShell>
  );
}
