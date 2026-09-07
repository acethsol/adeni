import { StyleSheet, Text, View } from "react-native";
import type { PublicReviewItem } from "@adeni/shared";
import { formatReviewCount } from "@adeni/shared";
import { StarRating } from "@/components/adeni/StarRating";
import { adeniTheme } from "@/lib/theme";

type Props = {
  reviews: PublicReviewItem[];
  ratingAvg?: number | null;
  reviewCount?: number | null;
};

function formatDate(value: string): string {
  try {
    return new Date(value).toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  } catch {
    return value;
  }
}

export function BusinessReviewsSection({ reviews, ratingAvg, reviewCount }: Props) {
  if (!reviewCount) {
    return null;
  }

  return (
    <View style={styles.section}>
      <View style={styles.header}>
        <Text style={styles.title}>Reviews</Text>
        <StarRating rating={ratingAvg ?? 0} size="md" />
        <Text style={styles.summary}>
          {(ratingAvg ?? 0).toFixed(1)} · {formatReviewCount(reviewCount)}
        </Text>
      </View>

      <View style={styles.list}>
        {reviews.map((review) => (
          <View key={review.id} style={styles.reviewRow}>
            <View style={styles.reviewHeader}>
              <Text style={styles.reviewer}>{review.customerDisplayName}</Text>
              <StarRating rating={review.rating} />
            </View>
            {review.comment ? <Text style={styles.comment}>{review.comment}</Text> : null}
            <Text style={styles.date}>{formatDate(review.createdAt)}</Text>
          </View>
        ))}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  section: {
    marginTop: adeniTheme.spacing.xl,
    borderRadius: adeniTheme.radius.lg,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    backgroundColor: adeniTheme.surface,
    padding: adeniTheme.spacing.xl,
  },
  header: {
    flexDirection: "row",
    flexWrap: "wrap",
    alignItems: "center",
    gap: adeniTheme.spacing.sm,
  },
  title: {
    fontSize: 20,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  summary: {
    fontSize: 14,
    color: adeniTheme.textMuted,
  },
  list: {
    marginTop: adeniTheme.spacing.lg,
    gap: adeniTheme.spacing.lg,
  },
  reviewRow: {
    borderTopWidth: 1,
    borderTopColor: adeniTheme.border,
    paddingTop: adeniTheme.spacing.lg,
  },
  reviewHeader: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: adeniTheme.spacing.sm,
  },
  reviewer: {
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  comment: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  date: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 12,
    color: adeniTheme.textSubtle,
  },
});
