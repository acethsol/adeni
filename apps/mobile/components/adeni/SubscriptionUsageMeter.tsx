import { StyleSheet, Text, View } from "react-native";
import { useRouter } from "expo-router";
import type { SubscriptionUsage } from "@adeni/shared";
import { formatBookingUsage, isNearBookingLimit } from "@adeni/shared";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { adeniTheme } from "@/lib/theme";

type Props = {
  usage: SubscriptionUsage;
};

export function SubscriptionUsageMeter({ usage }: Props) {
  const router = useRouter();
  const limit = usage.bookingsLimitThisMonth;
  const used = usage.bookingsUsedThisMonth;
  const percent = limit ? Math.min(100, Math.round((used / limit) * 100)) : 0;
  const nearLimit = isNearBookingLimit(usage);
  const atLimit = limit != null && used >= limit;

  return (
    <Card>
      <View style={styles.header}>
        <View style={styles.headerCopy}>
          <Text style={styles.eyebrow}>Plan usage</Text>
          <Text style={styles.usage}>{formatBookingUsage(usage)}</Text>
        </View>
        <View style={styles.tierBadge}>
          <Text style={styles.tierText}>{usage.tier}</Text>
        </View>
      </View>

      {limit != null ? (
        <View style={styles.meterBlock}>
          <View style={styles.track}>
            <View
              style={[
                styles.fill,
                { width: `${percent}%` },
                atLimit && styles.fillDanger,
                nearLimit && !atLimit && styles.fillWarning,
              ]}
            />
          </View>
          <Text style={styles.percentLabel}>{percent}% of monthly limit</Text>
        </View>
      ) : (
        <Text style={styles.unlimited}>Unlimited bookings on your plan.</Text>
      )}

      {(nearLimit || atLimit) && usage.tier === "free" ? (
        <Callout tone="warning" title={atLimit ? "Monthly limit reached" : "Almost at your limit"}>
          Upgrade to Pro for unlimited bookings, messaging, and analytics.
        </Callout>
      ) : null}

      <Button
        title="Compare plans"
        variant={nearLimit || atLimit ? "primary" : "ghost"}
        onPress={() => router.push("/business/plan")}
        containerStyle={styles.linkButton}
      />
    </Card>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: "row",
    alignItems: "flex-start",
    justifyContent: "space-between",
    gap: adeniTheme.spacing.md,
  },
  headerCopy: {
    flex: 1,
  },
  eyebrow: {
    fontSize: 11,
    fontWeight: "700",
    letterSpacing: 0.8,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  usage: {
    marginTop: 4,
    fontSize: 14,
    color: adeniTheme.textMuted,
  },
  tierBadge: {
    borderRadius: adeniTheme.radius.full,
    backgroundColor: adeniTheme.subtle,
    paddingHorizontal: 10,
    paddingVertical: 4,
  },
  tierText: {
    fontSize: 11,
    fontWeight: "700",
    textTransform: "uppercase",
    letterSpacing: 0.6,
    color: adeniTheme.text,
  },
  meterBlock: {
    marginTop: adeniTheme.spacing.lg,
  },
  track: {
    height: 8,
    borderRadius: adeniTheme.radius.full,
    backgroundColor: adeniTheme.subtle,
    overflow: "hidden",
  },
  fill: {
    height: "100%",
    borderRadius: adeniTheme.radius.full,
    backgroundColor: adeniTheme.accent,
  },
  fillWarning: {
    backgroundColor: "#fbbf24",
  },
  fillDanger: {
    backgroundColor: adeniTheme.destructive,
  },
  percentLabel: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 12,
    color: adeniTheme.textSubtle,
  },
  unlimited: {
    marginTop: adeniTheme.spacing.md,
    fontSize: 12,
    color: adeniTheme.textSubtle,
  },
  linkButton: {
    marginTop: adeniTheme.spacing.lg,
    alignSelf: "stretch",
  },
});
