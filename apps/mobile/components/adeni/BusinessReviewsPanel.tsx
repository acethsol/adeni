import { useCallback, useEffect, useState } from "react";
import { StyleSheet, Text, View } from "react-native";
import type { AdeniApiError } from "@adeni/api-client";
import type { PublicReviewItem } from "@adeni/shared";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Callout } from "@/components/ui/Callout";
import { EmptyState } from "@/components/ui/EmptyState";
import { useAuth } from "@/contexts/auth-context";
import { adeniTheme } from "@/lib/theme";

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

function StarRow({ rating }: { rating: number }) {
  return (
    <Text style={styles.stars} accessibilityLabel={`${rating} out of 5 stars`}>
      {"★".repeat(Math.round(rating)).padEnd(5, "☆")}
    </Text>
  );
}

export function BusinessReviewsPanel() {
  const { createBusinessApiClient } = useAuth();
  const [items, setItems] = useState<PublicReviewItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [replyingId, setReplyingId] = useState<string | null>(null);
  const [replyDraft, setReplyDraft] = useState("");
  const [submittingReplyId, setSubmittingReplyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      setItems(await client.listTenantReviews());
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(
        apiError.statusCode === 401
          ? "Sign in with your business account to view reviews."
          : "Could not load reviews.",
      );
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleReply(reviewId: string) {
    if (replyDraft.trim().length < 1) {
      setError("Write a reply before posting.");
      return;
    }

    setSubmittingReplyId(reviewId);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const updated = await client.replyToReview(reviewId, replyDraft.trim());
      setItems((current) => current.map((item) => (item.id === reviewId ? updated : item)));
      setReplyingId(null);
      setReplyDraft("");
    } catch {
      setError("Could not post reply.");
    } finally {
      setSubmittingReplyId(null);
    }
  }

  if (loading) {
    return (
      <Card title="Customer reviews">
        <Text style={styles.hint}>Loading reviews…</Text>
      </Card>
    );
  }

  const ratingAvg =
    items.length > 0 ? items.reduce((sum, item) => sum + item.rating, 0) / items.length : null;

  return (
    <Card title="Customer reviews" description="Reply publicly to customer feedback.">
      {error ? <Callout tone="error">{error}</Callout> : null}

      <View style={styles.summary}>
        <Text style={styles.summaryScore}>{ratingAvg ? ratingAvg.toFixed(1) : "—"}</Text>
        <View>
          <StarRow rating={ratingAvg ?? 0} />
          <Text style={styles.summaryMeta}>
            {items.length
              ? `${items.length} review${items.length === 1 ? "" : "s"}`
              : "No reviews yet"}
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
                <Text style={styles.reviewDate}>{formatDate(review.createdAt)}</Text>
              </View>
              <StarRow rating={review.rating} />
              {review.comment ? <Text style={styles.comment}>{review.comment}</Text> : null}

              {review.ownerReply ? (
                <View style={styles.replyBox}>
                  <Text style={styles.replyLabel}>Your reply</Text>
                  <Text style={styles.replyText}>{review.ownerReply}</Text>
                </View>
              ) : replyingId === review.id ? (
                <View style={styles.replyForm}>
                  <Input
                    label="Public reply"
                    value={replyDraft}
                    onChangeText={setReplyDraft}
                    multiline
                    placeholder="Thank the customer and address their feedback"
                  />
                  <View style={styles.replyActions}>
                    <Button
                      title={submittingReplyId === review.id ? "Posting…" : "Post reply"}
                      size="sm"
                      onPress={() => void handleReply(review.id)}
                      loading={submittingReplyId === review.id}
                      disabled={submittingReplyId === review.id}
                    />
                    <Button
                      title="Cancel"
                      size="sm"
                      variant="secondary"
                      onPress={() => {
                        setReplyingId(null);
                        setReplyDraft("");
                      }}
                    />
                  </View>
                </View>
              ) : (
                <Button
                  title="Reply publicly"
                  size="sm"
                  variant="secondary"
                  onPress={() => {
                    setReplyingId(review.id);
                    setReplyDraft("");
                  }}
                  containerStyle={styles.replyButton}
                />
              )}
            </View>
          ))}
        </View>
      )}
    </Card>
  );
}

const styles = StyleSheet.create({
  hint: { color: adeniTheme.textMuted, fontSize: adeniTheme.typography.bodySm.fontSize },
  summary: {
    flexDirection: "row",
    alignItems: "center",
    gap: adeniTheme.spacing.lg,
    marginTop: adeniTheme.spacing.md,
    padding: adeniTheme.spacing.md,
    borderRadius: adeniTheme.radius.lg,
    backgroundColor: adeniTheme.subtle,
  },
  summaryScore: {
    fontSize: 32,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  summaryMeta: {
    marginTop: 4,
    fontSize: adeniTheme.typography.bodySm.fontSize,
    color: adeniTheme.textMuted,
  },
  stars: {
    fontSize: 14,
    color: "#f59e0b",
    letterSpacing: 1,
  },
  list: {
    marginTop: adeniTheme.spacing.lg,
    gap: adeniTheme.spacing.md,
  },
  reviewCard: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.lg,
    padding: adeniTheme.spacing.md,
    gap: adeniTheme.spacing.sm,
  },
  reviewHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    gap: adeniTheme.spacing.sm,
  },
  reviewer: {
    flex: 1,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  reviewDate: {
    fontSize: 12,
    color: adeniTheme.textMuted,
  },
  comment: {
    fontSize: adeniTheme.typography.bodySm.fontSize,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  replyBox: {
    marginTop: adeniTheme.spacing.sm,
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
  replyForm: {
    marginTop: adeniTheme.spacing.sm,
    gap: adeniTheme.spacing.sm,
  },
  replyActions: {
    flexDirection: "row",
    gap: adeniTheme.spacing.sm,
  },
  replyButton: {
    marginTop: adeniTheme.spacing.sm,
    alignSelf: "flex-start",
  },
});
