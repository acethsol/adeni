import { Star } from "lucide-react";
import { BusinessBadgeUpgrade } from "@/components/business-badge-upgrade";
import { BusinessShareKit } from "@/components/business-share-kit";
import { BusinessPortalCard } from "@/components/business-portal-card";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import { BusinessCoverUpload } from "@/components/business-cover-upload";
import { BusinessBookingSettings } from "@/components/business-booking-settings";
import { BusinessMessagingSettings } from "@/components/business-messaging-settings";
import { BusinessNotificationSettings } from "@/components/business-notification-settings";
import { BusinessProfileForm } from "@/components/business-profile-form";
import { BusinessReviewsPanel } from "@/components/business-reviews-panel";
import { BusinessVerificationForm } from "@/components/business-verification-form";
import { BusinessVerificationStatus } from "@/components/business-verification-status";
import { createBusinessApiClient } from "@/lib/business-api";

export default async function BusinessProfilePage() {
  let profile = null;
  let loadError: string | null = null;

  try {
    const client = await createBusinessApiClient();
    profile = await client.getTenantProfile();
  } catch {
    loadError = "Could not load profile.";
  }

  const primaryLocation = profile?.locations.find((item) => item.isPrimary) ?? profile?.locations[0];

  return (
    <BusinessPortalShell
      title="Profile"
      description="Update your business details shown to customers."
    >
      {loadError ? (
        <p className="text-sm text-muted">{loadError}</p>
      ) : profile ? (
        <>
          {primaryLocation ? (
            <BusinessShareKit
              businessName={profile.businessName}
              slug={primaryLocation.slug}
              phone={profile.phone}
            />
          ) : null}

          <div className="mt-8 grid gap-8 lg:grid-cols-2 xl:grid-cols-3">
            <BusinessPortalCard>
              <h2 className="text-lg font-semibold text-foreground">Verification status</h2>
              <div className="mt-4">
                <BusinessVerificationStatus profile={profile} />
              </div>
            </BusinessPortalCard>

            <BusinessPortalCard>
              <h2 className="text-lg font-semibold text-foreground">Cover photo</h2>
              <p className="mt-1 text-sm text-muted">
                Shown on discovery cards and your public profile.
              </p>
              <div className="mt-4">
                <BusinessCoverUpload
                  categorySlug={profile.categorySlug}
                  coverImageUrl={profile.coverImageUrl}
                />
              </div>
            </BusinessPortalCard>

            <BusinessPortalCard>
              <h2 className="text-lg font-semibold text-foreground">Booking settings</h2>
              <div className="mt-4">
                <BusinessBookingSettings profile={profile} />
              </div>
            </BusinessPortalCard>

            <BusinessPortalCard>
              <h2 className="text-lg font-semibold text-foreground">Notification preferences</h2>
              <div className="mt-4">
                <BusinessNotificationSettings />
              </div>
            </BusinessPortalCard>

            <BusinessPortalCard>
              <h2 className="text-lg font-semibold text-foreground">Messaging settings</h2>
              <div className="mt-4">
                <BusinessMessagingSettings />
              </div>
            </BusinessPortalCard>

            <BusinessPortalCard>
              <h2 className="text-lg font-semibold text-foreground">Edit profile</h2>
              <div className="mt-4">
                <BusinessProfileForm profile={profile} />
              </div>
            </BusinessPortalCard>
          </div>

          <BusinessVerificationForm canSubmit={profile.status === 0 || profile.status === 3} />

          <div className="mt-8">
            <BusinessBadgeUpgrade />
          </div>

          <BusinessPortalCard padding="lg" className="mt-8">
            <div className="flex items-center gap-2">
              <Star className="h-5 w-5 text-accent" aria-hidden />
              <h2 className="text-lg font-semibold text-foreground">Customer reviews</h2>
            </div>
            <p className="mt-1 text-sm text-muted">
              What customers are saying about your primary location.
            </p>
            <div className="mt-5">
              <BusinessReviewsPanel />
            </div>
          </BusinessPortalCard>
        </>
      ) : null}
    </BusinessPortalShell>
  );
}
