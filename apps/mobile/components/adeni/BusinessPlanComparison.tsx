import { ScrollView, StyleSheet, Text, View } from "react-native";
import {
  PLAN_COMPARISON,
  PLAN_PRICING,
  type SubscriptionTier,
} from "@adeni/shared";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import { adeniTheme } from "@/lib/theme";

type Props = {
  currentTier?: SubscriptionTier;
};

const TIERS: SubscriptionTier[] = ["free", "pro", "business"];

function FeatureCell({ value }: { value: string | boolean }) {
  if (value === true) {
    return <Text style={styles.check}>✓</Text>;
  }
  if (value === false) {
    return <Text style={styles.dash}>—</Text>;
  }
  return <Text style={styles.featureValue}>{value}</Text>;
}

export function BusinessPlanComparison({ currentTier = "free" }: Props) {
  return (
    <View style={styles.stack}>
      <View style={styles.planGrid}>
        {TIERS.map((tier) => {
          const plan = PLAN_PRICING[tier];
          const isCurrent = tier === currentTier;

          return (
            <Card
              key={tier}
              style={[styles.planCard, isCurrent && styles.planCardCurrent]}
              padding="sm"
            >
              {isCurrent ? (
                <View style={styles.currentBadge}>
                  <Text style={styles.currentBadgeText}>Current plan</Text>
                </View>
              ) : null}
              <Text style={styles.planName}>{plan.name}</Text>
              <Text style={styles.planPrice}>{plan.priceLabel}</Text>
              <Text style={styles.planDescription}>{plan.description}</Text>
              {tier === "free" ? (
                <Text style={styles.planFootnote}>Default for all new businesses</Text>
              ) : (
                <Button
                  title={isCurrent ? "Current plan" : "Upgrade (coming soon)"}
                  variant={isCurrent ? "secondary" : "primary"}
                  disabled={isCurrent}
                  containerStyle={styles.upgradeButton}
                />
              )}
            </Card>
          );
        })}
      </View>

      <Card title="Feature comparison" padding="sm">
        <ScrollView horizontal showsHorizontalScrollIndicator={false}>
          <View>
            <View style={styles.tableHeader}>
              <Text style={[styles.tableCell, styles.featureHeader]}>Feature</Text>
              {TIERS.map((tier) => (
                <Text key={tier} style={[styles.tableCell, styles.tierHeader]}>
                  {tier}
                </Text>
              ))}
            </View>
            {PLAN_COMPARISON.map((row) => (
              <View key={row.label} style={styles.tableRow}>
                <Text style={[styles.tableCell, styles.featureLabel]}>{row.label}</Text>
                <View style={styles.tableCell}>
                  <FeatureCell value={row.free} />
                </View>
                <View style={styles.tableCell}>
                  <FeatureCell value={row.pro} />
                </View>
                <View style={styles.tableCell}>
                  <FeatureCell value={row.business} />
                </View>
              </View>
            ))}
          </View>
        </ScrollView>
        <Text style={styles.footnote}>
          Paid checkout via Paystack is planned for a future sprint. Admins can override tiers for
          pilot businesses.
        </Text>
      </Card>
    </View>
  );
}

const styles = StyleSheet.create({
  stack: {
    gap: adeniTheme.spacing.xl,
  },
  planGrid: {
    gap: adeniTheme.spacing.md,
  },
  planCard: {
    position: "relative",
  },
  planCardCurrent: {
    borderColor: "rgba(64, 145, 108, 0.4)",
    borderWidth: 2,
  },
  currentBadge: {
    position: "absolute",
    top: -10,
    left: adeniTheme.spacing.lg,
    borderRadius: adeniTheme.radius.full,
    backgroundColor: adeniTheme.accent,
    paddingHorizontal: 10,
    paddingVertical: 3,
  },
  currentBadgeText: {
    fontSize: 10,
    fontWeight: "700",
    letterSpacing: 0.6,
    textTransform: "uppercase",
    color: adeniTheme.primaryForeground,
  },
  planName: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 11,
    fontWeight: "700",
    letterSpacing: 1,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  planPrice: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 24,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  planDescription: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  planFootnote: {
    marginTop: adeniTheme.spacing.lg,
    fontSize: 12,
    color: adeniTheme.textSubtle,
  },
  upgradeButton: {
    marginTop: adeniTheme.spacing.lg,
    alignSelf: "stretch",
  },
  tableHeader: {
    flexDirection: "row",
    borderBottomWidth: 1,
    borderBottomColor: adeniTheme.border,
    paddingBottom: adeniTheme.spacing.sm,
  },
  tableRow: {
    flexDirection: "row",
    alignItems: "center",
    borderBottomWidth: 1,
    borderBottomColor: adeniTheme.border,
    paddingVertical: adeniTheme.spacing.md,
  },
  tableCell: {
    width: 88,
    alignItems: "center",
  },
  featureHeader: {
    width: 160,
    alignItems: "flex-start",
    fontWeight: "600",
    color: adeniTheme.textMuted,
  },
  tierHeader: {
    fontWeight: "600",
    textTransform: "capitalize",
    color: adeniTheme.textMuted,
  },
  featureLabel: {
    width: 160,
    alignItems: "flex-start",
    fontSize: 13,
    color: adeniTheme.textMuted,
  },
  check: {
    fontSize: 16,
    fontWeight: "700",
    color: adeniTheme.accent,
  },
  dash: {
    fontSize: 16,
    color: adeniTheme.textSubtle,
  },
  featureValue: {
    fontSize: 12,
    textAlign: "center",
    color: adeniTheme.text,
  },
  footnote: {
    marginTop: adeniTheme.spacing.lg,
    fontSize: 12,
    lineHeight: 18,
    color: adeniTheme.textSubtle,
  },
});
