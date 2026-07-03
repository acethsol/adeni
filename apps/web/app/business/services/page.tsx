import Link from "next/link";
import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import {
  canAccessBusinessPortal,
  requireBusinessPortalAccess,
} from "@/lib/business-access";
import { createBusinessApiClient } from "@/lib/business-api";

function formatPrice(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

export default async function BusinessServicesPage() {
  if (!canAccessBusinessPortal()) {
    return (
      <BusinessPortalShell title="Services" description="Manage your bookable services.">
        <AuthSetupCallout />
      </BusinessPortalShell>
    );
  }

  const access = await requireBusinessPortalAccess("/business/services");

  let services: Awaited<
    ReturnType<Awaited<ReturnType<typeof createBusinessApiClient>>["getTenantServices"]>
  > = [];
  let loadError: string | null = null;

  try {
    const client = await createBusinessApiClient();
    services = await client.getTenantServices();
  } catch {
    loadError = "Could not load services.";
  }

  return (
    <BusinessPortalShell
      title="Services"
      description="Services shown on your public profile and booking flow."
      devMode={access.mode === "dev"}
    >
      {loadError ? (
        <p className="text-sm text-[#1b4332]/70">{loadError}</p>
      ) : services.length === 0 ? (
        <p className="text-sm text-[#1b4332]/70">No services yet.</p>
      ) : (
        <ul className="divide-y divide-[#1b4332]/10 rounded-xl border border-[#1b4332]/10 bg-white">
          {services.map((service) => (
            <li key={service.id} className="flex items-center justify-between px-5 py-4">
              <div>
                <p className="font-medium">{service.name}</p>
                <p className="text-sm text-[#1b4332]/60">
                  {service.durationMinutes} min ·{" "}
                  {formatPrice(service.priceAmount, service.currency)}
                </p>
                {service.description ? (
                  <p className="mt-1 text-sm text-[#1b4332]/70">{service.description}</p>
                ) : null}
              </div>
              <span
                className={`text-xs font-semibold uppercase tracking-wide ${
                  service.isActive ? "text-[#40916c]" : "text-[#1b4332]/40"
                }`}
              >
                {service.isActive ? "Active" : "Inactive"}
              </span>
            </li>
          ))}
        </ul>
      )}

      <p className="mt-6 text-sm text-[#1b4332]/60">
        Service create/edit UI is next. API endpoints are ready at{" "}
        <code className="text-xs">/api/v1/tenant/services</code>.
      </p>

      <Link href="/business" className="mt-4 inline-block text-sm font-medium text-[#40916c]">
        ← Back to overview
      </Link>
    </BusinessPortalShell>
  );
}
