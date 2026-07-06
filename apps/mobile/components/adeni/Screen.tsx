import { ActivityIndicator, StyleSheet, Text, View, type ViewProps } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { adeniTheme } from "@/lib/theme";

type Props = ViewProps & {
  loading?: boolean;
  error?: string | null;
};

export function Screen({ children, loading, error, style, ...rest }: Props) {
  return (
    <SafeAreaView style={[styles.safe, style]} edges={["top", "left", "right"]} {...rest}>
      {loading ? (
        <View style={styles.centered}>
          <ActivityIndicator color={adeniTheme.accent} size="large" />
        </View>
      ) : error ? (
        <View style={styles.centered}>
          <Text style={styles.error}>{error}</Text>
        </View>
      ) : (
        children
      )}
    </SafeAreaView>
  );
}

export function ScreenHeader({
  eyebrow,
  title,
  subtitle,
}: {
  eyebrow?: string;
  title: string;
  subtitle?: string;
}) {
  return (
    <View style={styles.header}>
      {eyebrow ? <Text style={styles.eyebrow}>{eyebrow}</Text> : null}
      <Text style={styles.title}>{title}</Text>
      {subtitle ? <Text style={styles.subtitle}>{subtitle}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  safe: {
    flex: 1,
    backgroundColor: adeniTheme.background,
  },
  centered: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    padding: adeniTheme.spacing["2xl"],
  },
  error: {
    textAlign: "center",
    color: adeniTheme.destructive,
    fontSize: adeniTheme.typography.bodySm.fontSize,
    lineHeight: adeniTheme.typography.bodySm.lineHeight,
  },
  header: {
    paddingHorizontal: adeniTheme.spacing.xl,
    paddingTop: adeniTheme.spacing.sm,
    paddingBottom: adeniTheme.spacing.md,
  },
  eyebrow: {
    ...adeniTheme.typography.eyebrow,
    color: adeniTheme.accent,
  },
  title: {
    marginTop: adeniTheme.spacing.xs,
    ...adeniTheme.typography.titleLg,
    color: adeniTheme.text,
  },
  subtitle: {
    marginTop: adeniTheme.spacing.sm,
    ...adeniTheme.typography.body,
    color: adeniTheme.textMuted,
  },
});
