import { useRouter } from "expo-router";
import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import type { Category } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { Card } from "@/components/ui/Card";
import { SkeletonList } from "@/components/ui/Skeleton";
import { useMarket } from "@/contexts/market-context";
import { useCategories } from "@/lib/queries/public";
import { adeniTheme } from "@/lib/theme";

export default function HomeScreen() {
  const router = useRouter();
  const { market, loading: marketLoading, locationDenied } = useMarket();
  const { data: categories = [], isLoading, error } = useCategories();

  const grouped = groupCategories(categories);

  return (
    <Screen loading={marketLoading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow={market.name}
          title={market.tagline}
          subtitle={market.description}
        />

        {error ? (
          <Callout tone="error">Could not load categories. Is the API running?</Callout>
        ) : null}

        {market.launchNote ? <Callout tone="info">{market.launchNote}</Callout> : null}

        {locationDenied ? (
          <Callout tone="warning">
            Location access is off — showing the default market. Enable location for nearby
            results.
          </Callout>
        ) : null}

        <Button title="Browse services" onPress={() => router.push("/discover")} containerStyle={styles.cta} />

        {isLoading ? (
          <SkeletonList count={2} />
        ) : categories.length > 0 ? (
          <View style={styles.categories}>
            <Text style={styles.sectionTitle}>Categories</Text>
            {[...grouped.entries()].map(([parentSlug, items]) => (
              <View key={parentSlug} style={styles.group}>
                <Text style={styles.groupLabel}>{formatGroupLabel(parentSlug)}</Text>
                <View style={styles.groupList}>
                  {items.map((category) => (
                    <Pressable
                      key={category.id}
                      onPress={() =>
                        router.push({
                          pathname: "/discover",
                          params: { category: category.slug },
                        })
                      }
                    >
                      <Card padding="sm" style={styles.categoryCard}>
                        <Text style={styles.categoryName}>{category.name}</Text>
                        <Text style={styles.categorySlug}>{category.slug}</Text>
                      </Card>
                    </Pressable>
                  ))}
                </View>
              </View>
            ))}
          </View>
        ) : null}
      </ScrollView>
    </Screen>
  );
}

function groupCategories(categories: Category[]) {
  const groups = new Map<string, typeof categories>();

  for (const category of categories) {
    const key = category.parentSlug ?? "general";
    const existing = groups.get(key) ?? [];
    existing.push(category);
    groups.set(key, existing);
  }

  return groups;
}

function formatGroupLabel(parentSlug: string) {
  if (parentSlug === "general") {
    return "General";
  }

  return parentSlug
    .split("-")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

const styles = StyleSheet.create({
  content: {
    paddingHorizontal: adeniTheme.spacing.xl,
    paddingBottom: adeniTheme.spacing["3xl"],
  },
  cta: {
    marginTop: adeniTheme.spacing.xl,
  },
  categories: {
    marginTop: adeniTheme.spacing["3xl"],
  },
  sectionTitle: {
    ...adeniTheme.typography.titleSm,
    color: adeniTheme.text,
  },
  group: {
    marginTop: adeniTheme.spacing.lg,
  },
  groupLabel: {
    ...adeniTheme.typography.eyebrow,
    color: adeniTheme.accent,
  },
  groupList: {
    marginTop: adeniTheme.spacing.md,
    gap: adeniTheme.spacing.md,
  },
  categoryCard: {
    marginBottom: adeniTheme.spacing.md,
  },
  categoryName: {
    fontSize: adeniTheme.typography.body.fontSize,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  categorySlug: {
    marginTop: 2,
    fontSize: adeniTheme.typography.caption.fontSize,
    color: adeniTheme.textMuted,
  },
});
