import { BusinessMessagesInbox } from "@/components/business-messages-inbox";
import { BusinessPortalShell } from "@/components/business-portal-shell";

export default function BusinessMessagesPage() {
  return (
    <BusinessPortalShell
      title="Messages"
      description="Reply to customers in-app. Pro plan includes messaging — upgrade on Plan if needed."
    >
      <BusinessMessagesInbox />
    </BusinessPortalShell>
  );
}
