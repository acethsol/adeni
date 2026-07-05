import { useEffect, useState } from "react";
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
import type { Category } from "@adeni/shared";
import { markets } from "@adeni/shared";
import { Screen } from "@/components/adeni/Screen";
import { useAuth } from "@/contexts/auth-context";
import { createPublicApiClient } from "@/lib/api";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

export default function BusinessRegisterScreen() {
  const router = useRouter();
  const {
    loading: authLoading,
    isBusinessPortalEnabled,
    hasBusinessAccount,
    createBusinessApiClient,
    refreshBusinessContext,
  } = useAuth();

  const [categories, setCategories] = useState<Category[]>([]);
  const [businessName, setBusinessName] = useState("");
  const [categorySlug, setCategorySlug] = useState("barbers");
  const [phone, setPhone] = useState("");
  const [description, setDescription] = useState("");
  const [slug, setSlug] = useState("");
  const [locationName, setLocationName] = useState("");
  const [addressLine, setAddressLine] = useState("");
  const [area, setArea] = useState("");
  const [marketId, setMarketId] = useState("lagos");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void createPublicApiClient()
      .getCategories()
      .then((items) => {
        setCategories(items);
        if (items[0]) {
          setCategorySlug(items[0].slug);
        }
      })
      .catch(() => {
        setError("Could not load categories.");
      });
  }, []);

  useEffect(() => {
    if (!slug && businessName) {
      setSlug(
        businessName
          .toLowerCase()
          .replace(/[^a-z0-9]+/g, "-")
          .replace(/^-+|-+$/g, "")
          .slice(0, 48),
      );
    }
  }, [businessName, slug]);

  useEffect(() => {
    if (!authLoading && hasBusinessAccount) {
      router.replace("/business/profile");
    }
  }, [authLoading, hasBusinessAccount, router]);

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      await client.registerBusiness({
        businessName: businessName.trim(),
        categorySlug,
        phone: phone.trim(),
        description: description.trim() || undefined,
        location: {
          slug: slug.trim(),
          name: locationName.trim() || undefined,
          addressLine: addressLine.trim(),
          area: area.trim(),
          marketId,
        },
      });

      await refreshBusinessContext();
      router.replace("/business/profile");
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(
        apiError.message === "Request failed: /api/v1/tenant/register"
          ? "Registration failed. Check your details and try again."
          : "Registration failed. Check your details and try again.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  const marketOptions = Object.values(markets);

  return (
    <Screen loading={authLoading}>
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.title}>Register your business</Text>
        <Text style={styles.subtitle}>
          Create your Adeni profile and submit verification from the next screen.
        </Text>

        {!isBusinessPortalEnabled ? (
          <View style={styles.callout}>
            <Text style={styles.calloutTitle}>Sign in required</Text>
            <Text style={styles.calloutBody}>
              {isAuth0Configured()
                ? "Sign in from the Account tab to register a business."
                : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB in .env for local business mode, or sign in with Auth0."}
            </Text>
          </View>
        ) : null}

        {error ? <Text style={styles.error}>{error}</Text> : null}

        {isBusinessPortalEnabled ? (
          <>
            <FormField label="Business name" value={businessName} onChangeText={setBusinessName} />
            <Text style={styles.fieldLabel}>Category</Text>
            <View style={styles.chipRow}>
              {categories.map((category) => (
                <Pressable
                  key={category.slug}
                  onPress={() => setCategorySlug(category.slug)}
                  style={[
                    styles.chip,
                    categorySlug === category.slug && styles.chipActive,
                  ]}
                >
                  <Text
                    style={[
                      styles.chipText,
                      categorySlug === category.slug && styles.chipTextActive,
                    ]}
                  >
                    {category.name}
                  </Text>
                </Pressable>
              ))}
            </View>
            <FormField label="Phone" value={phone} onChangeText={setPhone} keyboardType="phone-pad" />
            <FormField
              label="Description (optional)"
              value={description}
              onChangeText={setDescription}
              multiline
            />

            <Text style={styles.sectionTitle}>Primary location</Text>
            <FormField label="Public slug" value={slug} onChangeText={setSlug} autoCapitalize="none" />
            <FormField
              label="Location name (optional)"
              value={locationName}
              onChangeText={setLocationName}
            />
            <FormField label="Address" value={addressLine} onChangeText={setAddressLine} />
            <FormField label="Area" value={area} onChangeText={setArea} />
            <Text style={styles.fieldLabel}>Market</Text>
            <View style={styles.chipRow}>
              {marketOptions.map((market) => (
                <Pressable
                  key={market.id}
                  onPress={() => setMarketId(market.id)}
                  style={[styles.chip, marketId === market.id && styles.chipActive]}
                >
                  <Text
                    style={[
                      styles.chipText,
                      marketId === market.id && styles.chipTextActive,
                    ]}
                  >
                    {market.name}
                  </Text>
                </Pressable>
              ))}
            </View>

            <Pressable
              onPress={() => void handleSubmit()}
              disabled={submitting}
              style={({ pressed }) => [
                styles.primaryButton,
                (submitting || pressed) && styles.buttonDisabled,
              ]}
            >
              <Text style={styles.primaryButtonText}>
                {submitting ? "Creating business…" : "Register business"}
              </Text>
            </Pressable>
          </>
        ) : null}
      </ScrollView>
    </Screen>
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
  autoCapitalize?: "none" | "sentences";
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
  sectionTitle: {
    marginTop: 24,
    fontSize: 18,
    fontWeight: "600",
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
    backgroundColor: adeniTheme.surface,
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
    color: "#ffffff",
  },
  primaryButton: {
    marginTop: 28,
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
