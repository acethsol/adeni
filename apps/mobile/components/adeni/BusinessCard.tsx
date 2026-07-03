import { Pressable, StyleSheet, Text } from "react-native";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import { adeniTheme } from "@/lib/theme";

type Props = {
  business: DiscoveryBusinessItem;
  onPress: () => void;
};

export function BusinessCard({ business, onPress }: Props) {
  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
    >
      <Text style={styles.name}>{business.name}</Text>
      <Text style={styles.meta}>
        {business.area} · {formatCategory(business.categorySlug)}
      </Text>
      <Text style={styles.distance}>{business.distanceKm.toFixed(1)} km away</Text>
    </Pressable>
  );
}

function formatCategory(slug: string) {
  return slug.replace(/-/g, " ");
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: adeniTheme.surface,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    padding: 16,
  },
  cardPressed: {
    borderColor: adeniTheme.accent,
    opacity: 0.95,
  },
  name: {
    fontSize: 17,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  meta: {
    marginTop: 4,
    fontSize: 14,
    color: adeniTheme.textMuted,
    textTransform: "capitalize",
  },
  distance: {
    marginTop: 8,
    fontSize: 12,
    fontWeight: "600",
    color: adeniTheme.accent,
  },
});
