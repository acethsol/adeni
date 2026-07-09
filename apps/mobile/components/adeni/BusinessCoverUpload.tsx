import { useState } from "react";
import { Image } from "react-native";
import * as ImagePicker from "expo-image-picker";
import { Pressable, StyleSheet, Text, View } from "react-native";
import type { AdeniApiClient } from "@adeni/api-client";
import { resolveBusinessCoverImage } from "@adeni/shared";

const MAX_BYTES = 5 * 1024 * 1024;

type Props = {
  categorySlug: string;
  coverImageUrl?: string | null;
  createClient: () => Promise<AdeniApiClient>;
};

export function BusinessCoverUpload({
  categorySlug,
  coverImageUrl,
  createClient,
}: Props) {
  const [previewUrl, setPreviewUrl] = useState(
    resolveBusinessCoverImage(categorySlug, coverImageUrl),
  );
  const [uploading, setUploading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handlePickImage() {
    setMessage(null);
    setError(null);

    const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permission.granted) {
      setError("Photo library permission is required.");
      return;
    }

    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ["images"],
      quality: 0.85,
    });

    if (result.canceled || !result.assets[0]) {
      return;
    }

    const asset = result.assets[0];
    const contentType = asset.mimeType ?? "image/jpeg";
    if (!["image/jpeg", "image/png", "image/webp"].includes(contentType)) {
      setError("Use a JPEG, PNG, or WebP image.");
      return;
    }

    if ((asset.fileSize ?? 0) > MAX_BYTES) {
      setError("Cover image must be 5 MB or smaller.");
      return;
    }

    setUploading(true);

    try {
      const response = await fetch(asset.uri);
      const blob = await response.blob();
      const client = await createClient();
      const slot = await client.createCoverUploadUrl(contentType, blob.size);

      const uploadResponse = await fetch(slot.uploadUrl, {
        method: "PUT",
        headers: { "Content-Type": contentType },
        body: blob,
      });

      if (!uploadResponse.ok) {
        throw new Error("Cover image upload failed.");
      }

      const nextUrl = await client.updateTenantCoverImage({ coverImageKey: slot.storageKey });
      setPreviewUrl(nextUrl);
      setMessage("Cover photo updated.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not upload cover image.");
    } finally {
      setUploading(false);
    }
  }

  return (
    <View style={styles.card}>
      <Text style={styles.cardTitle}>Cover photo</Text>
      <Text style={styles.helpText}>Shown on discovery cards and your public profile.</Text>
      <Image source={{ uri: previewUrl }} style={styles.preview} />
      {message ? <Text style={styles.success}>{message}</Text> : null}
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <Pressable
        onPress={() => void handlePickImage()}
        disabled={uploading}
        style={({ pressed }) => [
          styles.secondaryButton,
          (pressed || uploading) && styles.buttonPressed,
        ]}
      >
        <Text style={styles.secondaryButtonText}>
          {uploading ? "Uploading…" : "Change cover photo"}
        </Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: "#fff",
    borderRadius: 16,
    borderWidth: 1,
    borderColor: "rgba(27, 67, 50, 0.1)",
    padding: 16,
    gap: 12,
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: "600",
    color: "#1b4332",
  },
  helpText: {
    fontSize: 14,
    color: "rgba(27, 67, 50, 0.65)",
  },
  preview: {
    width: "100%",
    height: 180,
    borderRadius: 16,
  },
  secondaryButton: {
    alignSelf: "flex-start",
    borderWidth: 1,
    borderColor: "rgba(27, 67, 50, 0.2)",
    borderRadius: 999,
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  secondaryButtonText: {
    color: "#1b4332",
    fontWeight: "600",
  },
  buttonPressed: {
    opacity: 0.7,
  },
  success: {
    color: "#166534",
    fontSize: 14,
  },
  error: {
    color: "#b91c1c",
    fontSize: 14,
  },
});
