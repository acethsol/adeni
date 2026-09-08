import { VERIFICATION_BADGE_LABELS } from "@adeni/shared";
import { StyleSheet, Text, View } from "react-native";
import { adeniTheme } from "@/lib/theme";

type Props = {
  badges?: string[] | null;
  verifiedSince?: string | null;
  completionRate?: number | null;
};

export function TrustBadges({ badges, verifiedSince, completionRate }: Props) {
  const items = badges ?? [];

  if (items.length === 0 && !verifiedSince && completionRate == null) {
    return null;
  }

  return (
    <View style={styles.wrap}>
      {items.map((badge) => (
        <Text key={badge} style={styles.badge}>
          {VERIFICATION_BADGE_LABELS[badge] ?? badge}
        </Text>
      ))}
      {verifiedSince ? (
        <Text style={styles.meta}>
          Verified since {new Date(verifiedSince).toLocaleDateString(undefined, { month: "short", year: "numeric" })}
        </Text>
      ) : null}
      {completionRate != null ? (
        <Text style={styles.meta}>{Math.round(completionRate * 100)}% completion rate</Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.sm,
    marginTop: adeniTheme.spacing.sm,
  },
  badge: {
    overflow: "hidden",
    borderRadius: adeniTheme.radius.full,
    borderWidth: 1,
    borderColor: `${adeniTheme.accent}55`,
    backgroundColor: `${adeniTheme.accent}18`,
    paddingHorizontal: adeniTheme.spacing.sm,
    paddingVertical: 4,
    fontSize: 11,
    fontWeight: "700",
    color: adeniTheme.accent,
  },
  meta: {
    fontSize: 11,
    color: adeniTheme.textMuted,
    alignSelf: "center",
  },
});
