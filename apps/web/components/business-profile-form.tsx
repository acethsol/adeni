"use client";

import { useMemo, useState } from "react";
import { Lock } from "lucide-react";
import type { BusinessProfile } from "@adeni/shared";
import { formatTenantStatus } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { Input, Textarea } from "@/components/ui/input";
import { useToast } from "@/contexts/toast-context";
import { useUnsavedChangesGuard } from "@/contexts/navigation-guard-context";
import { useApiErrorMessage } from "@/lib/api-error";

type Props = {
  profile: BusinessProfile;
};

type FieldErrors = {
  businessName?: string;
  categorySlug?: string;
  phone?: string;
};

type FormValues = {
  businessName: string;
  categorySlug: string;
  phone: string;
  description: string;
};

const PHONE_PATTERN = /^\+?[0-9\s-]{7,20}$/;

function toValues(profile: BusinessProfile): FormValues {
  return {
    businessName: profile.businessName,
    categorySlug: profile.categorySlug,
    phone: profile.phone,
    description: profile.description,
  };
}

export function BusinessProfileForm({ profile }: Props) {
  const toast = useToast();
  const { formatApiError } = useApiErrorMessage();
  const canEdit = profile.status === 0 || profile.status === 3;

  const initialValues = useMemo(() => toValues(profile), [profile]);
  const [values, setValues] = useState<FormValues>(initialValues);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isDirty =
    values.businessName !== initialValues.businessName ||
    values.categorySlug !== initialValues.categorySlug ||
    values.phone !== initialValues.phone ||
    values.description !== initialValues.description;

  useUnsavedChangesGuard(canEdit && isDirty);

  if (!canEdit) {
    return (
      <div className="space-y-4">
        <Callout tone="info">
          Your profile is locked while status is <strong>{formatTenantStatus(profile.status)}</strong>. Business
          details can only be edited in Draft or Rejected status. Contact support if you need changes made.
        </Callout>
        <dl className="space-y-3 rounded-xl border border-border bg-subtle/40 p-4 text-sm">
          <ReadOnlyRow label="Business name" value={profile.businessName} />
          <ReadOnlyRow label="Category slug" value={profile.categorySlug} />
          <ReadOnlyRow label="Phone" value={profile.phone} />
          <ReadOnlyRow label="Description" value={profile.description || "—"} />
        </dl>
      </div>
    );
  }

  function validate(): FieldErrors {
    const errors: FieldErrors = {};
    if (!values.businessName.trim()) {
      errors.businessName = "Business name is required.";
    }
    if (!values.categorySlug.trim()) {
      errors.categorySlug = "Category slug is required.";
    }
    if (!values.phone.trim()) {
      errors.phone = "Phone number is required.";
    } else if (!PHONE_PATTERN.test(values.phone.trim())) {
      errors.phone = "Enter a valid phone number.";
    }
    return errors;
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();

    if (!isDirty) return;

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
          businessName: values.businessName,
          categorySlug: values.categorySlug,
          phone: values.phone,
          description: values.description.trim() || undefined,
        }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Could not save profile."));
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
        value={values.businessName}
        onChange={(event) => {
          setValues((current) => ({ ...current, businessName: event.target.value }));
          setFieldErrors((current) => ({ ...current, businessName: undefined }));
        }}
        error={fieldErrors.businessName}
      />

      <Input
        label="Category slug"
        required
        value={values.categorySlug}
        onChange={(event) => {
          setValues((current) => ({ ...current, categorySlug: event.target.value }));
          setFieldErrors((current) => ({ ...current, categorySlug: undefined }));
        }}
        error={fieldErrors.categorySlug}
      />

      <Input
        label="Phone"
        required
        value={values.phone}
        onChange={(event) => {
          setValues((current) => ({ ...current, phone: event.target.value }));
          setFieldErrors((current) => ({ ...current, phone: undefined }));
        }}
        error={fieldErrors.phone}
      />

      <Textarea
        label="Description"
        value={values.description}
        onChange={(event) => setValues((current) => ({ ...current, description: event.target.value }))}
        rows={4}
      />

      <Button type="submit" loading={saving} loadingLabel="Saving…" disabled={!isDirty}>
        Save profile
      </Button>
    </form>
  );
}

function ReadOnlyRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-4">
      <dt className="flex items-center gap-1.5 text-muted">
        <Lock className="h-3 w-3 shrink-0" aria-hidden />
        {label}
      </dt>
      <dd className="text-right font-medium text-foreground">{value}</dd>
    </div>
  );
}
