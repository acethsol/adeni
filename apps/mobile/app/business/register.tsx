import { useEffect, useState } from "react";
import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { useRouter } from "expo-router";
import type { Category, MarketConfig } from "@adeni/shared";
import { listMarkets } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Callout } from "@/components/ui/Callout";
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
  const [markets, setMarkets] = useState<MarketConfig[]>(listMarkets());
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
    const client = createPublicApiClient();
    void Promise.all([client.getCategories(), client.getMarkets()])
      .then(([items, marketItems]) => {
        setCategories(items);
        if (items[0]) {
          setCategorySlug(items[0].slug);
        }
        if (marketItems.length > 0) {
          setMarkets(marketItems);
          setMarketId(marketItems[0].id);
        }
      })
      .catch(() => {
        setError("Could not load registration options.");
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
      router.replace("/business");
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
      router.replace("/business");
    } catch {
      setError("Registration failed. Check your details and try again.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Screen loading={authLoading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Register your business"
          subtitle="Create your Adeni profile and submit verification from the next screen."
        />

        <View style={styles.section}>
          {!isBusinessPortalEnabled ? (
            <Callout title="Sign in required">
              {isAuth0Configured()
                ? "Sign in from the Account tab to register a business."
                : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB in .env for local business mode, or sign in with Auth0."}
            </Callout>
          ) : null}

          {error ? <Callout tone="error">{error}</Callout> : null}

          {isBusinessPortalEnabled ? (
            <Card style={styles.formCard}>
              <Input label="Business name" value={businessName} onChangeText={setBusinessName} />

              <Text style={styles.fieldLabel}>Category</Text>
              <View style={styles.chipRow}>
                {categories.map((category) => (
                  <Pressable
                    key={category.slug}
                    onPress={() => setCategorySlug(category.slug)}
                    style={[styles.chip, categorySlug === category.slug && styles.chipActive]}
                  >
                    <Text
                      style={[styles.chipText, categorySlug === category.slug && styles.chipTextActive]}
                    >
                      {category.name}
                    </Text>
                  </Pressable>
                ))}
              </View>

              <Input label="Phone" value={phone} onChangeText={setPhone} keyboardType="phone-pad" />
              <Input
                label="Description (optional)"
                value={description}
                onChangeText={setDescription}
                multiline
              />

              <Text style={styles.sectionTitle}>Primary location</Text>
              <Input label="Public slug" value={slug} onChangeText={setSlug} autoCapitalize="none" />
              <Input
                label="Location name (optional)"
                value={locationName}
                onChangeText={setLocationName}
              />
              <Input label="Address" value={addressLine} onChangeText={setAddressLine} />
              <Input label="Area" value={area} onChangeText={setArea} />

              <Text style={styles.fieldLabel}>Market</Text>
              <View style={styles.chipRow}>
                {markets.map((market) => (
                  <Pressable
                    key={market.id}
                    onPress={() => setMarketId(market.id)}
                    style={[styles.chip, marketId === market.id && styles.chipActive]}
                  >
                    <Text style={[styles.chipText, marketId === market.id && styles.chipTextActive]}>
                      {market.name}
                    </Text>
                  </Pressable>
                ))}
              </View>

              <Button
                title={submitting ? "Creating business…" : "Register business"}
                onPress={() => void handleSubmit()}
                loading={submitting}
                disabled={submitting || !businessName.trim() || !phone.trim()}
                containerStyle={styles.submitButton}
              />
            </Card>
          ) : null}
        </View>
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  content: {
    paddingBottom: adeniTheme.spacing["3xl"],
  },
  section: {
    paddingHorizontal: adeniTheme.spacing.xl,
  },
  formCard: {
    marginTop: adeniTheme.spacing.xl,
  },
  sectionTitle: {
    marginTop: adeniTheme.spacing["2xl"],
    fontSize: 16,
    fontWeight: "600",
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
  submitButton: {
    marginTop: adeniTheme.spacing["2xl"],
    alignSelf: "stretch",
  },
});
