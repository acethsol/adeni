import { useRouter } from "expo-router";
import { useState } from "react";
import { StyleSheet, TextInput, View } from "react-native";
import { adeniTheme } from "@/lib/theme";

type Props = {
  defaultValue?: string;
  onSubmit?: (value: string) => void;
};

export function GlobalSearchBar({ defaultValue = "", onSubmit }: Props) {
  const router = useRouter();
  const [value, setValue] = useState(defaultValue);

  function handleSubmit() {
    const trimmed = value.trim();
    if (onSubmit) {
      onSubmit(trimmed);
      return;
    }

    if (!trimmed) {
      router.push("/discover");
      return;
    }

    router.push({ pathname: "/discover", params: { q: trimmed } });
  }

  return (
    <View style={styles.wrapper}>
      <TextInput
        value={value}
        onChangeText={setValue}
        placeholder="Search services, areas, businesses…"
        placeholderTextColor={adeniTheme.textSubtle}
        returnKeyType="search"
        onSubmitEditing={() => handleSubmit()}
        style={styles.input}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: {
    paddingHorizontal: adeniTheme.spacing.xl,
    marginBottom: adeniTheme.spacing.sm,
  },
  input: {
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: adeniTheme.radius.full,
    backgroundColor: adeniTheme.surface,
    paddingHorizontal: adeniTheme.spacing.lg,
    paddingVertical: adeniTheme.spacing.md,
    fontSize: adeniTheme.typography.body.fontSize,
    color: adeniTheme.text,
    ...adeniTheme.shadows.sm,
  },
});
