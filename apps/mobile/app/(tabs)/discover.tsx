import { useEffect, useState } from "react";
import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";
import { SymbolView } from "expo-symbols";
import { useLocalSearchParams, useRouter } from "expo-router";
import { BusinessCard } from "@/components/adeni/BusinessCard";
import { CategoryFilter } from "@/components/adeni/CategoryFilter";
import { LocaleCurrencySheet } from "@/components/adeni/LocaleCurrencySheet";
import { StickySearchHeader } from "@/components/adeni/StickySearchHeader";
import { Screen } from "@/components/adeni/Screen";
import { DiscoverySearch } from "@/components/ui/DiscoverySearch";
import { EmptyState } from "@/components/ui/EmptyState";
import { SkeletonList } from "@/components/ui/Skeleton";
import { Callout } from "@/components/ui/Callout";
import { useLocale } from "@/contexts/locale-context";
import { useMarket } from "@/contexts/market-context";
import { useCategories, useDiscovery } from "@/lib/queries/public";
import { adeniTheme } from "@/lib/theme";

const STICKY_SEARCH_OFFSET = 120;

export default function DiscoverScreen() {
  const router = useRouter();
  const { t } = useLocale();
  const params = useLocalSearchParams<{ category?: string; q?: string }>();
  const { market, searchLocation, loading: marketLoading } = useMarket();
  const [pickerOpen, setPickerOpen] = useState(false);
  const [stickySearch, setStickySearch] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(
    params.category?.trim().toLowerCase() || null,
  );
  const [searchQuery, setSearchQuery] = useState(params.q?.trim() || "");

  useEffect(() => {
    setSelectedCategory(params.category?.trim().toLowerCase() || null);
    setSearchQuery(params.q?.trim() || "");
  }, [params.category, params.q]);

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
    q: searchQuery || null,
    enabled: !marketLoading,
  });

  const categoryLabel =
    categories.find((item) => item.slug === selectedCategory)?.name ?? null;

  function applySearch(query: string) {
    setSearchQuery(query);
    router.setParams({
      category: selectedCategory ?? undefined,
      q: query || undefined,
    });
  }

  return (
    <Screen loading={marketLoading}>
      <View style={styles.topBar}>
        <Text style={styles.brand}>
          <Text style={styles.brandDark}>Ad</Text>
          <Text style={styles.brandAccent}>eni</Text>
        </Text>
        <Pressable
          onPress={() => setPickerOpen(true)}
          style={({ pressed }) => [styles.locationButton, pressed && styles.pressed]}
        >
          <SymbolView
            name={{ ios: "mappin.and.ellipse", android: "place", web: "place" }}
            tintColor={adeniTheme.accent}
            size={16}
          />
          <Text style={styles.locationText}>{market.name}</Text>
        </Pressable>
      </View>

      <StickySearchHeader visible={stickySearch} />
      <LocaleCurrencySheet visible={pickerOpen} onClose={() => setPickerOpen(false)} />
      <FlatList
        data={businesses}
        keyExtractor={(item) => item.locationId}
        onScroll={(event) => {
          setStickySearch(event.nativeEvent.contentOffset.y > STICKY_SEARCH_OFFSET);
        }}
        scrollEventThrottle={16}
        refreshControl={
          <RefreshControl refreshing={isFetching && !isLoading} onRefresh={() => void refetch()} />
        }
        ListHeaderComponent={
          <View style={styles.headerBlock}>
            <View style={styles.hero}>
              <Text style={styles.title}>{t("nav.discover")}</Text>
              <Text style={styles.subtitle}>
                {searchQuery
                  ? `Results for “${searchQuery}” near ${market.name}.`
                  : categoryLabel
                    ? `Showing ${categoryLabel} near ${market.name}.`
                    : `Verified businesses near ${market.name}.`}
              </Text>
            </View>
            <DiscoverySearch
              defaultValue={searchQuery}
              marginHorizontal={false}
              onNavigate={(params) => {
                setSearchQuery(params.q ?? "");
                setSelectedCategory(params.category?.trim().toLowerCase() ?? null);
                router.setParams({
                  category: params.category,
                  q: params.q,
                });
              }}
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
              title={searchQuery ? "No matches found" : "No verified businesses yet"}
              description={
                searchQuery
                  ? "Try Ask Adeni or browse another category."
                  : "Businesses appear here once verified."
              }
              actionLabel={searchQuery ? "Clear search" : "Browse all"}
              onAction={() => applySearch("")}
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
  topBar: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingHorizontal: adeniTheme.spacing.xl,
    paddingBottom: adeniTheme.spacing.sm,
  },
  brand: {
    fontSize: 20,
    fontWeight: "700",
  },
  brandDark: {
    color: adeniTheme.text,
  },
  brandAccent: {
    color: adeniTheme.accent,
  },
  locationButton: {
    flexDirection: "row",
    alignItems: "center",
    gap: 6,
    borderRadius: 999,
    paddingHorizontal: 10,
    paddingVertical: 8,
  },
  locationText: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  pressed: {
    opacity: 0.92,
  },
  headerBlock: {
    paddingBottom: adeniTheme.spacing.sm,
  },
  hero: {
    paddingHorizontal: adeniTheme.spacing.xl,
    paddingTop: adeniTheme.spacing.sm,
    alignItems: "center",
  },
  title: {
    marginTop: adeniTheme.spacing.lg,
    ...adeniTheme.typography.titleLg,
    color: adeniTheme.text,
    textAlign: "center",
  },
  subtitle: {
    marginTop: adeniTheme.spacing.sm,
    ...adeniTheme.typography.body,
    color: adeniTheme.textMuted,
    textAlign: "center",
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
