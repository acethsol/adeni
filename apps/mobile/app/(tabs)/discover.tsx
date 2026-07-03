import { useCallback, useEffect, useState } from "react";
import {
  FlatList,
  RefreshControl,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useLocalSearchParams, useRouter } from "expo-router";
import type { Category, DiscoveryBusinessItem } from "@adeni/shared";
import { BusinessCard } from "@/components/adeni/BusinessCard";
import { CategoryFilter } from "@/components/adeni/CategoryFilter";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { useMarket } from "@/contexts/market-context";
import { createPublicApiClient } from "@/lib/api";
import { adeniTheme } from "@/lib/theme";

export default function DiscoverScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ category?: string }>();
  const { market, searchLocation, loading: marketLoading } = useMarket();

  const [categories, setCategories] = useState<Category[]>([]);
  const [businesses, setBusinesses] = useState<DiscoveryBusinessItem[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(
    params.category?.trim().toLowerCase() || null,
  );
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadCategories() {
      try {
        const client = createPublicApiClient();
        const items = await client.getCategories();
        if (!cancelled) {
          setCategories(items);
        }
      } catch {
        // Category filters are optional — discovery still works.
      }
    }

    void loadCategories();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    const next = params.category?.trim().toLowerCase() || null;
    setSelectedCategory(next);
  }, [params.category]);

  const loadDiscovery = useCallback(
    async (isRefresh = false) => {
      if (marketLoading) {
        return;
      }

      if (isRefresh) {
        setRefreshing(true);
      } else {
        setLoading(true);
      }

      setError(null);

      try {
        const client = createPublicApiClient();
        const discovery = await client.searchDiscovery({
          lat: searchLocation.lat,
          lng: searchLocation.lng,
          market: market.id,
          category: selectedCategory,
          pageSize: 50,
        });

        setBusinesses(discovery.items);
      } catch {
        setError("Could not load businesses. Check that the API is running.");
        setBusinesses([]);
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [market.id, marketLoading, searchLocation.lat, searchLocation.lng, selectedCategory],
  );

  useEffect(() => {
    void loadDiscovery();
  }, [loadDiscovery]);

  const categoryLabel =
    categories.find((item) => item.slug === selectedCategory)?.name ?? null;

  return (
    <Screen loading={marketLoading || (loading && businesses.length === 0)} error={error}>
      <FlatList
        data={businesses}
        keyExtractor={(item) => item.locationId}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={() => void loadDiscovery(true)} />
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
          </View>
        }
        contentContainerStyle={styles.listContent}
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        ListEmptyComponent={
          !loading && !error ? (
            <Text style={styles.empty}>
              No verified businesses found
              {selectedCategory ? " for this category" : ""} yet.
            </Text>
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
    paddingBottom: 8,
  },
  filters: {
    paddingHorizontal: 20,
    marginTop: 4,
  },
  listContent: {
    paddingHorizontal: 20,
    paddingBottom: 24,
  },
  separator: {
    height: 12,
  },
  empty: {
    marginTop: 24,
    textAlign: "center",
    color: adeniTheme.textMuted,
    lineHeight: 22,
  },
});
