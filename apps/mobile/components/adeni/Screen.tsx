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
          <ActivityIndicator color={adeniTheme.accent} />
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
    padding: 24,
  },
  error: {
    textAlign: "center",
    color: adeniTheme.textMuted,
    lineHeight: 22,
  },
  header: {
    paddingHorizontal: 20,
    paddingTop: 8,
    paddingBottom: 12,
  },
  eyebrow: {
    fontSize: 12,
    fontWeight: "700",
    letterSpacing: 1.2,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  title: {
    marginTop: 4,
    fontSize: 28,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  subtitle: {
    marginTop: 8,
    fontSize: 15,
    lineHeight: 22,
    color: adeniTheme.textMuted,
  },
});
