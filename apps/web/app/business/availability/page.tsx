import { BusinessAvailabilityEditor } from "@/components/business-availability-editor";
import { BusinessPortalShell } from "@/components/business-portal-shell";

export default function BusinessAvailabilityPage() {
  return (
    <BusinessPortalShell
      title="Weekly availability"
      description="Customers can only book during these hours."
    >
      <BusinessAvailabilityEditor />
    </BusinessPortalShell>
  );
}
