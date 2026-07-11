import { StyleSheet, Text, View, type ViewProps } from "react-native";
import { adeniTheme } from "@/lib/theme";

type Props = ViewProps & {
  title?: string;
  description?: string;
  padding?: "none" | "sm" | "md" | "lg";
};

const paddingMap = {
  none: 0,
  sm: adeniTheme.spacing.lg,
  md: adeniTheme.spacing.xl,
  lg: adeniTheme.spacing["2xl"],
};

export function Card({ title, description, children, style, padding = "md", ...rest }: Props) {
  return (
    <View style={[styles.card, { padding: paddingMap[padding] }, style]} {...rest}>
      {title ? <Text style={styles.title}>{title}</Text> : null}
      {description ? <Text style={styles.description}>{description}</Text> : null}
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: adeniTheme.surface,
    borderRadius: adeniTheme.radius.lg,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    ...adeniTheme.shadows.sm,
  },
  title: {
    fontSize: adeniTheme.typography.titleSm.fontSize,
    fontWeight: adeniTheme.typography.titleSm.fontWeight,
    color: adeniTheme.text,
  },
  description: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: adeniTheme.typography.bodySm.fontSize,
    lineHeight: adeniTheme.typography.bodySm.lineHeight,
    color: adeniTheme.textMuted,
  },
});
