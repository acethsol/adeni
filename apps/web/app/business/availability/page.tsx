import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { BusinessAvailabilityEditor } from "@/components/business-availability-editor";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import {
  canAccessBusinessPortal,
  requireBusinessPortalAccess,
} from "@/lib/business-access";

export default async function BusinessAvailabilityPage() {
  if (!canAccessBusinessPortal()) {
    return (
      <BusinessPortalShell
        title="Availability"
        description="Set your weekly opening hours for online booking."
      >
        <AuthSetupCallout />
      </BusinessPortalShell>
    );
  }

  const access = await requireBusinessPortalAccess("/business/availability");

  return (
    <BusinessPortalShell
      title="Weekly availability"
      description="Customers can only book during these hours."
      devMode={access.mode === "dev"}
    >
      <BusinessAvailabilityEditor />
    </BusinessPortalShell>
  );
}
