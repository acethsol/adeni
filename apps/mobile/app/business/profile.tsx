import { useCallback, useEffect, useState } from "react";
import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { useRouter } from "expo-router";
import type { AdeniApiError } from "@adeni/api-client";
import type { BusinessProfile } from "@adeni/shared";
import { VERIFICATION_DOCUMENT_LABELS } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { BusinessTabs } from "@/components/adeni/BusinessTabs";
import { BusinessCoverUpload } from "@/components/adeni/BusinessCoverUpload";
import { BusinessBadgeUpgrade } from "@/components/adeni/BusinessBadgeUpgrade";
import { BusinessReviewsPanel } from "@/components/adeni/BusinessReviewsPanel";
import { BusinessPortalSettings } from "@/components/adeni/BusinessPortalSettings";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Callout } from "@/components/ui/Callout";
import { useAuth } from "@/contexts/auth-context";
import { formatTenantStatus } from "@/lib/format";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

export default function BusinessProfileScreen() {
  const router = useRouter();
  const {
    loading: authLoading,
    isBusinessPortalEnabled,
    hasBusinessAccount,
    createBusinessApiClient,
  } = useAuth();

  const [profile, setProfile] = useState<BusinessProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadProfile = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const nextProfile = await client.getTenantProfile();
      setProfile(nextProfile);
    } catch (err) {
      const apiError = err as AdeniApiError;
      if (apiError.statusCode === 401 || apiError.statusCode === 404) {
        setError("No business profile found. Register your business first.");
      } else {
        setError("Could not load business profile.");
      }
      setProfile(null);
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient]);

  useEffect(() => {
    if (authLoading) {
      return;
    }

    if (!isBusinessPortalEnabled) {
      setLoading(false);
      return;
    }

    if (!hasBusinessAccount) {
      setLoading(false);
      setError("Register your business to manage your profile.");
      return;
    }

    void loadProfile();
  }, [authLoading, hasBusinessAccount, isBusinessPortalEnabled, loadProfile]);

  const canSubmitVerification = profile?.status === 0 || profile?.status === 3;
  const canManage = isBusinessPortalEnabled && hasBusinessAccount;

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Profile"
          subtitle="Update your public details and submit verification documents."
        />

        {canManage ? <BusinessTabs /> : null}

        <View style={styles.section}>
          {!isBusinessPortalEnabled ? (
            <Callout title="Sign in required">
              {isAuth0Configured()
                ? "Sign in from the Account tab to manage your business."
                : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB in .env for local business mode."}
            </Callout>
          ) : null}

          {!hasBusinessAccount && isBusinessPortalEnabled ? (
            <>
              <Callout title="No business yet">
                You have not registered a business on this account yet.
              </Callout>
              <Button
                title="Register business"
                onPress={() => router.push("/business/register")}
                containerStyle={styles.registerButton}
              />
            </>
          ) : null}

          {error && !profile ? <Callout tone="error">{error}</Callout> : null}

          {profile ? (
            <>
              <Card style={styles.statusCard} title="Status">
                <View style={styles.statusRow}>
                  <Badge
                    label={formatTenantStatus(profile.status)}
                    tone={profile.status === 2 ? "success" : profile.status === 3 ? "destructive" : "default"}
                  />
                </View>
                <DetailRow
                  label="Primary location"
                  value={
                    profile.locations[0]
                      ? `${profile.locations[0].name} · /businesses/${profile.locations[0].slug}`
                      : "—"
                  }
                />
              </Card>

              <View style={styles.blockSpacing}>
                <BusinessCoverUpload
                  categorySlug={profile.categorySlug}
                  coverImageUrl={profile.coverImageUrl}
                  createClient={createBusinessApiClient}
                />
              </View>

              <View style={styles.blockSpacing}>
                <ProfileEditor profile={profile} onSaved={(next) => setProfile(next)} />
              </View>

              {canSubmitVerification ? (
                <View style={styles.blockSpacing}>
                  <VerificationForm onSubmitted={() => void loadProfile()} />
                </View>
              ) : null}

              <View style={styles.blockSpacing}>
                <BusinessBadgeUpgrade />
              </View>

              <View style={styles.blockSpacing}>
                <BusinessReviewsPanel />
              </View>

              <BusinessPortalSettings />
            </>
          ) : null}
        </View>
      </ScrollView>
    </Screen>
  );
}

