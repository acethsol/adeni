import { StyleSheet, Text, View } from "react-native";
import type { PublicReviewItem } from "@adeni/shared";
import { getReviewCountLabel } from "@adeni/shared";
import { Card } from "@/components/ui/Card";
import { useLocale } from "@/contexts/locale-context";
import { adeniTheme } from "@/lib/theme";

type Props = {
  reviews: PublicReviewItem[];
  ratingAvg?: number | null;
  reviewCount?: number | null;
};

function StarRow({ rating }: { rating: number }) {
  return (
    <Text style={styles.stars} accessibilityLabel={`${rating} out of 5 stars`}>
      {"★".repeat(Math.round(rating)).padEnd(5, "☆")}
    </Text>
  );
}

export function BusinessReviewsSection({ reviews, ratingAvg, reviewCount }: Props) {
  const { locale, t } = useLocale();

  if (!reviewCount) {
    return null;
  }

  const reviewLabel = getReviewCountLabel(locale, reviewCount);

  return (
    <Card style={styles.card} title={t("business.reviewsTitle")}>
      <View style={styles.header}>
        <StarRow rating={ratingAvg ?? 0} />
        <Text style={styles.headerMeta}>
          {(ratingAvg ?? 0).toFixed(1)} · {reviewLabel}
        </Text>
      </View>

      <View style={styles.list}>
        {reviews.map((review) => (
          <View key={review.id} style={styles.review}>
            <View style={styles.reviewTop}>
              <Text style={styles.reviewer}>{review.customerDisplayName}</Text>
              <StarRow rating={review.rating} />
            </View>
            {review.comment ? <Text style={styles.comment}>{review.comment}</Text> : null}
            {review.ownerReply ? (
              <View style={styles.replyBox}>
                <Text style={styles.replyLabel}>Business reply</Text>
                <Text style={styles.replyText}>{review.ownerReply}</Text>
              </View>
            ) : null}
            <Text style={styles.date}>
              {new Date(review.createdAt).toLocaleDateString(locale)}
            </Text>
          </View>
        ))}
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  card: {
    marginTop: adeniTheme.spacing.lg,
  },
  header: {
    flexDirection: "row",
    alignItems: "center",
    gap: adeniTheme.spacing.sm,
    marginBottom: adeniTheme.spacing.md,
  },
  headerMeta: {
    fontSize: adeniTheme.typography.bodySm.fontSize,
    color: adeniTheme.textMuted,
  },
  stars: {
    fontSize: 14,
    color: "#f59e0b",
    letterSpacing: 1,
  },
  list: {
    gap: adeniTheme.spacing.md,
  },
  review: {
    borderTopWidth: 1,
    borderTopColor: adeniTheme.border,
    paddingTop: adeniTheme.spacing.md,
    gap: adeniTheme.spacing.sm,
  },
  reviewTop: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    gap: adeniTheme.spacing.sm,
  },
  reviewer: {
    flex: 1,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  comment: {
    fontSize: adeniTheme.typography.bodySm.fontSize,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  replyBox: {
    borderWidth: 1,
    borderColor: `${adeniTheme.accent}33`,
    borderRadius: adeniTheme.radius.md,
    backgroundColor: `${adeniTheme.accent}10`,
    padding: adeniTheme.spacing.sm,
  },
  replyLabel: {
    fontSize: 11,
    fontWeight: "700",
    textTransform: "uppercase",
    letterSpacing: 0.5,
    color: adeniTheme.accent,
  },
  replyText: {
    marginTop: 4,
    fontSize: adeniTheme.typography.bodySm.fontSize,
    color: adeniTheme.text,
  },
  date: {
    fontSize: 12,
    color: adeniTheme.textMuted,
  },
});
