import { formatTenantStatus, VERIFICATION_DOCUMENT_LABELS } from "@adeni/shared";
import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import { BusinessCoverUpload } from "@/components/business-cover-upload";
import { BusinessProfileForm } from "@/components/business-profile-form";
import { BusinessVerificationForm } from "@/components/business-verification-form";
import {
  canAccessBusinessPortal,
  requireBusinessPortalAccess,
} from "@/lib/business-access";
import { createBusinessApiClient } from "@/lib/business-api";

export default async function BusinessProfilePage() {
  if (!canAccessBusinessPortal()) {
    return (
      <BusinessPortalShell
        title="Profile"
        description="Edit your business profile and verification details."
      >
        <AuthSetupCallout />
      </BusinessPortalShell>
    );
  }

  const access = await requireBusinessPortalAccess("/business/profile");

  let profile = null;
  let loadError: string | null = null;

  try {
    const client = await createBusinessApiClient();
    profile = await client.getTenantProfile();
  } catch {
    loadError = "Could not load profile.";
  }

  return (
    <BusinessPortalShell
      title="Profile"
      description="Update your business details shown to customers."
      devMode={access.mode === "dev"}
    >
      {loadError ? (
        <p className="text-sm text-[#1b4332]/70">{loadError}</p>
      ) : profile ? (
        <>
          <div className="grid gap-8 lg:grid-cols-2">
            <div className="rounded-xl border border-[#1b4332]/10 bg-white p-6 shadow-sm">
              <h2 className="text-lg font-semibold">Status</h2>
              <dl className="mt-4 space-y-3 text-sm">
                <div>
                  <dt className="text-[#1b4332]/60">Verification</dt>
                  <dd className="font-medium">{formatTenantStatus(profile.status)}</dd>
                </div>
                <div>
                  <dt className="text-[#1b4332]/60">Locations</dt>
                  <dd className="font-medium">{profile.locations.length}</dd>
                </div>
                {profile.locations.map((location) => (
                  <div key={location.id}>
                    <dt className="text-[#1b4332]/60">{location.name}</dt>
                    <dd className="font-medium">
                      {location.area} · {location.marketId} · /businesses/{location.slug}
                    </dd>
                  </div>
                ))}
                {profile.verificationDocuments.length > 0 ? (
                  <div>
                    <dt className="text-[#1b4332]/60">Submitted documents</dt>
                    <dd className="mt-1 space-y-1 font-medium">
                      {profile.verificationDocuments.map((doc) => (
                        <p key={`${doc.documentType}-${doc.submittedAt}`}>
                          {VERIFICATION_DOCUMENT_LABELS[doc.documentType] ?? "Document"} ·{" "}
                          {new Date(doc.submittedAt).toLocaleDateString()}
                        </p>
                      ))}
                    </dd>
                  </div>
                ) : null}
              </dl>
            </div>

            <div className="rounded-xl border border-[#1b4332]/10 bg-white p-6 shadow-sm">
              <h2 className="text-lg font-semibold">Cover photo</h2>
              <p className="mt-1 text-sm text-[#1b4332]/60">
                Shown on discovery cards and your public profile.
              </p>
              <div className="mt-4">
                <BusinessCoverUpload
                  categorySlug={profile.categorySlug}
                  coverImageUrl={profile.coverImageUrl}
                />
              </div>
            </div>

            <div className="rounded-xl border border-[#1b4332]/10 bg-white p-6 shadow-sm">
              <h2 className="text-lg font-semibold">Edit profile</h2>
              <div className="mt-4">
                <BusinessProfileForm profile={profile} />
              </div>
            </div>
          </div>

          <BusinessVerificationForm canSubmit={profile.status === 0 || profile.status === 3} />
        </>
      ) : null}
    </BusinessPortalShell>
  );
}
