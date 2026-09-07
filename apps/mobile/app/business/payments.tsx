import { useCallback, useEffect, useState } from "react";
import { ScrollView, StyleSheet, View } from "react-native";
import { useRouter } from "expo-router";
import type { AdeniApiError } from "@adeni/api-client";
import type { BusinessProfile } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { BusinessTabs } from "@/components/adeni/BusinessTabs";
import { BusinessPaymentsPanel } from "@/components/adeni/BusinessPaymentsPanel";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { useAuth } from "@/contexts/auth-context";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

export default function BusinessPaymentsScreen() {
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

    if (!isBusinessPortalEnabled || !hasBusinessAccount) {
      setLoading(false);
      return;
    }

    void loadProfile();
  }, [authLoading, hasBusinessAccount, isBusinessPortalEnabled, loadProfile]);

  const canManage = isBusinessPortalEnabled && hasBusinessAccount;

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Payments"
          subtitle="Create payment links, track transactions, and manage refunds."
        />

        {canManage ? <BusinessTabs capabilities={profile?.capabilities} /> : null}

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
            <View style={styles.panel}>
              <BusinessPaymentsPanel profile={profile} />
            </View>
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
  registerButton: {
    marginTop: adeniTheme.spacing.md,
  },
  panel: {
    marginTop: adeniTheme.spacing.xl,
  },
});
