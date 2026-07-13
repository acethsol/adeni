"use client";

import { useState } from "react";
import type { BusinessProfile } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Input, Textarea } from "@/components/ui/input";
import { useToast } from "@/contexts/toast-context";

type Props = {
  profile: BusinessProfile;
};

type FieldErrors = {
  businessName?: string;
  categorySlug?: string;
  phone?: string;
};

const PHONE_PATTERN = /^\+?[0-9\s-]{7,20}$/;

export function BusinessProfileForm({ profile }: Props) {
  const toast = useToast();
  const [businessName, setBusinessName] = useState(profile.businessName);
  const [categorySlug, setCategorySlug] = useState(profile.categorySlug);
  const [phone, setPhone] = useState(profile.phone);
  const [description, setDescription] = useState(profile.description);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function validate(): FieldErrors {
    const errors: FieldErrors = {};
    if (!businessName.trim()) {
      errors.businessName = "Business name is required.";
    }
    if (!categorySlug.trim()) {
      errors.categorySlug = "Category slug is required.";
    }
    if (!phone.trim()) {
      errors.phone = "Phone number is required.";
    } else if (!PHONE_PATTERN.test(phone.trim())) {
      errors.phone = "Enter a valid phone number.";
    }
    return errors;
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();

    const errors = validate();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    setSaving(true);
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

      toast.success("Profile saved");
    } catch (err) {
      const messageText = err instanceof Error ? err.message : "Could not save profile.";
      setError(messageText);
      toast.error(messageText);
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={(event) => void handleSubmit(event)} className="space-y-5">
      {error ? (
        <p className="rounded-lg bg-destructive-bg px-4 py-3 text-sm text-destructive">{error}</p>
      ) : null}

      <Input
        label="Business name"
        required
        value={businessName}
        onChange={(event) => {
          setBusinessName(event.target.value);
          setFieldErrors((current) => ({ ...current, businessName: undefined }));
        }}
        error={fieldErrors.businessName}
      />

      <Input
        label="Category slug"
        required
        value={categorySlug}
        onChange={(event) => {
          setCategorySlug(event.target.value);
          setFieldErrors((current) => ({ ...current, categorySlug: undefined }));
        }}
        error={fieldErrors.categorySlug}
      />

      <Input
        label="Phone"
        required
        value={phone}
        onChange={(event) => {
          setPhone(event.target.value);
          setFieldErrors((current) => ({ ...current, phone: undefined }));
        }}
        error={fieldErrors.phone}
      />

      <Textarea
        label="Description"
        value={description}
        onChange={(event) => setDescription(event.target.value)}
        rows={4}
      />

      <Button type="submit" loading={saving} loadingLabel="Saving…">
        Save profile
      </Button>
    </form>
  );
}
