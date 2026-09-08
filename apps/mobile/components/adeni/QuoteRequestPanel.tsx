import { discoveryCtaLabel, MAX_QUOTE_PHOTOS, MAX_UPLOAD_BYTES } from "@adeni/shared";
import { useState } from "react";
import { Alert, Image, Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import * as ImagePicker from "expo-image-picker";
import { useRouter } from "expo-router";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { useAuth } from "@/contexts/auth-context";
import { adeniTheme } from "@/lib/theme";

type Props = {
  slug: string;
};

export function QuoteRequestPanel({ slug }: Props) {
  const router = useRouter();
  const { isBookingEnabled, createApiClient } = useAuth();
  const [description, setDescription] = useState("");
  const [serviceAddress, setServiceAddress] = useState("");
  const [photoKeys, setPhotoKeys] = useState<string[]>([]);
  const [photoPreviews, setPhotoPreviews] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [uploadingPhotos, setUploadingPhotos] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  async function handleAddPhoto() {
    if (photoKeys.length >= MAX_QUOTE_PHOTOS) {
      return;
    }

    const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permission.granted) {
      Alert.alert("Photo permission required");
      return;
    }

    const result = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ["images"], quality: 0.85 });
    if (result.canceled || !result.assets[0]) {
      return;
    }

    const asset = result.assets[0];
    const contentType = asset.mimeType ?? "image/jpeg";
    if ((asset.fileSize ?? 0) > MAX_UPLOAD_BYTES) {
      Alert.alert("Photo must be 5 MB or smaller.");
      return;
    }

    setUploadingPhotos(true);
    try {
      const response = await fetch(asset.uri);
      const blob = await response.blob();
      const client = createApiClient("customer");
      const slot = await client.createQuotePhotoUploadUrl({ purpose: "quote_photo", contentType, contentLength: blob.size });
      const uploadResponse = await fetch(slot.uploadUrl, {
        method: "PUT",
        headers: { "Content-Type": contentType },
        body: blob,
      });
      if (!uploadResponse.ok) {
        throw new Error("Upload failed");
      }
      setPhotoKeys((current) => [...current, slot.storageKey]);
      setPhotoPreviews((current) => [...current, asset.uri]);
    } catch {
      Alert.alert("Could not upload photo.");
    } finally {
      setUploadingPhotos(false);
    }
  }

  async function handleSubmit() {
    if (!isBookingEnabled) {
      Alert.alert("Sign in required", "Sign in from Account to request a quote.");
      return;
    }

    setSubmitting(true);
    try {
      const client = createApiClient("customer");
      await client.createQuoteRequest(slug, {
        description: description.trim(),
        serviceAddress: serviceAddress.trim() || undefined,
        photoKeys: photoKeys.length > 0 ? photoKeys : undefined,
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
        <Text style={styles.body}>Track progress in My quotes.</Text>
        <Button title="View my quotes" onPress={() => router.push("/my-quotes")} />
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
      <View style={styles.photoRow}>
        {photoPreviews.map((uri) => (
          <Image key={uri} source={{ uri }} style={styles.photo} />
        ))}
      </View>
      <Pressable onPress={() => void handleAddPhoto()} disabled={uploadingPhotos || photoKeys.length >= MAX_QUOTE_PHOTOS}>
        <Text style={styles.photoLink}>
          {uploadingPhotos ? "Uploading…" : `Add photo (${photoKeys.length}/${MAX_QUOTE_PHOTOS})`}
        </Text>
      </Pressable>
      <View style={styles.actions}>
        <Button
          title={submitting ? "Sending…" : "Request quote"}
          onPress={handleSubmit}
          disabled={submitting || uploadingPhotos || description.trim().length < 10}
        />
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  card: { marginTop: adeniTheme.spacing.lg, gap: adeniTheme.spacing.md },
  title: { fontSize: adeniTheme.typography.titleSm.fontSize, fontWeight: "700", color: adeniTheme.text },
  body: { fontSize: adeniTheme.typography.bodySm.fontSize, color: adeniTheme.textMuted },
  input: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.md,
    paddingHorizontal: adeniTheme.spacing.md,
    paddingVertical: adeniTheme.spacing.sm,
    fontSize: adeniTheme.typography.bodySm.fontSize,
    color: adeniTheme.text,
  },
  textArea: { minHeight: 120, textAlignVertical: "top" },
  photoRow: { flexDirection: "row", flexWrap: "wrap", gap: adeniTheme.spacing.sm },
  photo: { width: 64, height: 64, borderRadius: adeniTheme.radius.md },
  photoLink: { color: adeniTheme.accent, fontWeight: "600" },
  actions: { marginTop: adeniTheme.spacing.sm },
});
