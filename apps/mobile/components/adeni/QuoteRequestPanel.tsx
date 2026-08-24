import { discoveryCtaLabel } from "@adeni/shared";
import { useState } from "react";
import { Alert, StyleSheet, Text, TextInput, View } from "react-native";
import { AdeniApiClient } from "@adeni/api-client";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { adeniTheme } from "@/lib/theme";

type Props = {
  slug: string;
  client: AdeniApiClient;
};

export function QuoteRequestPanel({ slug, client }: Props) {
  const [description, setDescription] = useState("");
  const [serviceAddress, setServiceAddress] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  async function handleSubmit() {
    setSubmitting(true);
    try {
      await client.createQuoteRequest(slug, {
        description: description.trim(),
        serviceAddress: serviceAddress.trim() || undefined,
      });
      setSubmitted(true);
    } catch (error) {
      Alert.alert(
        "Quote request failed",
        error instanceof Error ? error.message : "Please try again.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  if (submitted) {
    return (
      <Card style={styles.card}>
        <Text style={styles.title}>Quote request sent</Text>
        <Text style={styles.body}>
          The business will review your job details and follow up with a quote.
        </Text>
      </Card>
    );
  }

  return (
    <Card style={styles.card}>
      <Text style={styles.title}>{discoveryCtaLabel("get_quote")}</Text>
      <Text style={styles.body}>Describe the job and where service is needed.</Text>
      <TextInput
        value={description}
        onChangeText={setDescription}
        placeholder="Describe the issue or job"
        multiline
        style={[styles.input, styles.textArea]}
      />
      <TextInput
        value={serviceAddress}
        onChangeText={setServiceAddress}
        placeholder="Service address (optional)"
        style={styles.input}
      />
      <View style={styles.actions}>
        <Button
          title={submitting ? "Sending…" : "Request quote"}
          onPress={handleSubmit}
          disabled={submitting || description.trim().length < 10}
        />
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  card: {
    marginTop: adeniTheme.spacing.lg,
    gap: adeniTheme.spacing.md,
  },
  title: {
    fontSize: adeniTheme.typography.titleSm.fontSize,
    fontWeight: adeniTheme.typography.titleSm.fontWeight,
    color: adeniTheme.text,
  },
  body: {
    fontSize: adeniTheme.typography.bodySm.fontSize,
    color: adeniTheme.textMuted,
  },
  input: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.md,
    paddingHorizontal: adeniTheme.spacing.md,
    paddingVertical: adeniTheme.spacing.sm,
    fontSize: adeniTheme.typography.bodySm.fontSize,
    color: adeniTheme.text,
  },
  textArea: {
    minHeight: 120,
    textAlignVertical: "top",
  },
  actions: {
    marginTop: adeniTheme.spacing.sm,
  },
});
