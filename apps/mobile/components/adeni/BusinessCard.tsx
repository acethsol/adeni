import { Image, Pressable, StyleSheet, Text, View } from "react-native";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import { formatCategoryLabel, resolveBusinessCoverImage } from "@adeni/shared";
import { adeniTheme } from "@/lib/theme";

type Props = {
  business: DiscoveryBusinessItem;
  onPress: () => void;
};

export function BusinessCard({ business, onPress }: Props) {
  const imageUrl = resolveBusinessCoverImage(business.categorySlug, business.coverImageUrl);
  const categoryLabel = formatCategoryLabel(business.categorySlug);

  return (
    <Pressable onPress={onPress} style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}>
      <View style={styles.imageWrap}>
        <Image source={{ uri: imageUrl }} style={styles.image} resizeMode="cover" />
      </View>
      <View style={styles.body}>
        <View style={styles.titleRow}>
          <Text style={styles.name} numberOfLines={2}>
            {business.name}
          </Text>
          <Text style={styles.verified}>Verified</Text>
        </View>
        <Text style={styles.meta}>{categoryLabel}</Text>
        <Text style={styles.meta}>
          {business.area} · {business.distanceKm.toFixed(1)} km
        </Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    marginBottom: adeniTheme.spacing.xs,
  },
  cardPressed: {
    opacity: 0.92,
  },
  imageWrap: {
    height: 220,
    borderRadius: adeniTheme.radius.md,
    overflow: "hidden",
    backgroundColor: adeniTheme.background,
  },
  image: {
    width: "100%",
    height: "100%",
  },
  body: {
    marginTop: adeniTheme.spacing.md,
    gap: 2,
  },
  titleRow: {
    flexDirection: "row",
    alignItems: "flex-start",
    justifyContent: "space-between",
    gap: adeniTheme.spacing.sm,
  },
  name: {
    flex: 1,
    fontSize: 16,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  verified: {
    fontSize: 12,
    fontWeight: "500",
    color: adeniTheme.textMuted,
  },
  meta: {
    fontSize: 14,
    color: adeniTheme.textMuted,
  },
});
