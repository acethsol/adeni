import { useEffect, useState } from "react";
import { FlatList, RefreshControl, StyleSheet, View } from "react-native";
import { useLocalSearchParams, useRouter } from "expo-router";
import { BusinessCard } from "@/components/adeni/BusinessCard";
import { CategoryFilter } from "@/components/adeni/CategoryFilter";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { EmptyState } from "@/components/ui/EmptyState";
import { SkeletonList } from "@/components/ui/Skeleton";
import { Callout } from "@/components/ui/Callout";
import { useMarket } from "@/contexts/market-context";
import { useCategories, useDiscovery } from "@/lib/queries/public";
import { adeniTheme } from "@/lib/theme";

export default function DiscoverScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ category?: string }>();
  const { market, searchLocation, loading: marketLoading } = useMarket();
  const [selectedCategory, setSelectedCategory] = useState<string | null>(
    params.category?.trim().toLowerCase() || null,
  );

  useEffect(() => {
    setSelectedCategory(params.category?.trim().toLowerCase() || null);
  }, [params.category]);

  const { data: categories = [] } = useCategories();
  const {
    data: businesses = [],
    isLoading,
    isFetching,
    error,
    refetch,
  } = useDiscovery({
    lat: searchLocation.lat,
    lng: searchLocation.lng,
    market: market.id,
    category: selectedCategory,
    enabled: !marketLoading,
  });

  const categoryLabel =
    categories.find((item) => item.slug === selectedCategory)?.name ?? null;

  return (
    <Screen loading={marketLoading}>
      <FlatList
        data={businesses}
        keyExtractor={(item) => item.locationId}
        refreshControl={
          <RefreshControl refreshing={isFetching && !isLoading} onRefresh={() => void refetch()} />
        }
        ListHeaderComponent={
          <View style={styles.headerBlock}>
            <ScreenHeader
              eyebrow={market.name}
              title="Discover"
              subtitle={
                categoryLabel
                  ? `Showing ${categoryLabel} near ${market.name}.`
                  : `Verified businesses near ${market.name}.`
              }
            />
            <View style={styles.filters}>
              <CategoryFilter
                categories={categories}
                selectedSlug={selectedCategory}
                onSelect={setSelectedCategory}
              />
            </View>
            {error ? (
              <Callout tone="error">Could not load businesses. Check that the API is running.</Callout>
            ) : null}
            {isLoading ? <SkeletonList count={3} /> : null}
          </View>
        }
        contentContainerStyle={styles.listContent}
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        ListEmptyComponent={
          !isLoading && !error ? (
            <EmptyState
              title="No verified businesses yet"
              description={
                selectedCategory
                  ? "Try another category or check back soon."
                  : "Businesses will appear here once verified on Adeni."
              }
              actionLabel="Browse all"
              onAction={() => setSelectedCategory(null)}
            />
          ) : null
        }
        renderItem={({ item }) => (
          <BusinessCard
            business={item}
            onPress={() => router.push(`/business/${item.slug}`)}
          />
        )}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  headerBlock: {
    paddingBottom: adeniTheme.spacing.sm,
  },
  filters: {
    paddingHorizontal: adeniTheme.spacing.xl,
    marginTop: adeniTheme.spacing.xs,
  },
  listContent: {
    paddingHorizontal: adeniTheme.spacing.xl,
    paddingBottom: adeniTheme.spacing["2xl"],
  },
  separator: {
    height: adeniTheme.spacing.md,
  },
});
