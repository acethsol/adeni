import { Linking, Pressable, StyleSheet, Text, View } from "react-native";
import { getWebBaseUrl } from "@/lib/env";
import { adeniTheme } from "@/lib/theme";

type Props = {
  checked: boolean;
  onChange: (checked: boolean) => void;
  error?: string | null;
  includePaymentsNote?: boolean;
};

function openLegal(path: "/terms" | "/privacy") {
  void Linking.openURL(`${getWebBaseUrl()}${path}`);
}

export function LegalAcceptanceField({
  checked,
  onChange,
  error,
  includePaymentsNote = false,
}: Props) {
  return (
    <View style={styles.wrap}>
      <Pressable
        accessibilityRole="checkbox"
        accessibilityState={{ checked }}
        onPress={() => onChange(!checked)}
        style={styles.row}
      >
        <View style={[styles.box, checked && styles.boxChecked]}>
          {checked ? <Text style={styles.checkmark}>✓</Text> : null}
        </View>
        <Text style={styles.text}>
          I agree to the{" "}
          <Text style={styles.link} onPress={() => openLegal("/terms")}>
            Terms of Service
          </Text>{" "}
          and{" "}
          <Text style={styles.link} onPress={() => openLegal("/privacy")}>
            Privacy Policy
          </Text>
          {includePaymentsNote
            ? ". Payments are processed by licensed providers; Adeni does not hold funds."
            : "."}
        </Text>
      </Pressable>
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginTop: adeniTheme.spacing.lg,
  },
  row: {
    flexDirection: "row",
    alignItems: "flex-start",
    gap: adeniTheme.spacing.md,
  },
  box: {
    marginTop: 2,
    width: 20,
    height: 20,
    borderRadius: 4,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: adeniTheme.surface,
  },
  boxChecked: {
    borderColor: adeniTheme.accent,
    backgroundColor: "rgba(64, 145, 108, 0.1)",
  },
  checkmark: {
    fontSize: 12,
    fontWeight: "700",
    color: adeniTheme.accent,
  },
  text: {
    flex: 1,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  link: {
    color: adeniTheme.accent,
    fontWeight: "600",
    textDecorationLine: "underline",
  },
  error: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: 13,
    color: adeniTheme.destructive,
  },
});
