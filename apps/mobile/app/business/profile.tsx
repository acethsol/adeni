import { useCallback, useEffect, useState } from "react";
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import { useRouter } from "expo-router";
import type { AdeniApiError } from "@adeni/api-client";
import type { BusinessProfile } from "@adeni/shared";
import { VERIFICATION_DOCUMENT_LABELS } from "@adeni/shared";
import { Screen } from "@/components/adeni/Screen";
import { BusinessCoverUpload } from "@/components/adeni/BusinessCoverUpload";
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

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <Pressable onPress={() => router.back()}>
          <Text style={styles.backLink}>← Back</Text>
        </Pressable>

        <Text style={styles.title}>Business profile</Text>
        <Text style={styles.subtitle}>
          Update your public details and submit verification documents.
        </Text>

        {!isBusinessPortalEnabled ? (
          <View style={styles.callout}>
            <Text style={styles.calloutTitle}>Sign in required</Text>
            <Text style={styles.calloutBody}>
              {isAuth0Configured()
                ? "Sign in from the Account tab to manage your business."
                : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB in .env for local business mode."}
            </Text>
          </View>
        ) : null}

        {!hasBusinessAccount && isBusinessPortalEnabled ? (
          <View style={styles.callout}>
            <Text style={styles.calloutBody}>
              You have not registered a business on this account yet.
            </Text>
            <Pressable
              style={styles.primaryButton}
              onPress={() => router.push("/business/register")}
            >
              <Text style={styles.primaryButtonText}>Register business</Text>
            </Pressable>
          </View>
        ) : null}

        {error ? <Text style={styles.error}>{error}</Text> : null}

        {profile ? (
          <>
            <View style={styles.card}>
              <Text style={styles.cardTitle}>Status</Text>
              <DetailRow label="Verification" value={formatTenantStatus(profile.status)} />
              <DetailRow
                label="Primary location"
                value={
                  profile.locations[0]
                    ? `${profile.locations[0].name} · /businesses/${profile.locations[0].slug}`
                    : "—"
                }
              />
            </View>

            <BusinessCoverUpload
              categorySlug={profile.categorySlug}
              coverImageUrl={profile.coverImageUrl}
              createClient={createBusinessApiClient}
            />

            <ProfileEditor
              profile={profile}
              onSaved={(next) => setProfile(next)}
            />

            {canSubmitVerification ? (
              <VerificationForm onSubmitted={() => void loadProfile()} />
            ) : null}
          </>
        ) : null}
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
    <View style={styles.card}>
      <Text style={styles.cardTitle}>Edit profile</Text>
      {message ? <Text style={styles.success}>{message}</Text> : null}
      {error ? <Text style={styles.errorInline}>{error}</Text> : null}
      <FormField label="Business name" value={businessName} onChangeText={setBusinessName} />
      <FormField label="Category slug" value={categorySlug} onChangeText={setCategorySlug} autoCapitalize="none" />
      <FormField label="Phone" value={phone} onChangeText={setPhone} keyboardType="phone-pad" />
      <FormField
        label="Description"
        value={description}
        onChangeText={setDescription}
        multiline
      />
      <Pressable
        onPress={() => void handleSave()}
        disabled={saving}
        style={({ pressed }) => [
          styles.primaryButton,
          (saving || pressed) && styles.buttonDisabled,
        ]}
      >
        <Text style={styles.primaryButtonText}>{saving ? "Saving…" : "Save profile"}</Text>
      </Pressable>
    </View>
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
    <View style={styles.card}>
      <Text style={styles.cardTitle}>Submit verification</Text>
      <Text style={styles.hint}>
        Provide a registration or ID reference for admin review.
      </Text>
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
            <Text
              style={[
                styles.chipText,
                documentType === Number(value) && styles.chipTextActive,
              ]}
            >
              {label}
            </Text>
          </Pressable>
        ))}
      </View>

      <FormField
        label="Reference number"
        value={referenceNumber}
        onChangeText={setReferenceNumber}
        autoCapitalize="characters"
      />

      <Pressable
        onPress={() => void handleSubmit()}
        disabled={submitting || referenceNumber.trim().length === 0}
        style={({ pressed }) => [
          styles.primaryButton,
          (submitting || pressed) && styles.buttonDisabled,
        ]}
      >
        <Text style={styles.primaryButtonText}>
          {submitting ? "Submitting…" : "Submit for verification"}
        </Text>
      </Pressable>
    </View>
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

function FormField({
  label,
  value,
  onChangeText,
  multiline = false,
  keyboardType,
  autoCapitalize,
}: {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  multiline?: boolean;
  keyboardType?: "default" | "phone-pad";
  autoCapitalize?: "none" | "sentences" | "characters";
}) {
  return (
    <View style={styles.fieldBlock}>
      <Text style={styles.fieldLabel}>{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChangeText}
        multiline={multiline}
        keyboardType={keyboardType}
        autoCapitalize={autoCapitalize}
        style={[styles.input, multiline && styles.inputMultiline]}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  content: {
    paddingHorizontal: 20,
    paddingBottom: 32,
  },
  backLink: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.accent,
    marginBottom: 12,
  },
  title: {
    fontSize: 26,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  subtitle: {
    marginTop: 8,
    fontSize: 15,
    lineHeight: 22,
    color: adeniTheme.textMuted,
  },
  callout: {
    marginTop: 20,
    padding: 16,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    backgroundColor: adeniTheme.surface,
  },
  calloutTitle: {
    fontSize: 16,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  calloutBody: {
    marginTop: 8,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  error: {
    marginTop: 16,
    padding: 10,
    borderRadius: 8,
    backgroundColor: "#fef2f2",
    color: "#991b1b",
    fontSize: 14,
  },
  errorInline: {
    marginTop: 12,
    color: "#991b1b",
    fontSize: 14,
  },
  success: {
    marginTop: 12,
    color: adeniTheme.accent,
    fontSize: 14,
  },
  card: {
    marginTop: 20,
    backgroundColor: adeniTheme.surface,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    padding: 20,
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  hint: {
    marginTop: 8,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  detailRow: {
    marginTop: 14,
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
  fieldBlock: {
    marginTop: 16,
  },
  fieldLabel: {
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.textSubtle,
  },
  input: {
    marginTop: 8,
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 15,
    color: adeniTheme.text,
    backgroundColor: "#ffffff",
  },
  inputMultiline: {
    minHeight: 96,
    textAlignVertical: "top",
  },
  chipRow: {
    marginTop: 10,
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 8,
  },
  chip: {
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: 999,
    paddingHorizontal: 14,
    paddingVertical: 8,
    backgroundColor: "#ffffff",
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
    color: "#ffffff",
  },
  primaryButton: {
    marginTop: 16,
    alignSelf: "flex-start",
    backgroundColor: adeniTheme.primary,
    borderRadius: 999,
    paddingHorizontal: 20,
    paddingVertical: 14,
  },
  primaryButtonText: {
    color: "#ffffff",
    fontSize: 15,
    fontWeight: "600",
  },
  buttonDisabled: {
    opacity: 0.65,
  },
});
