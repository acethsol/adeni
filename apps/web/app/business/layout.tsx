import type { ReactNode } from "react";
import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { BusinessPortalChrome } from "@/components/business-portal-chrome";
import { BusinessPortalContent } from "@/components/business-portal-content";
import { BusinessPortalTopbar } from "@/components/business-portal-topbar";
import { Callout } from "@/components/ui/callout";
import { BusinessSidebarProvider } from "@/contexts/business-sidebar-context";
import { canAccessBusinessPortal, requireBusinessPortalAccess } from "@/lib/business-access";
import { createBusinessApiClient } from "@/lib/business-api";
import type { BusinessProfile } from "@adeni/shared";

export default async function BusinessLayout({ children }: { children: ReactNode }) {
  const configured = canAccessBusinessPortal();

  if (!configured) {
    return (
      <BusinessSidebarProvider>
        <div className="min-h-screen bg-subtle text-foreground">
          <BusinessPortalChrome hasBusiness={false} />
          <BusinessPortalContent>
            <div className="mx-auto w-full max-w-[1600px] px-4 py-8 sm:px-6 lg:px-10 lg:py-10">
              <AuthSetupCallout />
            </div>
          </BusinessPortalContent>
        </div>
      </BusinessSidebarProvider>
    );
  }

  const access = await requireBusinessPortalAccess("/business");

  let profile: BusinessProfile | null = null;
  try {
    const client = await createBusinessApiClient();
    profile = await client.getTenantProfile();
  } catch {
    profile = null;
  }

  const primaryLocation = profile?.locations.find((location) => location.isPrimary) ?? profile?.locations[0];

  return (
    <BusinessSidebarProvider>
      <div className="min-h-screen bg-subtle text-foreground">
        <BusinessPortalChrome hasBusiness={Boolean(profile)} capabilities={profile?.capabilities} />
        <BusinessPortalContent
          topbar={
            <BusinessPortalTopbar
              businessName={profile?.businessName ?? null}
              status={profile?.status ?? null}
              primarySlug={primaryLocation?.slug ?? null}
              sessionName={access.session?.name ?? null}
              sessionEmail={access.session?.email ?? null}
              mode={access.mode}
            />
          }
        >
          {access.mode === "dev" ? (
            <div className="mx-auto w-full max-w-[1600px] px-4 pt-6 sm:px-6 lg:px-10">
              <Callout tone="success">
                Local dev mode — using <code className="text-xs">DEV_BUSINESS_AUTH0_SUB</code>{" "}
                (linked to <code className="text-xs">lekki-cuts</code> in dev seed).
              </Callout>
            </div>
          ) : null}
          {children}
        </BusinessPortalContent>
      </div>
    </BusinessSidebarProvider>
  );
}
