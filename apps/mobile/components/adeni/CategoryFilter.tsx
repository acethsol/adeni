import { Pressable, ScrollView, StyleSheet, Text } from "react-native";
import type { Category } from "@adeni/shared";
import { adeniTheme } from "@/lib/theme";

type Props = {
  categories: Category[];
  selectedSlug: string | null;
  onSelect: (slug: string | null) => void;
};

export function CategoryFilter({ categories, selectedSlug, onSelect }: Props) {
  if (categories.length === 0) {
    return null;
  }

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={styles.row}
    >
      <Chip label="All" selected={selectedSlug === null} onPress={() => onSelect(null)} />
      {categories.map((category) => (
        <Chip
          key={category.id}
          label={category.name}
          selected={selectedSlug === category.slug}
          onPress={() => onSelect(category.slug)}
        />
      ))}
    </ScrollView>
  );
}

function Chip({
  label,
  selected,
  onPress,
}: {
  label: string;
  selected: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      style={[styles.chip, selected ? styles.chipSelected : styles.chipDefault]}
    >
      <Text style={[styles.chipText, selected && styles.chipTextSelected]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  row: {
    gap: 8,
    paddingVertical: 4,
  },
  chip: {
    borderRadius: 999,
    paddingHorizontal: 14,
    paddingVertical: 8,
  },
  chipDefault: {
    backgroundColor: adeniTheme.surface,
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
  },
  chipSelected: {
    backgroundColor: adeniTheme.primary,
  },
  chipText: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  chipTextSelected: {
    color: "#ffffff",
  },
});
