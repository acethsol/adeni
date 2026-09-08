import { useCallback, useEffect, useState } from "react";
import { ScrollView, StyleSheet, View } from "react-native";
import { useRouter } from "expo-router";
import type { AdeniApiError } from "@adeni/api-client";
import type { SubscriptionTier, SubscriptionUsage } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { BusinessTabs } from "@/components/adeni/BusinessTabs";
import { BusinessPlanComparison } from "@/components/adeni/BusinessPlanComparison";
import { SubscriptionUsageMeter } from "@/components/adeni/SubscriptionUsageMeter";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { useAuth } from "@/contexts/auth-context";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

export default function BusinessPlanScreen() {
  const router = useRouter();
  const {
    loading: authLoading,
    isBusinessPortalEnabled,
    hasBusinessAccount,
    createBusinessApiClient,
  } = useAuth();

  const [currentTier, setCurrentTier] = useState<SubscriptionTier>("free");
  const [usage, setUsage] = useState<SubscriptionUsage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadPlan = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const [profile, subscriptionUsage] = await Promise.all([
        client.getTenantProfile(),
        client.getTenantSubscriptionUsage(),
      ]);
      setCurrentTier(profile.subscriptionTier ?? subscriptionUsage.tier);
      setUsage(subscriptionUsage);
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(apiError.message || "Could not load plan details.");
      setUsage(null);
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

    void loadPlan();
  }, [authLoading, hasBusinessAccount, isBusinessPortalEnabled, loadPlan]);

  const canManage = isBusinessPortalEnabled && hasBusinessAccount;

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Plans & billing"
          subtitle="Compare Adeni plans, track usage, and upgrade when you're ready."
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

          {error ? (
            <Callout tone="error" title="Something went wrong">
              {error}
            </Callout>
          ) : null}

          {usage ? (
            <View style={styles.stack}>
              <SubscriptionUsageMeter usage={usage} />
              <BusinessPlanComparison currentTier={currentTier} />
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
    marginTop: adeniTheme.spacing.xl,
  },
  registerButton: {
    marginTop: adeniTheme.spacing.md,
  },
  stack: {
    gap: adeniTheme.spacing.xl,
  },
});
