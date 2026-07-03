import { useEffect, useState } from "react";
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useRouter } from "expo-router";
import type { Category } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { useMarket } from "@/contexts/market-context";
import { createPublicApiClient } from "@/lib/api";
import { adeniTheme } from "@/lib/theme";

export default function HomeScreen() {
  const router = useRouter();
  const { market, loading: marketLoading, locationDenied } = useMarket();
  const [categories, setCategories] = useState<Category[]>([]);
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
        if (!cancelled) {
          setError("Could not load categories. Is the API running?");
        }
      }
    }

    void loadCategories();

    return () => {
      cancelled = true;
    };
  }, []);

  const grouped = groupCategories(categories);

  return (
    <Screen loading={marketLoading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow={market.name}
          title={market.tagline}
          subtitle={market.description}
        />

        {error ? <Text style={styles.inlineError}>{error}</Text> : null}

        {market.launchNote ? (
          <Text style={styles.launchNote}>{market.launchNote}</Text>
        ) : null}

        {locationDenied ? (
          <Text style={styles.locationHint}>
            Location access is off — showing the default market. Enable location for
            nearby results.
          </Text>
        ) : null}

        <Pressable
          style={({ pressed }) => [styles.primaryButton, pressed && styles.buttonPressed]}
          onPress={() => router.push("/discover")}
        >
          <Text style={styles.primaryButtonText}>Browse services</Text>
        </Pressable>

        {categories.length > 0 ? (
          <View style={styles.categories}>
            <Text style={styles.sectionTitle}>Categories</Text>
            {[...grouped.entries()].map(([parentSlug, items]) => (
              <View key={parentSlug} style={styles.group}>
                <Text style={styles.groupLabel}>{formatGroupLabel(parentSlug)}</Text>
                <View style={styles.groupList}>
                  {items.map((category) => (
                    <Pressable
                      key={category.id}
                      style={({ pressed }) => [styles.categoryCard, pressed && styles.cardPressed]}
                      onPress={() =>
                        router.push({
                          pathname: "/discover",
                          params: { category: category.slug },
                        })
                      }
                    >
                      <Text style={styles.categoryName}>{category.name}</Text>
                      <Text style={styles.categorySlug}>{category.slug}</Text>
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
    paddingHorizontal: 20,
    paddingBottom: 32,
  },
  launchNote: {
    marginTop: 8,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textSubtle,
  },
  inlineError: {
    marginTop: 12,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  locationHint: {
    marginTop: 12,
    fontSize: 13,
    lineHeight: 19,
    color: adeniTheme.textSubtle,
  },
  primaryButton: {
    marginTop: 20,
    alignSelf: "flex-start",
    backgroundColor: adeniTheme.primary,
    borderRadius: 999,
    paddingHorizontal: 20,
    paddingVertical: 14,
  },
  primaryButtonText: {
    color: "#ffffff",
    fontSize: 15,
    fontWeight: "600",
  },
  buttonPressed: {
    opacity: 0.9,
  },
  categories: {
    marginTop: 28,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  group: {
    marginTop: 16,
  },
  groupLabel: {
    fontSize: 12,
    fontWeight: "700",
    letterSpacing: 0.8,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  groupList: {
    marginTop: 10,
    gap: 10,
  },
  categoryCard: {
    backgroundColor: adeniTheme.surface,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    padding: 14,
  },
  cardPressed: {
    borderColor: adeniTheme.accent,
  },
  categoryName: {
    fontSize: 16,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  categorySlug: {
    marginTop: 2,
    fontSize: 13,
    color: adeniTheme.textMuted,
  },
});