function ProfileEditor({
  profile,
  onSaved,
}: {
  profile: BusinessProfile;
  onSaved: (profile: BusinessProfile) => void;
}) {
  const { createBusinessApiClient } = useAuth();
  const [businessName, setBusinessName] = useState(profile.businessName);
  const [categorySlug, setCategorySlug] = useState(profile.categorySlug);
  const [phone, setPhone] = useState(profile.phone);
  const [description, setDescription] = useState(profile.description);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleSave() {
    setSaving(true);
    setMessage(null);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const updated = await client.updateTenantProfile({
        businessName: businessName.trim(),
        categorySlug: categorySlug.trim(),
        phone: phone.trim(),
        description: description.trim() || undefined,
      });
      onSaved(updated);
      setMessage("Profile saved.");
    } catch {
      setError("Could not save profile.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Card title="Edit profile">
      {message ? <Text style={styles.success}>{message}</Text> : null}
      {error ? <Text style={styles.errorInline}>{error}</Text> : null}
      <Input label="Business name" value={businessName} onChangeText={setBusinessName} />
      <Input
        label="Category slug"
        value={categorySlug}
        onChangeText={setCategorySlug}
        autoCapitalize="none"
      />
      <Input label="Phone" value={phone} onChangeText={setPhone} keyboardType="phone-pad" />
      <Input label="Description" value={description} onChangeText={setDescription} multiline />
      <Button
        title={saving ? "Saving…" : "Save profile"}
        onPress={() => void handleSave()}
        loading={saving}
        containerStyle={styles.saveButton}
      />
    </Card>
  );
}

function VerificationForm({ onSubmitted }: { onSubmitted: () => void }) {
  const { createBusinessApiClient } = useAuth();
  const [documentType, setDocumentType] = useState(0);
  const [referenceNumber, setReferenceNumber] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit() {
    setSubmitting(true);
    setMessage(null);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      await client.submitTenantVerification({
        documents: [{ documentType, referenceNumber: referenceNumber.trim() }],
      });
      setMessage("Verification submitted. An admin will review your business.");
      setReferenceNumber("");
      onSubmitted();
    } catch {
      setError("Could not submit verification.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card title="Submit verification" description="Provide a registration or ID reference for admin review.">
      {message ? <Text style={styles.success}>{message}</Text> : null}
      {error ? <Text style={styles.errorInline}>{error}</Text> : null}

      <Text style={styles.fieldLabel}>Document type</Text>
      <View style={styles.chipRow}>
        {Object.entries(VERIFICATION_DOCUMENT_LABELS).map(([value, label]) => (
          <Pressable
            key={value}
            onPress={() => setDocumentType(Number(value))}
            style={[styles.chip, documentType === Number(value) && styles.chipActive]}
          >
            <Text style={[styles.chipText, documentType === Number(value) && styles.chipTextActive]}>
              {label}
            </Text>
          </Pressable>
        ))}
      </View>

      <Input
        label="Reference number"
        value={referenceNumber}
        onChangeText={setReferenceNumber}
        autoCapitalize="characters"
      />

      <Button
        title={submitting ? "Submitting…" : "Submit for verification"}
        onPress={() => void handleSubmit()}
        loading={submitting}
        disabled={submitting || referenceNumber.trim().length === 0}
        containerStyle={styles.saveButton}
      />
    </Card>
  );
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.detailRow}>
      <Text style={styles.detailLabel}>{label}</Text>
      <Text style={styles.detailValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  content: {
    paddingBottom: adeniTheme.spacing["3xl"],
  },
  section: {
    paddingHorizontal: adeniTheme.spacing.xl,
  },
  registerButton: {
    marginTop: adeniTheme.spacing.md,
  },
  statusCard: {
    marginTop: adeniTheme.spacing.xl,
  },
  statusRow: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
  },
  blockSpacing: {
    marginTop: adeniTheme.spacing.xl,
  },
  detailRow: {
    marginTop: adeniTheme.spacing.md,
  },
  detailLabel: {
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.textSubtle,
  },
  detailValue: {
    marginTop: 4,
    fontSize: 15,
    color: adeniTheme.text,
  },
  fieldLabel: {
    marginTop: adeniTheme.spacing.lg,
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.textSubtle,
  },
  chipRow: {
    marginTop: adeniTheme.spacing.sm,
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.sm,
  },
  chip: {
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: adeniTheme.radius.full,
    paddingHorizontal: adeniTheme.spacing.lg,
    paddingVertical: adeniTheme.spacing.sm,
    backgroundColor: adeniTheme.surface,
  },
  chipActive: {
    backgroundColor: adeniTheme.primary,
    borderColor: adeniTheme.primary,
  },
  chipText: {
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  chipTextActive: {
    color: adeniTheme.primaryForeground,
  },
  saveButton: {
    marginTop: adeniTheme.spacing.xl,
    alignSelf: "stretch",
  },
  success: {
    marginTop: adeniTheme.spacing.md,
    fontSize: 14,
    color: adeniTheme.accent,
  },
  errorInline: {
    marginTop: adeniTheme.spacing.md,
    fontSize: 14,
    color: adeniTheme.destructive,
  },
});
