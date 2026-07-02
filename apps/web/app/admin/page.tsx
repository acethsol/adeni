import { AdeniRoles } from "@adeni/shared";
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
  let queueError: string | null = null;

  try {
    const client = await createAuthenticatedApiClient();
    pending = await client.getPendingBusinesses();
  } catch {
    queueError =
      "Could not load the verification queue. Ensure Auth0 is enabled on the API and your admin token includes the admin role (+ MFA if required).";
  }

  return (
    <PortalShell
      title="Admin portal"
      description="Verification queue and moderation — Auth0 admin role required."
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
        {queueError ? (
          <p className="mt-4 text-sm text-[#1b4332]/70">{queueError}</p>
        ) : pending.length === 0 ? (
          <p className="mt-4 text-sm text-[#1b4332]/70">No businesses awaiting review.</p>
        ) : (
          <ul className="mt-4 divide-y divide-[#1b4332]/10 rounded-xl border border-[#1b4332]/10 bg-white">
            {pending.map((business) => (
              <li key={business.id} className="flex items-center justify-between px-5 py-4">
                <div>
                  <p className="font-medium">{business.name}</p>
                  <p className="text-sm text-[#1b4332]/60">
                    {business.slug} · {business.status}
                  </p>
                </div>
                <time className="text-xs text-[#1b4332]/50">
                  {new Date(business.createdAt).toLocaleDateString()}
                </time>
              </li>
            ))}
          </ul>
        )}
      </section>
    </PortalShell>
  );
}
