import { Image, Pressable, StyleSheet, Text, View } from "react-native";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import { getBusinessCoverImage, getCategoryVisual } from "@adeni/shared";
import { adeniTheme } from "@/lib/theme";

type Props = {
  business: DiscoveryBusinessItem;
  onPress: () => void;
};

export function BusinessCard({ business, onPress }: Props) {
  const visual = getCategoryVisual(business.categorySlug);
  const imageUrl = getBusinessCoverImage(business.categorySlug);

  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
    >
      <View style={styles.imageWrap}>
        <Image source={{ uri: imageUrl }} style={styles.image} resizeMode="cover" />
        <View style={styles.imageOverlay} />
        <Text style={styles.badge}>
          {visual.icon} {visual.label}
        </Text>
      </View>
      <View style={styles.body}>
        <Text style={styles.name}>{business.name}</Text>
        <Text style={styles.meta}>{business.area}</Text>
        <Text style={styles.distance}>{business.distanceKm.toFixed(1)} km away</Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: adeniTheme.surface,
    borderRadius: adeniTheme.radius.lg,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    overflow: "hidden",
    ...adeniTheme.shadows.sm,
  },
  cardPressed: {
    borderColor: adeniTheme.accent,
    opacity: 0.96,
  },
  imageWrap: {
    height: 160,
    backgroundColor: adeniTheme.background,
  },
  image: {
    width: "100%",
    height: "100%",
  },
  imageOverlay: {
    ...StyleSheet.absoluteFill,
    backgroundColor: "rgba(0,0,0,0.15)",
  },
  badge: {
    position: "absolute",
    bottom: 10,
    left: 10,
    backgroundColor: "rgba(255,255,255,0.92)",
    borderRadius: adeniTheme.radius.full,
    paddingHorizontal: 10,
    paddingVertical: 4,
    fontSize: 12,
    fontWeight: "600",
    color: adeniTheme.text,
    overflow: "hidden",
  },
  body: {
    padding: adeniTheme.spacing.lg,
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
  },
  distance: {
    marginTop: 8,
    fontSize: 12,
    fontWeight: "600",
    color: adeniTheme.accent,
  },
});
