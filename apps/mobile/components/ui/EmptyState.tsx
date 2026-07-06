import { StyleSheet, Text, View } from "react-native";
import { Button } from "@/components/ui/Button";
import { adeniTheme } from "@/lib/theme";

type Props = {
  title: string;
  description?: string;
  actionLabel?: string;
  onAction?: () => void;
};

export function EmptyState({ title, description, actionLabel, onAction }: Props) {
  return (
    <View style={styles.container}>
      <View style={styles.icon}>
        <Text style={styles.iconText}>✦</Text>
      </View>
      <Text style={styles.title}>{title}</Text>
      {description ? <Text style={styles.description}>{description}</Text> : null}
      {actionLabel && onAction ? (
        <Button title={actionLabel} onPress={onAction} containerStyle={styles.action} />
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    alignItems: "center",
    borderRadius: adeniTheme.radius.lg,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    backgroundColor: adeniTheme.surface,
    paddingHorizontal: adeniTheme.spacing["2xl"],
    paddingVertical: adeniTheme.spacing["3xl"],
    ...adeniTheme.shadows.sm,
  },
  icon: {
    width: 48,
    height: 48,
    borderRadius: 24,
    backgroundColor: "rgba(64, 145, 108, 0.1)",
    alignItems: "center",
    justifyContent: "center",
    marginBottom: adeniTheme.spacing.lg,
  },
  iconText: {
    fontSize: 20,
    color: adeniTheme.accent,
  },
  title: {
    fontSize: adeniTheme.typography.titleSm.fontSize,
    fontWeight: adeniTheme.typography.titleSm.fontWeight,
    color: adeniTheme.text,
    textAlign: "center",
  },
  description: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: adeniTheme.typography.bodySm.fontSize,
    lineHeight: adeniTheme.typography.bodySm.lineHeight,
    color: adeniTheme.textMuted,
    textAlign: "center",
  },
  action: {
    marginTop: adeniTheme.spacing.xl,
  },
});
