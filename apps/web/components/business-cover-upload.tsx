"use client";

import { useRef, useState } from "react";
import { resolveBusinessCoverImage } from "@adeni/shared";

type Props = {
  categorySlug: string;
  coverImageUrl?: string | null;
};

const ALLOWED_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const MAX_BYTES = 5 * 1024 * 1024;

export function BusinessCoverUpload({ categorySlug, coverImageUrl }: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [previewUrl, setPreviewUrl] = useState(
    resolveBusinessCoverImage(categorySlug, coverImageUrl),
  );
  const [uploading, setUploading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    setMessage(null);
    setError(null);

    if (!ALLOWED_TYPES.has(file.type)) {
      setError("Use a JPEG, PNG, or WebP image.");
      return;
    }

    if (file.size > MAX_BYTES) {
      setError("Cover image must be 5 MB or smaller.");
      return;
    }

    setUploading(true);

    try {
      const slotResponse = await fetch("/api/business/media/upload-url", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          purpose: "cover",
          contentType: file.type,
          contentLength: file.size,
        }),
      });

      const slotPayload = await slotResponse.json().catch(() => ({}));
      if (!slotResponse.ok) {
        throw new Error(
          typeof slotPayload.title === "string"
            ? slotPayload.title
            : "Could not start cover upload.",
        );
      }

      const uploadResponse = await fetch(slotPayload.uploadUrl as string, {
        method: "PUT",
        headers: { "Content-Type": file.type },
        body: file,
      });

      if (!uploadResponse.ok) {
        throw new Error("Cover image upload failed.");
      }

      const confirmResponse = await fetch("/api/business/profile/cover", {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ coverImageKey: slotPayload.storageKey }),
      });

      const confirmPayload = await confirmResponse.json().catch(() => ({}));
      if (!confirmResponse.ok) {
        throw new Error(
          typeof confirmPayload.title === "string"
            ? confirmPayload.title
            : "Could not save cover image.",
        );
      }

      const nextUrl =
        typeof confirmPayload.coverImageUrl === "string"
          ? confirmPayload.coverImageUrl
          : previewUrl;
      setPreviewUrl(nextUrl);
      setMessage("Cover photo updated.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not upload cover image.");
    } finally {
      setUploading(false);
      if (inputRef.current) {
        inputRef.current.value = "";
      }
    }
  }

  return (
    <div className="space-y-3">
      <div className="overflow-hidden rounded-2xl border border-[#1b4332]/10">
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img src={previewUrl} alt="Business cover" className="h-48 w-full object-cover" />
      </div>

      {message ? (
        <p className="rounded-lg bg-green-50 px-4 py-3 text-sm text-green-800">{message}</p>
      ) : null}
      {error ? (
        <p className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">{error}</p>
      ) : null}

      <div>
        <input
          ref={inputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          className="hidden"
          onChange={(event) => void handleFileChange(event)}
        />
        <button
          type="button"
          disabled={uploading}
          onClick={() => inputRef.current?.click()}
          className="rounded-full border border-[#1b4332]/20 px-5 py-2.5 text-sm font-medium text-[#1b4332] hover:bg-[#1b4332]/5 disabled:opacity-60"
        >
          {uploading ? "Uploading…" : "Change cover photo"}
        </button>
      </div>
    </div>
  );
}
