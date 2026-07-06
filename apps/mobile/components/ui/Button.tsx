import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  type PressableProps,
  type TextStyle,
  type ViewStyle,
} from "react-native";
import { adeniTheme } from "@/lib/theme";

type Variant = "primary" | "secondary" | "ghost" | "destructive";
type Size = "sm" | "md" | "lg";

type Props = Omit<PressableProps, "style"> & {
  title: string;
  variant?: Variant;
  size?: Size;
  loading?: boolean;
  containerStyle?: ViewStyle;
};

const variantStyles: Record<Variant, ViewStyle> = {
  primary: { backgroundColor: adeniTheme.primary },
  secondary: {
    backgroundColor: adeniTheme.surface,
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
  },
  ghost: { backgroundColor: "transparent" },
  destructive: {
    backgroundColor: adeniTheme.destructiveBg,
    borderWidth: 1,
    borderColor: "rgba(153, 27, 27, 0.2)",
  },
};

const textStyles: Record<Variant, TextStyle> = {
  primary: { color: adeniTheme.primaryForeground },
  secondary: { color: adeniTheme.text },
  ghost: { color: adeniTheme.accent },
  destructive: { color: adeniTheme.destructive },
};

const sizeStyles: Record<Size, ViewStyle> = {
  sm: { paddingHorizontal: 14, paddingVertical: 8 },
  md: { paddingHorizontal: 20, paddingVertical: 14 },
  lg: { paddingHorizontal: 24, paddingVertical: 16 },
};

const textSizeStyles: Record<Size, TextStyle> = {
  sm: { fontSize: 13 },
  md: { fontSize: 15 },
  lg: { fontSize: 16 },
};

export function Button({
  title,
  variant = "primary",
  size = "md",
  loading = false,
  disabled,
  containerStyle,
  ...rest
}: Props) {
  return (
    <Pressable
      disabled={disabled || loading}
      style={({ pressed }) => [
        styles.base,
        variantStyles[variant],
        sizeStyles[size],
        containerStyle,
        (pressed || loading || disabled) && styles.disabled,
      ]}
      {...rest}
    >
      {loading ? (
        <ActivityIndicator
          color={variant === "primary" ? adeniTheme.primaryForeground : adeniTheme.accent}
          size="small"
        />
      ) : (
        <Text style={[styles.text, textStyles[variant], textSizeStyles[size]]}>{title}</Text>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    alignSelf: "flex-start",
    borderRadius: adeniTheme.radius.full,
    alignItems: "center",
    justifyContent: "center",
  },
  text: {
    fontWeight: "600",
  },
  disabled: {
    opacity: 0.65,
  },
});
