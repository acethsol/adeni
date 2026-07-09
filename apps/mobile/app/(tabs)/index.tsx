import { useRouter } from "expo-router";
import { Image, Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import type { Category } from "@adeni/shared";
import { getCategoryVisual } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { DiscoverySearch } from "@/components/ui/DiscoverySearch";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
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

        <DiscoverySearch />

        <Button
          title="Browse services"
          onPress={() => router.push("/discover")}
          containerStyle={styles.cta}
        />

        {isLoading ? (
          <SkeletonList count={2} />
        ) : categories.length > 0 ? (
          <View style={styles.categories}>
            <Text style={styles.sectionTitle}>Browse by category</Text>
            {[...grouped.entries()].map(([parentSlug, items]) => (
              <View key={parentSlug} style={styles.group}>
                <Text style={styles.groupLabel}>
                  {getCategoryVisual(parentSlug, formatGroupLabel(parentSlug)).icon}{" "}
                  {formatGroupLabel(parentSlug)}
                </Text>
                <View style={styles.groupList}>
                  {items.map((category) => (
                    <CategoryTile
                      key={category.id}
                      category={category}
                      onPress={() =>
                        router.push({
                          pathname: "/discover",
                          params: { category: category.slug },
                        })
                      }
                    />
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

function CategoryTile({ category, onPress }: { category: Category; onPress: () => void }) {
  const visual = getCategoryVisual(category.slug, category.name);

  return (
    <Pressable onPress={onPress} style={({ pressed }) => [styles.tile, pressed && styles.tilePressed]}>
      <Image source={{ uri: visual.imageUrl }} style={styles.tileImage} resizeMode="cover" />
      <Text style={styles.tileName}>
        {visual.icon} {category.name}
      </Text>
    </Pressable>
  );
}

function groupCategories(categories: Category[]) {
  const groups = new Map<string, Category[]>();

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
    paddingBottom: adeniTheme.spacing["3xl"],
  },
  cta: {
    marginTop: adeniTheme.spacing.lg,
    marginHorizontal: adeniTheme.spacing.xl,
  },
  categories: {
    marginTop: adeniTheme.spacing["3xl"],
    paddingHorizontal: adeniTheme.spacing.xl,
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
  tile: {
    marginBottom: adeniTheme.spacing.md,
  },
  tilePressed: {
    opacity: 0.92,
  },
  tileImage: {
    height: 140,
    width: "100%",
    borderRadius: adeniTheme.radius.md,
    backgroundColor: adeniTheme.background,
  },
  tileName: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
});
