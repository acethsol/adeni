import type { ReactNode } from "react";
import { StyleSheet, Text, View } from "react-native";
import { adeniTheme } from "@/lib/theme";

type Tone = "info" | "success" | "warning" | "error";

const toneStyles: Record<Tone, object> = {
  info: { backgroundColor: adeniTheme.surface, borderColor: adeniTheme.border },
  success: { backgroundColor: "rgba(64, 145, 108, 0.05)", borderColor: "rgba(64, 145, 108, 0.2)" },
  warning: { backgroundColor: "#fffbeb", borderColor: "rgba(245, 158, 11, 0.2)" },
  error: { backgroundColor: adeniTheme.destructiveBg, borderColor: "rgba(153, 27, 27, 0.2)" },
};

export function Callout({
  title,
  children,
  tone = "info",
}: {
  title?: string;
  children: ReactNode;
  tone?: Tone;
}) {
  return (
    <View style={[styles.callout, toneStyles[tone]]}>
      {title ? <Text style={styles.title}>{title}</Text> : null}
      <Text style={styles.body}>{children}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  callout: {
    marginTop: adeniTheme.spacing.xl,
    borderWidth: 1,
    borderRadius: adeniTheme.radius.lg,
    padding: adeniTheme.spacing.lg,
  },
  title: {
    fontSize: adeniTheme.typography.bodySm.fontSize,
    fontWeight: "600",
    color: adeniTheme.text,
    marginBottom: adeniTheme.spacing.xs,
  },
  body: {
    fontSize: adeniTheme.typography.bodySm.fontSize,
    lineHeight: adeniTheme.typography.bodySm.lineHeight,
    color: adeniTheme.textMuted,
  },
});
