import { useRouter } from "expo-router";
import { useEffect, useRef, useState } from "react";
import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import {
  ASK_ADENI_PROMPTS,
  resolveDiscoverySearch,
} from "@adeni/shared";
import { Button } from "@/components/ui/Button";
import { adeniTheme } from "@/lib/theme";

type Props = {
  defaultValue?: string;
  onNavigate?: (params: { category?: string; q?: string }) => void;
  marginHorizontal?: boolean;
};

export function DiscoverySearch({
  defaultValue = "",
  onNavigate,
  marginHorizontal = true,
}: Props) {
  const router = useRouter();
  const [value, setValue] = useState(defaultValue);
  const [expanded, setExpanded] = useState(false);
  const [hint, setHint] = useState<string | null>(null);
  const blurTimeout = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    setValue(defaultValue);
  }, [defaultValue]);

  function clearBlurTimeout() {
    if (blurTimeout.current) {
      clearTimeout(blurTimeout.current);
      blurTimeout.current = null;
    }
  }

  function navigate(text: string) {
    const trimmed = text.trim();
    clearBlurTimeout();
    setExpanded(false);

    if (!trimmed) {
      if (onNavigate) {
        onNavigate({});
        return;
      }
      router.push("/discover");
      return;
    }

    const params = resolveDiscoverySearch(trimmed);
    setHint(params.summary ?? null);
    setValue(trimmed);

    if (onNavigate) {
      onNavigate({ category: params.category, q: params.q });
      return;
    }

    router.push({
      pathname: "/discover",
      params: {
        ...(params.category ? { category: params.category } : {}),
        ...(params.q ? { q: params.q } : {}),
      },
    });
  }

  return (
    <View style={[styles.wrapper, marginHorizontal && styles.wrapperInset]}>
      <TextInput
        value={value}
        onChangeText={setValue}
        placeholder="Search or ask — barber, braids, plumber…"
        placeholderTextColor={adeniTheme.textSubtle}
        returnKeyType="search"
        onFocus={() => {
          clearBlurTimeout();
          setExpanded(true);
        }}
        onBlur={() => {
          blurTimeout.current = setTimeout(() => setExpanded(false), 150);
        }}
        onSubmitEditing={() => navigate(value)}
        style={[styles.input, expanded && styles.inputFocused]}
      />

      {expanded ? (
        <View
          style={styles.panel}
          onTouchStart={clearBlurTimeout}
        >
          <Text style={styles.panelTitle}>✨ Ask Adeni</Text>
          <Text style={styles.panelHint}>
            Type a name or describe what you need — we&apos;ll find verified providers nearby.
          </Text>
          <View style={styles.chips}>
            {ASK_ADENI_PROMPTS.map((example) => (
              <Pressable
                key={example}
                onPress={() => {
                  setValue(example);
                  navigate(example);
                }}
                style={styles.chip}
              >
                <Text style={styles.chipText}>{example}</Text>
              </Pressable>
            ))}
          </View>
          <Button
            title="Find services"
            onPress={() => navigate(value)}
            disabled={!value.trim()}
          />
          {hint ? <Text style={styles.resultHint}>{hint}</Text> : null}
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: {
    marginBottom: adeniTheme.spacing.sm,
    zIndex: 20,
  },
  wrapperInset: {
    paddingHorizontal: adeniTheme.spacing.xl,
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
  inputFocused: {
    borderColor: adeniTheme.accent,
  },
  panel: {
    marginTop: adeniTheme.spacing.sm,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.lg,
    backgroundColor: adeniTheme.surface,
    padding: adeniTheme.spacing.lg,
    ...adeniTheme.shadows.md,
  },
  panelTitle: {
    fontSize: adeniTheme.typography.body.fontSize,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  panelHint: {
    marginTop: adeniTheme.spacing.xs,
    fontSize: adeniTheme.typography.caption.fontSize,
    lineHeight: adeniTheme.typography.bodySm.lineHeight,
    color: adeniTheme.textMuted,
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
  resultHint: {
    marginTop: adeniTheme.spacing.sm,
    fontSize: adeniTheme.typography.caption.fontSize,
    color: adeniTheme.accent,
  },
});
