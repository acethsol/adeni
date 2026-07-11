import { useState } from "react";
import { useRouter } from "expo-router";
import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { SymbolView } from "expo-symbols";
import type { Category } from "@adeni/shared";
import { getCategoryVisual } from "@adeni/shared";
import { Screen } from "@/components/adeni/Screen";
import { LocaleCurrencySheet } from "@/components/adeni/LocaleCurrencySheet";
import { StickySearchHeader } from "@/components/adeni/StickySearchHeader";
import { DiscoverySearch } from "@/components/ui/DiscoverySearch";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { SkeletonList } from "@/components/ui/Skeleton";
import { useLocale } from "@/contexts/locale-context";
import { useMarket } from "@/contexts/market-context";
import { useCategories } from "@/lib/queries/public";
import { adeniTheme } from "@/lib/theme";

const STICKY_SEARCH_OFFSET = 140;

export default function HomeScreen() {
  const router = useRouter();
  const { t } = useLocale();
  const { market, loading: marketLoading, locationDenied } = useMarket();
  const { data: categories = [], isLoading, error } = useCategories();
  const [pickerOpen, setPickerOpen] = useState(false);
  const [stickySearch, setStickySearch] = useState(false);

  const grouped = groupCategories(categories);

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

      <ScrollView
        contentContainerStyle={styles.content}
        scrollEventThrottle={16}
        onScroll={(event) => {
          setStickySearch(event.nativeEvent.contentOffset.y > STICKY_SEARCH_OFFSET);
        }}
      >
        <View style={styles.hero}>
          <Text style={styles.title}>{market.tagline}</Text>
          <Text style={styles.subtitle}>{market.description}</Text>
        </View>

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

        <View style={styles.searchBlock}>
          <DiscoverySearch />
        </View>

        <Button
          title={t("home.browseServices")}
          onPress={() => router.push("/discover")}
          containerStyle={styles.cta}
        />

        {isLoading ? (
          <SkeletonList count={2} />
        ) : categories.length > 0 ? (
          <View style={styles.categories}>
            <Text style={styles.sectionTitle}>{t("home.browseCategory")}</Text>
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
    <Pressable onPress={onPress} style={({ pressed }) => [styles.tile, pressed && styles.pressed]}>
      <View style={styles.tileImagePlaceholder}>
        <Text style={styles.tileEmoji}>{visual.icon}</Text>
      </View>
      <Text style={styles.tileName}>{category.name}</Text>
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
  content: {
    paddingBottom: adeniTheme.spacing["3xl"],
  },
  hero: {
    paddingHorizontal: adeniTheme.spacing.xl,
    paddingTop: adeniTheme.spacing.sm,
    alignItems: "center",
  },
  title: {
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
  searchBlock: {
    marginTop: adeniTheme.spacing.lg,
    paddingHorizontal: adeniTheme.spacing.xl,
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
  tileImagePlaceholder: {
    height: 140,
    width: "100%",
    borderRadius: adeniTheme.radius.md,
    backgroundColor: adeniTheme.background,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: adeniTheme.border,
  },
  tileEmoji: {
    fontSize: 36,
  },
  tileName: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
});
