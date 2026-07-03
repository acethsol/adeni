"use client";

import { useState } from "react";
import type { BusinessProfile } from "@adeni/shared";

type Props = {
  profile: BusinessProfile;
};

export function BusinessProfileForm({ profile }: Props) {
  const [businessName, setBusinessName] = useState(profile.businessName);
  const [categorySlug, setCategorySlug] = useState(profile.categorySlug);
  const [phone, setPhone] = useState(profile.phone);
  const [description, setDescription] = useState(profile.description);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSaving(true);
    setMessage(null);
    setError(null);

    try {
      const response = await fetch("/api/business/profile", {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          businessName,
          categorySlug,
          phone,
          description: description.trim() || undefined,
        }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(
          typeof payload.title === "string" ? payload.title : "Could not save profile.",
        );
      }

      setMessage("Profile saved.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save profile.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={(event) => void handleSubmit(event)} className="space-y-5">
      {message ? (
        <p className="rounded-lg bg-green-50 px-4 py-3 text-sm text-green-800">{message}</p>
      ) : null}
      {error ? (
        <p className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">{error}</p>
      ) : null}

      <label className="block">
        <span className="text-sm font-medium text-[#1b4332]/70">Business name</span>
        <input
          required
          value={businessName}
          onChange={(event) => setBusinessName(event.target.value)}
          className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
        />
      </label>

      <label className="block">
        <span className="text-sm font-medium text-[#1b4332]/70">Category slug</span>
        <input
          required
          value={categorySlug}
          onChange={(event) => setCategorySlug(event.target.value)}
          className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
        />
      </label>

      <label className="block">
        <span className="text-sm font-medium text-[#1b4332]/70">Phone</span>
        <input
          required
          value={phone}
          onChange={(event) => setPhone(event.target.value)}
          className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
        />
      </label>

      <label className="block">
        <span className="text-sm font-medium text-[#1b4332]/70">Description</span>
        <textarea
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          rows={4}
          className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
        />
      </label>

      <button
        type="submit"
        disabled={saving}
        className="rounded-full bg-[#1b4332] px-5 py-2.5 text-sm font-medium text-white hover:opacity-90 disabled:opacity-60"
      >
        {saving ? "Saving…" : "Save profile"}
      </button>
    </form>
  );
}
