import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { BusinessBookingInbox } from "@/components/business-booking-inbox";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import {
  canAccessBusinessPortal,
  requireBusinessPortalAccess,
} from "@/lib/business-access";

export default async function BusinessBookingsPage() {
  if (!canAccessBusinessPortal()) {
    return (
      <BusinessPortalShell
        title="Bookings"
        description="Review and respond to customer booking requests."
      >
        <AuthSetupCallout />
      </BusinessPortalShell>
    );
  }

  const access = await requireBusinessPortalAccess("/business/bookings");

  return (
    <BusinessPortalShell
      title="Booking inbox"
      description="Accept or reject pending customer bookings."
      devMode={access.mode === "dev"}
      hasBusiness
    >
      <BusinessBookingInbox />
    </BusinessPortalShell>
  );
}
