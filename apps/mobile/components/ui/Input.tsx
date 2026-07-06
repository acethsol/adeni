import { StyleSheet, Text, TextInput, View, type TextInputProps } from "react-native";
import { adeniTheme } from "@/lib/theme";

type Props = TextInputProps & {
  label?: string;
  hint?: string;
  error?: string;
};

export function Input({ label, hint, error, style, ...rest }: Props) {
  return (
    <View style={styles.wrapper}>
      {label ? <Text style={styles.label}>{label}</Text> : null}
      <TextInput
        placeholderTextColor={adeniTheme.textSubtle}
        style={[styles.input, error && styles.inputError, style]}
        {...rest}
      />
      {error ? <Text style={styles.error}>{error}</Text> : hint ? <Text style={styles.hint}>{hint}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: {
    marginTop: adeniTheme.spacing.lg,
  },
  label: {
    fontSize: adeniTheme.typography.caption.fontSize,
    fontWeight: "600",
    color: adeniTheme.textSubtle,
    marginBottom: adeniTheme.spacing.sm,
  },
  input: {
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: adeniTheme.radius.md,
    paddingHorizontal: adeniTheme.spacing.lg,
    paddingVertical: adeniTheme.spacing.md,
    fontSize: adeniTheme.typography.body.fontSize,
    color: adeniTheme.text,
    backgroundColor: adeniTheme.surface,
  },
  inputError: {
    borderColor: "rgba(153, 27, 27, 0.4)",
  },
  hint: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: adeniTheme.typography.caption.fontSize,
    color: adeniTheme.textSubtle,
  },
  error: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: adeniTheme.typography.caption.fontSize,
    color: adeniTheme.destructive,
  },
});
