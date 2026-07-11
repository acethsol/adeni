import { StyleSheet, View } from "react-native";
import { DiscoverySearch } from "@/components/ui/DiscoverySearch";
import { adeniTheme } from "@/lib/theme";

type Props = {
  visible: boolean;
};

export function StickySearchHeader({ visible }: Props) {
  if (!visible) {
    return null;
  }

  return (
    <View style={styles.wrap}>
      <DiscoverySearch />
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    position: "absolute",
    top: 0,
    left: 0,
    right: 0,
    zIndex: 20,
    paddingHorizontal: adeniTheme.spacing.xl,
    paddingTop: adeniTheme.spacing.sm,
    paddingBottom: adeniTheme.spacing.sm,
    backgroundColor: "rgba(255, 255, 255, 0.96)",
    borderBottomWidth: 1,
    borderBottomColor: adeniTheme.border,
    shadowColor: "#0f172a",
    shadowOpacity: 0.08,
    shadowRadius: 10,
    shadowOffset: { width: 0, height: 4 },
    elevation: 4,
  },
});
