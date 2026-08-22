import { useCallback, useEffect, useState } from "react";
import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { useRouter } from "expo-router";
import { SymbolView } from "expo-symbols";
import type { AdeniApiError } from "@adeni/api-client";
import type { BusinessProfile } from "@adeni/shared";
import { formatTenantStatus } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { BusinessTabs } from "@/components/adeni/BusinessTabs";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { useAuth } from "@/contexts/auth-context";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

const QUICK_LINKS = [
  {
    href: "/business/bookings",
    title: "Booking inbox",
    description: "Confirm appointments and manage your schedule.",
    ios: "calendar",
    android: "event",
  },
  {
    href: "/business/services",
    title: "Services & pricing",
    description: "Update what customers can book.",
    ios: "scissors",
    android: "content_cut",
  },
  {
    href: "/business/availability",
    title: "Weekly hours",
    description: "Set when you're open for bookings.",
    ios: "clock",
    android: "schedule",
  },
  {
    href: "/business/locations",
    title: "Locations",
    description: "Branches and public profile URLs.",
    ios: "mappin",
    android: "place",
  },
] as const;

export default function BusinessOverviewScreen() {
  const router = useRouter();
  const {
    loading: authLoading,
    isBusinessPortalEnabled,
    hasBusinessAccount,
    createBusinessApiClient,
  } = useAuth();

  const [profile, setProfile] = useState<BusinessProfile | null>(null);
  const [pendingCount, setPendingCount] = useState(0);
  const [activeServiceCount, setActiveServiceCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadOverview = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const [nextProfile, bookings, services] = await Promise.all([
        client.getTenantProfile(),
        client.getTenantBookings(),
        client.getTenantServices(),
      ]);
      setProfile(nextProfile);
      setPendingCount(bookings.filter((item) => item.status === 0).length);
      setActiveServiceCount(services.filter((item) => item.isActive).length);
    } catch (err) {
      const apiError = err as AdeniApiError;
      if (apiError.statusCode === 401 || apiError.statusCode === 404) {
        setError("No business profile found. Register your business first.");
      } else {
        setError("Could not load your business overview.");
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

    void loadOverview();
  }, [authLoading, hasBusinessAccount, isBusinessPortalEnabled, loadOverview]);

  const primaryLocation = profile?.locations.find((item) => item.isPrimary) ?? profile?.locations[0];

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Overview"
          subtitle="Your command center for bookings, services, and public profile."
        />

        {hasBusinessAccount ? <BusinessTabs /> : null}

        <View style={styles.section}>
          {!isBusinessPortalEnabled ? (
            <Callout title="Sign in required">
              {isAuth0Configured()
                ? "Sign in from the Account tab to manage your business."
                : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB in .env for local business mode."}
            </Callout>
          ) : null}

          {isBusinessPortalEnabled && !hasBusinessAccount ? (
            <>
              <Callout title="No business yet">
                You have not registered a business on this account.
              </Callout>
              <Button
                title="Register business"
                onPress={() => router.push("/business/register")}
                containerStyle={styles.calloutAction}
              />
            </>
          ) : null}

          {error ? (
            <Callout title="Something went wrong" tone="error">
              {error}
            </Callout>
          ) : null}
        </View>

        {profile ? (
          <>
            <View style={styles.statRow}>
              <Card style={styles.statCard} padding="sm">
                <Text style={styles.statLabel}>Status</Text>
                <Text style={styles.statValue}>{formatTenantStatus(profile.status)}</Text>
              </Card>
              <Card style={styles.statCard} padding="sm">
                <Text style={styles.statLabel}>Pending</Text>
                <Text style={styles.statValue}>{pendingCount}</Text>
              </Card>
              <Card style={styles.statCard} padding="sm">
                <Text style={styles.statLabel}>Active services</Text>
                <Text style={styles.statValue}>{activeServiceCount}</Text>
              </Card>
            </View>

            <Card style={styles.heroCard}>
              <View style={styles.heroTop}>
                <SymbolView
                  name={{ ios: "sparkles", android: "auto_awesome", web: "auto_awesome" }}
                  tintColor={adeniTheme.accent}
                  size={16}
                />
                <Text style={styles.heroEyebrow}>Your business</Text>
              </View>
              <Text style={styles.heroTitle}>{profile.businessName}</Text>
              <Text style={styles.heroMeta}>
                {profile.categorySlug.replace(/-/g, " ")} · {profile.phone}
              </Text>
              {primaryLocation ? (
                <Text style={styles.heroMeta}>
                  {primaryLocation.name} ({primaryLocation.slug})
                </Text>
              ) : null}
              <View style={styles.heroActions}>
                {pendingCount > 0 ? (
                  <Button
                    title={`${pendingCount} booking${pendingCount === 1 ? "" : "s"} waiting`}
                    onPress={() => router.push("/business/bookings")}
                  />
                ) : (
                  <Button
                    title="Add a service"
                    variant="secondary"
                    onPress={() => router.push("/business/services")}
                  />
                )}
                {primaryLocation ? (
                  <Button
                    title="View public profile"
                    variant="ghost"
                    onPress={() => router.push(`/business/${primaryLocation.slug}`)}
                  />
                ) : null}
              </View>
            </Card>

            <Text style={styles.sectionLabel}>Quick actions</Text>
            <View style={styles.quickGrid}>
              {QUICK_LINKS.map((link) => (
                <Pressable
                  key={link.href}
                  onPress={() => router.push(link.href)}
                  style={({ pressed }) => [styles.quickCard, pressed && styles.quickCardPressed]}
                >
                  <View style={styles.quickIcon}>
                    <SymbolView
                      name={{ ios: link.ios, android: link.android, web: link.android }}
                      tintColor={adeniTheme.accent}
                      size={18}
                    />
                  </View>
                  <Text style={styles.quickTitle}>{link.title}</Text>
                  <Text style={styles.quickDescription}>{link.description}</Text>
                </Pressable>
              ))}
            </View>

            {profile.status === 0 || profile.status === 3 ? (
              <Card style={styles.verifyCard}>
                <View style={styles.heroTop}>
                  <SymbolView
                    name={{ ios: "checkmark.seal", android: "verified", web: "verified" }}
                    tintColor={adeniTheme.accent}
                    size={16}
                  />
                  <Badge label="Get verified" tone="accent" />
                </View>
                <Text style={styles.quickTitle}>Stand out with a verified badge</Text>
                <Text style={styles.quickDescription}>
                  Submit verification documents so customers know you're trusted.
                </Text>
                <Button
                  title="Start verification"
                  variant="secondary"
                  containerStyle={{ marginTop: adeniTheme.spacing.md }}
                  onPress={() => router.push("/business/profile")}
                />
              </Card>
            ) : null}
          </>
        ) : null}
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
  calloutAction: {
    marginTop: adeniTheme.spacing.md,
  },
  statRow: {
    marginTop: adeniTheme.spacing.xl,
    paddingHorizontal: adeniTheme.spacing.xl,
    flexDirection: "row",
    gap: adeniTheme.spacing.md,
  },
  statCard: {
    flex: 1,
  },
  statLabel: {
    fontSize: 11,
    fontWeight: "700",
    letterSpacing: 0.6,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  statValue: {
    marginTop: adeniTheme.spacing.xs,
    fontSize: 20,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  heroCard: {
    marginTop: adeniTheme.spacing.xl,
    marginHorizontal: adeniTheme.spacing.xl,
  },
  heroTop: {
    flexDirection: "row",
    alignItems: "center",
    gap: 6,
  },
  heroEyebrow: {
    fontSize: 12,
    fontWeight: "700",
    color: adeniTheme.accent,
  },
  heroTitle: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 20,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  heroMeta: {
    marginTop: 4,
    fontSize: 14,
    color: adeniTheme.textMuted,
  },
  heroActions: {
    marginTop: adeniTheme.spacing.lg,
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.sm,
  },
  sectionLabel: {
    marginTop: adeniTheme.spacing["2xl"],
    marginHorizontal: adeniTheme.spacing.xl,
    fontSize: 12,
    fontWeight: "700",
    letterSpacing: 0.8,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  quickGrid: {
    marginTop: adeniTheme.spacing.md,
    paddingHorizontal: adeniTheme.spacing.xl,
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.md,
  },
  quickCard: {
    flexBasis: "47%",
    flexGrow: 1,
    borderRadius: adeniTheme.radius.lg,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    backgroundColor: adeniTheme.surface,
    padding: adeniTheme.spacing.lg,
  },
  quickCardPressed: {
    backgroundColor: adeniTheme.subtle,
  },
  quickIcon: {
    width: 36,
    height: 36,
    borderRadius: adeniTheme.radius.md,
    backgroundColor: "rgba(64, 145, 108, 0.1)",
    alignItems: "center",
    justifyContent: "center",
    marginBottom: adeniTheme.spacing.md,
  },
  quickTitle: {
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  quickDescription: {
    marginTop: 4,
    fontSize: 13,
    lineHeight: 18,
    color: adeniTheme.textMuted,
  },
  verifyCard: {
    marginTop: adeniTheme.spacing.xl,
    marginHorizontal: adeniTheme.spacing.xl,
  },
});
