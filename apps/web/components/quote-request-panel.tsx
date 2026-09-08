"use client";

import { useRef, useState } from "react";
import { discoveryCtaLabel } from "@adeni/shared";
import { Callout } from "@/components/ui/callout";
import { Input, Textarea } from "@/components/ui/input";
import { useActionLoading } from "@/contexts/action-loading-context";
import { useApiErrorMessage } from "@/lib/api-error";

type Props = {
  slug: string;
  loginHref: string;
  enabled: boolean;
};

const ALLOWED_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const MAX_BYTES = 5 * 1024 * 1024;
const MAX_PHOTOS = 5;

export function QuoteRequestPanel({ slug, loginHref, enabled }: Props) {
  const { run } = useActionLoading();
  const { formatApiError } = useApiErrorMessage();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [description, setDescription] = useState("");
  const [serviceAddress, setServiceAddress] = useState("");
  const [photoKeys, setPhotoKeys] = useState<string[]>([]);
  const [uploadingPhotos, setUploadingPhotos] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const title = discoveryCtaLabel("get_quote");

  async function handlePhotoSelect(event: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? []);
    event.target.value = "";

    if (files.length === 0) {
      return;
    }

    setUploadingPhotos(true);
    setError(null);

    try {
      const nextKeys = [...photoKeys];

      for (const file of files) {
        if (nextKeys.length >= MAX_PHOTOS) {
          break;
        }

        if (!ALLOWED_TYPES.has(file.type)) {
          throw new Error("Use JPEG, PNG, or WebP photos.");
        }

        if (file.size > MAX_BYTES) {
          throw new Error("Each photo must be 5 MB or smaller.");
        }

        const slotResponse = await fetch("/api/customer/media/upload-url", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            purpose: "quote_photo",
            contentType: file.type,
            contentLength: file.size,
          }),
        });

        const slotPayload = await slotResponse.json().catch(() => ({}));
        if (!slotResponse.ok) {
          throw new Error(formatApiError(slotPayload, "Could not start photo upload."));
        }

        const uploadResponse = await fetch(slotPayload.uploadUrl as string, {
          method: "PUT",
          headers: { "Content-Type": file.type },
          body: file,
        });

        if (!uploadResponse.ok) {
          throw new Error("Photo upload failed.");
        }

        nextKeys.push(slotPayload.storageKey as string);
      }

      setPhotoKeys(nextKeys);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not upload photos.");
    } finally {
      setUploadingPhotos(false);
    }
  }

  if (!enabled) {
    return (
      <section className="mt-8 rounded-2xl border border-border bg-surface p-8 shadow-sm">
        <h2 className="text-lg font-semibold">{title}</h2>
        <p className="mt-2 text-sm text-muted">
          Sign in to describe your job and request a quote from this business.
        </p>
        <a href={loginHref} className="mt-4 inline-flex rounded-full bg-primary px-6 py-3 text-sm font-semibold text-primary-foreground">
          Sign in to continue
        </a>
      </section>
    );
  }

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);

    try {
      await run("Sending quote request…", async () => {
        const response = await fetch(`/api/businesses/${encodeURIComponent(slug)}/quote-requests`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            description: description.trim(),
            serviceAddress: serviceAddress.trim() || undefined,
            photoKeys: photoKeys.length > 0 ? photoKeys : undefined,
          }),
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(formatApiError(payload, "Could not send quote request."));
        }

        setSubmitted(true);
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not send quote request.");
    } finally {
      setSubmitting(false);
    }
  }

  if (submitted) {
    return (
      <section className="mt-8 rounded-2xl border border-accent/30 bg-surface p-8 shadow-sm">
        <p className="text-sm font-semibold uppercase tracking-widest text-accent">Quote request sent</p>
        <h2 className="mt-2 text-xl font-bold">We received your job details</h2>
        <p className="mt-3 text-sm text-muted">
          The business will review your request and send a formal quote. Track progress on{" "}
          <a href="/my-quotes" className="font-semibold text-accent underline">
            My quotes
          </a>
          .
        </p>
      </section>
    );
  }

  return (
    <section className="mt-8 rounded-2xl border border-border bg-surface p-8 shadow-sm">
      <h2 className="text-lg font-semibold">{title}</h2>
      <p className="mt-2 text-sm text-muted">
        Describe the job and where service is needed. The business will respond with a price and proposed schedule.
      </p>

      <div className="mt-6 space-y-4">
        <Textarea
          label="Describe the job"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          rows={5}
          placeholder="Example: Kitchen sink is leaking under the cabinet. Need repair this week."
        />
        <Input
          label="Service address (optional)"
          value={serviceAddress}
          onChange={(event) => setServiceAddress(event.target.value)}
          placeholder="Street, area, city"
        />
        <div>
          <p className="text-sm font-medium text-foreground">Photos (optional)</p>
          <p className="mt-1 text-xs text-muted">Add up to {MAX_PHOTOS} photos of the job site or issue.</p>
          <input
            ref={fileInputRef}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            multiple
            className="mt-2 block w-full text-sm"
            onChange={(event) => void handlePhotoSelect(event)}
            disabled={uploadingPhotos || photoKeys.length >= MAX_PHOTOS}
          />
          {photoKeys.length > 0 ? (
            <p className="mt-2 text-xs text-muted">{photoKeys.length} photo(s) attached</p>
          ) : null}
        </div>
        {error ? <Callout tone="error">{error}</Callout> : null}
        <button
          type="button"
          onClick={handleSubmit}
          disabled={submitting || uploadingPhotos || description.trim().length < 10}
          className="inline-flex rounded-full bg-primary px-6 py-3 text-sm font-semibold text-primary-foreground disabled:opacity-60"
        >
          {submitting ? "Sending…" : "Request quote"}
        </button>
      </div>
    </section>
  );
}
