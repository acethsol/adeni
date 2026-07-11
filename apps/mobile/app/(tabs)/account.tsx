import { useState } from "react";
import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { useRouter } from "expo-router";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { LocaleCurrencySheet, FooterLocaleButtons } from "@/components/adeni/LocaleCurrencySheet";
import { useAuth } from "@/contexts/auth-context";
import { useLocale } from "@/contexts/locale-context";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

export default function AccountScreen() {
  const router = useRouter();
  const { t } = useLocale();
  const [pickerOpen, setPickerOpen] = useState(false);
  const {
    loading,
    isAuthenticated,
    profileName,
    profileEmail,
    apiSession,
    isBusinessPortalEnabled,
    hasBusinessAccount,
    isBusinessInboxEnabled,
    isBookingEnabled,
    login,
    logout,
  } = useAuth();
  const [authError, setAuthError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const auth0Configured = isAuth0Configured();
  const devCustomerSub = process.env.EXPO_PUBLIC_DEV_CUSTOMER_AUTH0_SUB?.trim();
  const devBusinessSub = process.env.EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB?.trim();

  async function handleLogin() {
    setAuthError(null);
    setBusy(true);

    try {
      await login();
    } catch {
      setAuthError("Could not start sign in. Check Auth0 configuration.");
    } finally {
      setBusy(false);
    }
  }

  async function handleLogout() {
    setBusy(true);
    try {
      await logout();
    } finally {
      setBusy(false);
    }
  }

  return (
    <Screen loading={loading}>
      <LocaleCurrencySheet visible={pickerOpen} onClose={() => setPickerOpen(false)} />
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Account"
          title={isAuthenticated ? profileName ?? "Signed in" : "Welcome"}
          subtitle={
            isAuthenticated
              ? profileEmail ?? "Your Adeni account"
              : "Sign in to book services and manage your business."
          }
        />

        {authError ? <Text style={styles.error}>{authError}</Text> : null}

        <View style={styles.card}>
          <Text style={styles.cardTitle}>{t("locale.title")}</Text>
          <FooterLocaleButtons onOpen={() => setPickerOpen(true)} />
        </View>

        <View style={styles.card}>
          <Text style={styles.cardTitle}>Session</Text>

          {isAuthenticated ? (
            <>
              {profileEmail ? (
                <DetailRow label="Email" value={profileEmail} />
              ) : null}
              <DetailRow
                label="Roles"
                value={apiSession?.roles.join(", ") || "—"}
              />
              <DetailRow
                label="Booking"
                value={isBookingEnabled ? "Ready" : "Not available"}
              />

              {auth0Configured ? (
                <Pressable
                  onPress={() => void handleLogout()}
                  disabled={busy}
                  style={({ pressed }) => [
                    styles.secondaryButton,
                    pressed && styles.buttonPressed,
                  ]}
                >
                  <Text style={styles.secondaryButtonText}>Sign out</Text>
                </Pressable>
              ) : null}
            </>
          ) : auth0Configured ? (
            <>
              <Text style={styles.hint}>
                Sign in with Auth0 to book services and access business tools.
              </Text>
              <Pressable
                onPress={() => void handleLogin()}
                disabled={busy}
                style={({ pressed }) => [
                  styles.primaryButton,
                  pressed && styles.buttonPressed,
                ]}
              >
                <Text style={styles.primaryButtonText}>
                  {busy ? "Opening…" : "Sign in"}
                </Text>
              </Pressable>
            </>
          ) : (
            <Text style={styles.hint}>
              Auth0 is not configured. Use dev auth subs in `.env` for local
              booking and business inbox testing.
            </Text>
          )}

          {!auth0Configured ? (
            <View style={styles.devBlock}>
              <Text style={styles.devTitle}>Local dev mode</Text>
              {devCustomerSub ? (
                <Text style={styles.devLine}>Customer: {devCustomerSub}</Text>
              ) : (
                <Text style={styles.devLine}>
                  Set EXPO_PUBLIC_DEV_CUSTOMER_AUTH0_SUB for booking.
                </Text>
              )}
              {devBusinessSub ? (
                <Text style={styles.devLine}>Business: {devBusinessSub}</Text>
              ) : (
                <Text style={styles.devLine}>
                  Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB for the booking inbox.
                </Text>
              )}
            </View>
          ) : null}
        </View>

        {isBookingEnabled ? (
          <View style={styles.card}>
            <Text style={styles.cardTitle}>My bookings</Text>
            <Text style={styles.hint}>
              View upcoming and past appointments you have booked.
            </Text>
            <Pressable
              onPress={() => router.push("/my-bookings")}
              style={({ pressed }) => [
                styles.primaryButton,
                pressed && styles.buttonPressed,
              ]}
            >
              <Text style={styles.primaryButtonText}>View bookings</Text>
            </Pressable>
          </View>
        ) : null}

        {isBusinessPortalEnabled || hasBusinessAccount ? (
          <View style={styles.card}>
            <Text style={styles.cardTitle}>Business</Text>
            <Text style={styles.hint}>
              {hasBusinessAccount
                ? "Manage your profile, verification, and booking inbox."
                : "Register your business to appear on Adeni and accept bookings."}
            </Text>
            {hasBusinessAccount ? (
              <>
                <Pressable
                  onPress={() => router.push("/business/profile")}
                  style={({ pressed }) => [
                    styles.primaryButton,
                    pressed && styles.buttonPressed,
                  ]}
                >
                  <Text style={styles.primaryButtonText}>Business profile</Text>
                </Pressable>
                {isBusinessInboxEnabled ? (
                  <Pressable
                    onPress={() => router.push("/business/bookings")}
                    style={({ pressed }) => [
                      styles.secondaryButton,
                      pressed && styles.buttonPressed,
                    ]}
                  >
                    <Text style={styles.secondaryButtonText}>Open booking inbox</Text>
                  </Pressable>
                ) : null}
              </>
            ) : (
              <Pressable
                onPress={() => router.push("/business/register")}
                style={({ pressed }) => [
                  styles.primaryButton,
                  pressed && styles.buttonPressed,
                ]}
              >
                <Text style={styles.primaryButtonText}>Register business</Text>
              </Pressable>
            )}
          </View>
        ) : null}
      </ScrollView>
    </Screen>
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
    paddingHorizontal: 20,
    paddingBottom: 32,
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
    marginTop: 10,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  error: {
    marginTop: 12,
    padding: 10,
    borderRadius: 8,
    backgroundColor: "#fef2f2",
    color: "#991b1b",
    fontSize: 14,
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
  secondaryButton: {
    marginTop: 16,
    alignSelf: "flex-start",
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: 999,
    paddingHorizontal: 18,
    paddingVertical: 12,
  },
  secondaryButtonText: {
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  buttonPressed: {
    opacity: 0.9,
  },
  devBlock: {
    marginTop: 16,
    paddingTop: 16,
    borderTopWidth: 1,
    borderTopColor: adeniTheme.border,
  },
  devTitle: {
    fontSize: 13,
    fontWeight: "700",
    letterSpacing: 0.6,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  devLine: {
    marginTop: 8,
    fontSize: 13,
    lineHeight: 19,
    color: adeniTheme.textSubtle,
  },
});
