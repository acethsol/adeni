import { StyleSheet, Text, View } from "react-native";
import { adeniTheme } from "@/lib/theme";

type Tone = "default" | "accent" | "success" | "warning" | "destructive";

const toneStyles: Record<Tone, { container: object; text: object }> = {
  default: {
    container: { backgroundColor: adeniTheme.surface, borderColor: adeniTheme.border },
    text: { color: adeniTheme.text },
  },
  accent: {
    container: { backgroundColor: "rgba(64, 145, 108, 0.1)", borderColor: "rgba(64, 145, 108, 0.2)" },
    text: { color: adeniTheme.accent },
  },
  success: {
    container: { backgroundColor: "rgba(64, 145, 108, 0.1)", borderColor: "rgba(64, 145, 108, 0.2)" },
    text: { color: adeniTheme.accent },
  },
  warning: {
    container: { backgroundColor: "#fffbeb", borderColor: "rgba(245, 158, 11, 0.2)" },
    text: { color: "#92400e" },
  },
  destructive: {
    container: { backgroundColor: adeniTheme.destructiveBg, borderColor: "rgba(153, 27, 27, 0.2)" },
    text: { color: adeniTheme.destructive },
  },
};

export function Badge({ label, tone = "default" }: { label: string; tone?: Tone }) {
  const toneStyle = toneStyles[tone];

  return (
    <View style={[styles.badge, toneStyle.container]}>
      <Text style={[styles.text, toneStyle.text]}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: {
    alignSelf: "flex-start",
    borderWidth: 1,
    borderRadius: adeniTheme.radius.full,
    paddingHorizontal: 10,
    paddingVertical: 4,
  },
  text: {
    fontSize: 12,
    fontWeight: "600",
  },
});
