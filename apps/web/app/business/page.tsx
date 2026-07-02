import { AdeniRoles } from "@adeni/shared";
import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { PortalShell } from "@/components/portal-shell";
import { createAuthenticatedApiClient } from "@/lib/adeni";
import { isAuth0Configured } from "@/lib/auth/config";
import { requireRole } from "@/lib/auth/session";

export default async function BusinessPortalPage() {
  if (!isAuth0Configured()) {
    return (
      <PortalShell
        title="Business portal"
        description="Manage your profile, services, and availability after signing in with a business account."
      >
        <AuthSetupCallout />
      </PortalShell>
    );
  }

  const session = await requireRole(AdeniRoles.Business, "/business");

  let apiSession: Awaited<
    ReturnType<Awaited<ReturnType<typeof createAuthenticatedApiClient>>["getMe"]>
  > | null = null;
  let apiError: string | null = null;

  try {
    const client = await createAuthenticatedApiClient();
    apiSession = await client.getMe();
    client.setTenantId(apiSession.tenantId);
  } catch {
    apiError =
      "Could not load API session. Enable Auth0 on the API (Auth0:Enabled) for full end-to-end auth.";
  }

  return (
    <PortalShell
      title="Business portal"
      description="Onboarding, profile, services, and availability — business role required."
    >
      <div className="rounded-xl border border-[#1b4332]/10 bg-white p-6 shadow-sm">
        <h2 className="text-lg font-semibold">Signed in</h2>
        <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-[#1b4332]/60">Name</dt>
            <dd className="font-medium">{session.name ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-[#1b4332]/60">Email</dt>
            <dd className="font-medium">{session.email ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-[#1b4332]/60">Roles</dt>
            <dd className="font-medium">{session.roles.join(", ") || "—"}</dd>
          </div>
          <div>
            <dt className="text-[#1b4332]/60">Tenant</dt>
            <dd className="font-medium">
              {apiSession?.tenantId ?? session.tenantId ?? "Not linked yet"}
            </dd>
          </div>
        </dl>

        {apiError ? (
          <p className="mt-4 text-sm text-[#1b4332]/70">{apiError}</p>
        ) : apiSession ? (
          <p className="mt-4 text-sm text-[#40916c]">
            API session verified{apiSession.hasMfa ? " · MFA present" : ""}.
          </p>
        ) : null}
      </div>

      <p className="mt-6 text-sm text-[#1b4332]/70">
        Profile editing and verification submission will land in the next business portal
        sprint. You are authenticated and routed correctly.
      </p>
    </PortalShell>
  );
}
