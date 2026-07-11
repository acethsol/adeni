import { Image, Pressable, StyleSheet, Text, View } from "react-native";
import { SymbolView } from "expo-symbols";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import {
  formatCategoryLabel,
  getReviewCountLabel,
  resolveBusinessCoverImage,
} from "@adeni/shared";
import { useLocale } from "@/contexts/locale-context";
import { Badge } from "@/components/ui/Badge";
import { Card } from "@/components/ui/Card";
import { adeniTheme } from "@/lib/theme";

type Props = {
  business: DiscoveryBusinessItem;
  onPress: () => void;
};

export function BusinessCard({ business, onPress }: Props) {
  const { locale, t } = useLocale();
  const imageUrl = resolveBusinessCoverImage(business.categorySlug, business.coverImageUrl);
  const categoryLabel = formatCategoryLabel(business.categorySlug);
  const hasReviews = Boolean(business.reviewCount && business.reviewCount > 0);
  const reviewLabel = hasReviews ? getReviewCountLabel(locale, business.reviewCount) : null;
  const awaitingReviewsLabel = t("business.awaitingReviews");

  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => [styles.pressable, pressed && styles.pressed]}
    >
      <Card padding="none" style={styles.card}>
        <View style={styles.imageWrap}>
          <Image source={{ uri: imageUrl }} style={styles.image} resizeMode="cover" />
          <View style={styles.verifiedBadge}>
            <SymbolView
              name={{ ios: "checkmark.shield.fill", android: "verified_user", web: "verified_user" }}
              tintColor={adeniTheme.accent}
              size={14}
            />
            <Text style={styles.verifiedText}>{t("business.verified")}</Text>
          </View>
        </View>

        <Text style={styles.name} numberOfLines={1}>
          {business.name}
        </Text>

        <View style={styles.ratingRow}>
          {hasReviews ? (
            <Text style={styles.rating}>
              {`★ ${(business.ratingAvg ?? 0).toFixed(1)} · ${reviewLabel}`}
            </Text>
          ) : (
            <View
              style={styles.awaitingReviews}
              accessible
              accessibilityLabel={awaitingReviewsLabel}
            >
              <Text style={styles.emptyStars}>☆☆☆☆☆</Text>
              <SymbolView
                name={{
                  ios: "bubble.left.and.bubble.right",
                  android: "chat_bubble_outline",
                  web: "chat_bubble_outline",
                }}
                tintColor={adeniTheme.textMuted}
                size={14}
              />
            </View>
          )}
        </View>

        <Text style={styles.meta} numberOfLines={1}>
          {categoryLabel} · {business.area} · {business.distanceKm.toFixed(1)} km
        </Text>

        <View style={styles.actions}>
          <Badge label="View profile" tone="accent" />
          <Badge label="Book now" tone="accent" />
        </View>
      </Card>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  pressable: {
    marginBottom: adeniTheme.spacing.xs,
  },
  pressed: {
    opacity: 0.94,
  },
  card: {
    overflow: "hidden",
  },
  imageWrap: {
    height: 200,
    backgroundColor: adeniTheme.background,
  },
  image: {
    width: "100%",
    height: "100%",
  },
  verifiedBadge: {
    position: "absolute",
    top: 12,
    left: 12,
    zIndex: 2,
    flexDirection: "row",
    alignItems: "center",
    gap: 4,
    borderRadius: 999,
    borderWidth: 1,
    borderColor: "rgba(22, 101, 52, 0.2)",
    backgroundColor: "rgba(255, 255, 255, 0.95)",
    paddingHorizontal: 10,
    paddingVertical: 5,
  },
  verifiedText: {
    fontSize: 11,
    fontWeight: "700",
    color: adeniTheme.accent,
  },
  name: {
    marginTop: adeniTheme.spacing.lg,
    marginHorizontal: adeniTheme.spacing.lg,
    fontSize: adeniTheme.typography.bodySm.fontSize,
    fontWeight: adeniTheme.typography.titleSm.fontWeight,
    color: adeniTheme.text,
  },
  rating: {
    fontSize: adeniTheme.typography.bodySm.fontSize,
    fontWeight: "500",
    color: adeniTheme.accent,
  },
  ratingRow: {
    marginTop: adeniTheme.spacing.xs,
    marginHorizontal: adeniTheme.spacing.lg,
    minHeight: 20,
    justifyContent: "center",
  },
  awaitingReviews: {
    flexDirection: "row",
    alignItems: "center",
    gap: 6,
  },
  emptyStars: {
    fontSize: adeniTheme.typography.bodySm.fontSize,
    color: adeniTheme.textMuted,
    letterSpacing: 1,
  },
  meta: {
    marginTop: adeniTheme.spacing.xs,
    marginHorizontal: adeniTheme.spacing.lg,
    fontSize: adeniTheme.typography.bodySm.fontSize,
    color: adeniTheme.textMuted,
  },
  actions: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.sm,
    marginTop: adeniTheme.spacing.md,
    marginHorizontal: adeniTheme.spacing.lg,
    marginBottom: adeniTheme.spacing.lg,
  },
});
