import { BusinessBookingInbox } from "@/components/business-booking-inbox";
import { BusinessPortalShell } from "@/components/business-portal-shell";

export default function BusinessBookingsPage() {
  return (
    <BusinessPortalShell
      title="Booking inbox"
      description="Accept or reject pending customer bookings."
    >
      <BusinessBookingInbox />
    </BusinessPortalShell>
  );
}
