import { useRouter } from "expo-router";
import { useState } from "react";
import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import {
  ASK_ADENI_PROMPTS,
  buildDiscoverSearchParams,
  parseSearchIntent,
} from "@adeni/shared";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { adeniTheme } from "@/lib/theme";

export function AskAdeniPanel() {
  const router = useRouter();
  const [prompt, setPrompt] = useState("");

  function navigateWithIntent(text: string) {
    const intent = parseSearchIntent(text);
    const params = buildDiscoverSearchParams(intent);
    router.push({
      pathname: "/discover",
      params: {
        ...(params.category ? { category: params.category } : {}),
        ...(params.q ? { q: params.q } : {}),
      },
    });
  }

  return (
    <Card style={styles.card}>
      <Text style={styles.title}>✨ Ask Adeni</Text>
      <Text style={styles.subtitle}>
        Describe what you need — we&apos;ll find verified providers nearby.
      </Text>
      <TextInput
        value={prompt}
        onChangeText={setPrompt}
        placeholder='e.g. "Barber in Lekki"'
        placeholderTextColor={adeniTheme.textSubtle}
        multiline
        style={styles.input}
      />
      <View style={styles.chips}>
        {ASK_ADENI_PROMPTS.map((example) => (
          <Pressable
            key={example}
            onPress={() => {
              setPrompt(example);
              navigateWithIntent(example);
            }}
            style={styles.chip}
          >
            <Text style={styles.chipText}>{example}</Text>
          </Pressable>
        ))}
      </View>
      <Button
        title="Find services"
        onPress={() => prompt.trim() && navigateWithIntent(prompt)}
        disabled={!prompt.trim()}
      />
    </Card>
  );
}

const styles = StyleSheet.create({
  card: {
    marginHorizontal: adeniTheme.spacing.xl,
    marginTop: adeniTheme.spacing.lg,
    borderColor: "rgba(64, 145, 108, 0.2)",
    backgroundColor: adeniTheme.surface,
  },
  title: {
    fontSize: adeniTheme.typography.titleSm.fontSize,
    fontWeight: adeniTheme.typography.titleSm.fontWeight,
    color: adeniTheme.text,
  },
  subtitle: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: adeniTheme.typography.bodySm.fontSize,
    lineHeight: adeniTheme.typography.bodySm.lineHeight,
    color: adeniTheme.textMuted,
  },
  input: {
    marginTop: adeniTheme.spacing.lg,
    minHeight: 80,
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: adeniTheme.radius.md,
    paddingHorizontal: adeniTheme.spacing.lg,
    paddingVertical: adeniTheme.spacing.md,
    fontSize: adeniTheme.typography.body.fontSize,
    color: adeniTheme.text,
    textAlignVertical: "top",
  },
  chips: {
    marginTop: adeniTheme.spacing.md,
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.sm,
  },
  chip: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.full,
    paddingHorizontal: 12,
    paddingVertical: 6,
    backgroundColor: adeniTheme.background,
  },
  chipText: {
    fontSize: 12,
    fontWeight: "600",
    color: adeniTheme.textMuted,
  },
});
