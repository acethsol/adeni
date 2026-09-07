import { useCallback, useEffect, useState } from "react";
import { StyleSheet, Text, View } from "react-native";
import type { AdeniApiError } from "@adeni/api-client";
import type { PublicReviewItem } from "@adeni/shared";
import { formatReviewCount } from "@adeni/shared";
import { StarRating } from "@/components/adeni/StarRating";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { EmptyState } from "@/components/ui/EmptyState";
import { useAuth } from "@/contexts/auth-context";
import { adeniTheme } from "@/lib/theme";

const PAGE_SIZE = 5;

type ReviewsSummary = {
  ratingAvg: number | null;
  reviewCount: number;
  locationName?: string;
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

export function BusinessReviewsPanel() {
  const { createBusinessApiClient } = useAuth();
  const [summary, setSummary] = useState<ReviewsSummary | null>(null);
  const [items, setItems] = useState<PublicReviewItem[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    async (nextPage: number, append: boolean) => {
      if (append) {
        setLoadingMore(true);
      } else {
        setLoading(true);
      }
      setError(null);

      try {
        const client = await createBusinessApiClient();
        const profile = await client.getTenantProfile();
        const primaryLocation =
          profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];

        if (!primaryLocation) {
          setSummary({ ratingAvg: null, reviewCount: 0 });
          setItems([]);
          setTotalCount(0);
          return;
        }

        const [publicProfile, reviews] = await Promise.all([
          client.getBusinessProfile(primaryLocation.slug),
          client.getBusinessReviews(primaryLocation.slug, nextPage, PAGE_SIZE),
        ]);

        setSummary({
          ratingAvg: publicProfile.ratingAvg ?? null,
          reviewCount: publicProfile.reviewCount ?? 0,
          locationName: primaryLocation.name,
        });
        setTotalCount(reviews.totalCount);
        setItems((current) => (append ? [...current, ...reviews.items] : reviews.items));
        setPage(nextPage);
      } catch (err) {
        const apiError = err as AdeniApiError;
        setError(apiError.message || "Could not load reviews.");
      } finally {
        setLoading(false);
        setLoadingMore(false);
      }
    },
    [createBusinessApiClient],
  );

  useEffect(() => {
    void load(1, false);
  }, [load]);

  if (loading) {
    return <Text style={styles.muted}>Loading reviews…</Text>;
  }

  if (error) {
    return (
      <Callout tone="error" title="Could not load reviews">
        {error}
      </Callout>
    );
  }

  const hasMore = items.length < totalCount;

  return (
    <View>
      <View style={styles.summaryCard}>
        <Text style={styles.summaryScore}>
          {summary?.ratingAvg ? summary.ratingAvg.toFixed(1) : "—"}
        </Text>
        <View>
          <StarRating rating={summary?.ratingAvg ?? 0} size="lg" />
          <Text style={styles.summaryMeta}>
            {formatReviewCount(summary?.reviewCount, "No reviews yet")}
            {summary?.locationName ? ` · ${summary.locationName}` : ""}
          </Text>
        </View>
      </View>

      {items.length === 0 ? (
        <EmptyState
          title="No reviews yet"
          description="Customer reviews will show up here once they start booking and rating your service."
        />
      ) : (
        <View style={styles.list}>
          {items.map((review) => (
            <View key={review.id} style={styles.reviewCard}>
              <View style={styles.reviewHeader}>
                <Text style={styles.reviewer}>{review.customerDisplayName}</Text>
                <View style={styles.reviewMeta}>
                  <StarRating rating={review.rating} />
                  <Text style={styles.reviewDate}>{formatDate(review.createdAt)}</Text>
                </View>
              </View>
              {review.comment ? (
                <Text style={styles.reviewComment}>{review.comment}</Text>
              ) : null}
            </View>
          ))}

          {hasMore ? (
            <Button
              title={loadingMore ? "Loading…" : "Load more reviews"}
              variant="secondary"
              onPress={() => void load(page + 1, true)}
              loading={loadingMore}
              containerStyle={styles.loadMore}
            />
          ) : null}
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  muted: {
    fontSize: 14,
    color: adeniTheme.textMuted,
  },
  summaryCard: {
    flexDirection: "row",
    alignItems: "center",
    gap: adeniTheme.spacing.lg,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.lg,
    backgroundColor: adeniTheme.subtle,
    padding: adeniTheme.spacing.lg,
  },
  summaryScore: {
    fontSize: 32,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  summaryMeta: {
    marginTop: 4,
    fontSize: 13,
    color: adeniTheme.textMuted,
  },
  list: {
    marginTop: adeniTheme.spacing.lg,
    gap: adeniTheme.spacing.md,
  },
  reviewCard: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.lg,
    backgroundColor: adeniTheme.surface,
    padding: adeniTheme.spacing.lg,
  },
  reviewHeader: {
    flexDirection: "row",
    flexWrap: "wrap",
    alignItems: "center",
    justifyContent: "space-between",
    gap: adeniTheme.spacing.sm,
  },
  reviewer: {
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  reviewMeta: {
    flexDirection: "row",
    alignItems: "center",
    gap: adeniTheme.spacing.sm,
  },
  reviewDate: {
    fontSize: 12,
    color: adeniTheme.textSubtle,
  },
  reviewComment: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  loadMore: {
    alignSelf: "center",
    marginTop: adeniTheme.spacing.sm,
  },
});
