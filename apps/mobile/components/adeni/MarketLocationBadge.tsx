import { Pressable, StyleSheet, Text, View } from "react-native";
import { SymbolView } from "expo-symbols";
import { adeniTheme } from "@/lib/theme";

type Props = {
  label: string;
  marketName: string;
  onPress?: () => void;
};

export function MarketLocationBadge({ label, marketName, onPress }: Props) {
  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => [styles.badge, pressed && styles.pressed]}
      accessibilityRole="button"
      accessibilityLabel={`${label} ${marketName}`}
    >
      <View style={styles.iconWrap}>
        <SymbolView
          name={{ ios: "mappin.and.ellipse", android: "place", web: "place" }}
          tintColor={adeniTheme.accent}
          size={16}
        />
      </View>
      <View style={styles.copy}>
        <Text style={styles.label}>{label}</Text>
        <Text style={styles.market}>{marketName}</Text>
      </View>
      <SymbolView
        name={{ ios: "chevron.down", android: "expand_more", web: "expand_more" }}
        tintColor={adeniTheme.textSubtle}
        size={14}
      />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  badge: {
    alignSelf: "center",
    flexDirection: "row",
    alignItems: "center",
    gap: 10,
    borderWidth: 1,
    borderColor: "rgba(22, 101, 52, 0.18)",
    backgroundColor: "rgba(22, 101, 52, 0.05)",
    borderRadius: 999,
    paddingHorizontal: 14,
    paddingVertical: 10,
  },
  pressed: {
    opacity: 0.92,
  },
  iconWrap: {
    width: 32,
    height: 32,
    borderRadius: 16,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "rgba(22, 101, 52, 0.12)",
  },
  copy: {
    minWidth: 0,
  },
  label: {
    fontSize: 10,
    fontWeight: "700",
    letterSpacing: 1.1,
    textTransform: "uppercase",
    color: adeniTheme.textSubtle,
  },
  market: {
    marginTop: 2,
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
});
