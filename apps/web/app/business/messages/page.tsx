import { BusinessMessagesInbox } from "@/components/business-messages-inbox";
import { BusinessMessagingSettings } from "@/components/business-messaging-settings";
import { BusinessPortalCard } from "@/components/business-portal-card";
import { BusinessPortalShell } from "@/components/business-portal-shell";

export default function BusinessMessagesPage() {
  return (
    <BusinessPortalShell
      title="Messages"
      description="Reply to customers in-app. Pro plan includes messaging — upgrade on Plan if needed."
    >
      <BusinessPortalCard className="mb-8">
        <h2 className="text-lg font-semibold text-foreground">FAQ auto-responder</h2>
        <div className="mt-4">
          <BusinessMessagingSettings />
        </div>
      </BusinessPortalCard>
      <BusinessMessagesInbox />
    </BusinessPortalShell>
  );
}
