import { useCallback, useEffect, useState } from "react";
import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { SymbolView } from "expo-symbols";
import type { AdeniApiError } from "@adeni/api-client";
import { DAY_OF_WEEK_LABELS, type WeeklyAvailabilityRule } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { BusinessTabs } from "@/components/adeni/BusinessTabs";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Callout } from "@/components/ui/Callout";
import { useAuth } from "@/contexts/auth-context";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

type DayRow = {
  dayOfWeek: number;
  enabled: boolean;
  openTime: string;
  closeTime: string;
};

const DEFAULT_OPEN = "09:00";
const DEFAULT_CLOSE = "17:00";

function toTimeInput(value: string): string {
  return value.slice(0, 5);
}

function toApiTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

function rulesToRows(rules: WeeklyAvailabilityRule[]): DayRow[] {
  return [0, 1, 2, 3, 4, 5, 6].map((dayOfWeek) => {
    const rule = rules.find((item) => item.dayOfWeek === dayOfWeek);
    if (rule) {
      return {
        dayOfWeek,
        enabled: true,
        openTime: toTimeInput(rule.openTime),
        closeTime: toTimeInput(rule.closeTime),
      };
    }

    return { dayOfWeek, enabled: false, openTime: DEFAULT_OPEN, closeTime: DEFAULT_CLOSE };
  });
}

function defaultRows(): DayRow[] {
  return [0, 1, 2, 3, 4, 5, 6].map((dayOfWeek) => ({
    dayOfWeek,
    enabled: dayOfWeek !== 0,
    openTime: DEFAULT_OPEN,
    closeTime: DEFAULT_CLOSE,
  }));
}

const TIME_PATTERN = /^([01]\d|2[0-3]):([0-5]\d)$/;

export default function BusinessAvailabilityScreen() {
  const {
    loading: authLoading,
    isBusinessPortalEnabled,
    hasBusinessAccount,
    createBusinessApiClient,
  } = useAuth();

  const [rows, setRows] = useState<DayRow[]>(defaultRows);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const loadAvailability = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const items = await client.getTenantAvailability();
      setRows(rulesToRows(items));
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(
        apiError.statusCode === 401
          ? "Sign in with a business account to manage hours."
          : "Could not load availability.",
      );
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient]);

  useEffect(() => {
    if (authLoading) {
      return;
    }

    if (isBusinessPortalEnabled && hasBusinessAccount) {
      void loadAvailability();
      return;
    }

    setLoading(false);
  }, [authLoading, hasBusinessAccount, isBusinessPortalEnabled, loadAvailability]);

  function updateRow(dayOfWeek: number, patch: Partial<DayRow>) {
    setRows((current) =>
      current.map((row) => (row.dayOfWeek === dayOfWeek ? { ...row, ...patch } : row)),
    );
  }

  const invalidRow = rows.find(
    (row) => row.enabled && (!TIME_PATTERN.test(row.openTime) || !TIME_PATTERN.test(row.closeTime)),
  );

  async function handleSave() {
    setSaving(true);
    setError(null);
    setMessage(null);

    if (invalidRow) {
      setError(`Enter valid HH:MM times for ${DAY_OF_WEEK_LABELS[invalidRow.dayOfWeek]}.`);
      setSaving(false);
      return;
    }

    const rules: WeeklyAvailabilityRule[] = rows
      .filter((row) => row.enabled)
      .map((row) => ({
        dayOfWeek: row.dayOfWeek,
        openTime: toApiTime(row.openTime),
        closeTime: toApiTime(row.closeTime),
      }));

    try {
      const client = await createBusinessApiClient();
      const saved = await client.replaceTenantAvailability(rules);
      setRows(rulesToRows(saved));
      setMessage("Weekly hours saved.");
    } catch {
      setError("Could not save availability.");
    } finally {
      setSaving(false);
    }
  }

  const canManage = isBusinessPortalEnabled && hasBusinessAccount;

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Availability"
          subtitle="Set your weekly opening hours for online booking."
        />

        {canManage ? <BusinessTabs /> : null}

        <View style={styles.section}>
          {!isBusinessPortalEnabled ? (
            <Callout title="Sign in required">
              {isAuth0Configured()
                ? "Sign in from the Account tab to manage your hours."
                : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB in .env for local business mode."}
            </Callout>
          ) : null}

          {isBusinessPortalEnabled && !hasBusinessAccount ? (
            <Callout title="No business yet">Register your business first to set hours.</Callout>
          ) : null}

          {message ? <Callout tone="success">{message}</Callout> : null}
          {error ? <Callout tone="error">{error}</Callout> : null}

          {canManage ? (
            <>
              <Text style={styles.hint}>
                Customers can only book during enabled hours. Turn a day off to close bookings entirely.
              </Text>

              <View style={styles.list}>
                {rows.map((row) => (
                  <Card key={row.dayOfWeek} style={styles.dayCard} padding="sm">
                    <Pressable
                      style={styles.dayHeader}
                      onPress={() => updateRow(row.dayOfWeek, { enabled: !row.enabled })}
                    >
                      <View
                        style={[styles.checkbox, row.enabled && styles.checkboxChecked]}
                      >
                        {row.enabled ? (
                          <SymbolView
                            name={{ ios: "checkmark", android: "check", web: "check" }}
                            tintColor={adeniTheme.primaryForeground}
                            size={11}
                          />
                        ) : null}
                      </View>
                      <Text style={styles.dayLabel}>{DAY_OF_WEEK_LABELS[row.dayOfWeek]}</Text>
                    </Pressable>

                    {row.enabled ? (
                      <View style={styles.timeRow}>
                        <View style={styles.timeInput}>
                          <Input
                            label="Open"
                            value={row.openTime}
                            onChangeText={(value) => updateRow(row.dayOfWeek, { openTime: value })}
                            placeholder="09:00"
                            keyboardType="numbers-and-punctuation"
                            maxLength={5}
                          />
                        </View>
                        <View style={styles.timeInput}>
                          <Input
                            label="Close"
                            value={row.closeTime}
                            onChangeText={(value) => updateRow(row.dayOfWeek, { closeTime: value })}
                            placeholder="17:00"
                            keyboardType="numbers-and-punctuation"
                            maxLength={5}
                          />
                        </View>
                      </View>
                    ) : null}
                  </Card>
                ))}
              </View>

              <Button
                title={saving ? "Saving…" : "Save weekly hours"}
                onPress={() => void handleSave()}
                loading={saving}
                containerStyle={styles.saveButton}
              />
            </>
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
  hint: {
    marginTop: adeniTheme.spacing.xl,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  list: {
    marginTop: adeniTheme.spacing.lg,
    gap: adeniTheme.spacing.sm,
  },
  dayCard: {},
  dayHeader: {
    flexDirection: "row",
    alignItems: "center",
    gap: adeniTheme.spacing.md,
  },
  checkbox: {
    width: 20,
    height: 20,
    borderRadius: 6,
    borderWidth: 1.5,
    borderColor: adeniTheme.borderStrong,
    alignItems: "center",
    justifyContent: "center",
  },
  checkboxChecked: {
    backgroundColor: adeniTheme.primary,
    borderColor: adeniTheme.primary,
  },
  dayLabel: {
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  timeRow: {
    marginTop: adeniTheme.spacing.sm,
    flexDirection: "row",
    gap: adeniTheme.spacing.md,
    paddingLeft: 32,
  },
  timeInput: {
    flex: 1,
  },
  saveButton: {
    marginTop: adeniTheme.spacing.xl,
    alignSelf: "stretch",
  },
});
