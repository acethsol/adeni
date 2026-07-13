"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Building2, MapPin, Sparkles } from "lucide-react";
import type { Category, MarketConfig } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Input, Textarea } from "@/components/ui/input";
import { BusinessPortalCard } from "@/components/business-portal-card";
import { useActionLoading } from "@/contexts/action-loading-context";

type Props = {
  categories: Category[];
  markets: MarketConfig[];
};

type FieldName = "businessName" | "phone" | "slug" | "addressLine" | "area";
type FieldErrors = Partial<Record<FieldName, string>>;

const PHONE_PATTERN = /^\+?[0-9\s-]{7,20}$/;
const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

const steps = [
  {
    icon: Building2,
    title: "Business details",
    description: "Name, category, and how customers reach you.",
  },
  {
    icon: MapPin,
    title: "Primary location",
    description: "Your public profile URL and service area.",
  },
  {
    icon: Sparkles,
    title: "Go live",
    description: "Add services, hours, and submit verification.",
  },
] as const;

export function BusinessRegisterForm({ categories, markets }: Props) {
  const router = useRouter();
  const { run, isActive } = useActionLoading();
  const [businessName, setBusinessName] = useState("");
  const [categorySlug, setCategorySlug] = useState(categories[0]?.slug ?? "barbers");
  const [phone, setPhone] = useState("");
  const [description, setDescription] = useState("");
  const [slug, setSlug] = useState("");
  const [locationName, setLocationName] = useState("");
  const [addressLine, setAddressLine] = useState("");
  const [area, setArea] = useState("");
  const [marketId, setMarketId] = useState(markets[0]?.id ?? "lagos");
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  function clearFieldError(field: FieldName) {
    setFieldErrors((current) => {
      if (!current[field]) {
        return current;
      }
      const next = { ...current };
      delete next[field];
      return next;
    });
  }

  function validate(): FieldErrors {
    const errors: FieldErrors = {};

    if (!businessName.trim()) {
      errors.businessName = "Business name is required.";
    }

    if (!phone.trim()) {
      errors.phone = "Phone number is required.";
    } else if (!PHONE_PATTERN.test(phone.trim())) {
      errors.phone = "Enter a valid phone number.";
    }

    const trimmedSlug = slug.trim().toLowerCase();
    if (!trimmedSlug) {
      errors.slug = "Public slug is required.";
    } else if (!SLUG_PATTERN.test(trimmedSlug)) {
      errors.slug = "Use lowercase letters, numbers, and hyphens only.";
    }

    if (!addressLine.trim()) {
      errors.addressLine = "Address is required.";
    }

    if (!area.trim()) {
      errors.area = "Area is required.";
    }

    return errors;
  }

  useEffect(() => {
    if (!slug && businessName) {
      setSlug(
        businessName
          .toLowerCase()
          .replace(/[^a-z0-9]+/g, "-")
          .replace(/^-+|-+$/g, "")
          .slice(0, 48),
      );
    }
  }, [businessName, slug]);

  const marketOptions = markets;

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);

    const errors = validate();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    await run("Creating your business profile…", async () => {
      const response = await fetch("/api/business/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          businessName: businessName.trim(),
          categorySlug,
          phone: phone.trim(),
          description: description.trim() || undefined,
          location: {
            slug: slug.trim(),
            name: locationName.trim() || undefined,
            addressLine: addressLine.trim(),
            area: area.trim(),
            marketId,
          },
        }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(
          typeof payload.title === "string" ? payload.title : "Registration failed.",
        );
      }

      router.push("/business/profile");
      router.refresh();
    }).catch((err) => {
      setError(err instanceof Error ? err.message : "Registration failed.");
    });
  }

  const inputClass =
    "mt-2 w-full rounded-xl border border-border bg-background px-3 py-2.5 text-sm outline-none transition-shadow focus:border-accent/50 focus:ring-2 focus:ring-accent/20";

  return (
    <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_280px]">
      <form onSubmit={(event) => void handleSubmit(event)} className="space-y-6">
        {error ? (
          <p className="rounded-xl bg-destructive-bg px-4 py-3 text-sm text-destructive">
            {error}
          </p>
        ) : null}

        <BusinessPortalCard>
          <h2 className="text-lg font-semibold text-foreground">About your business</h2>
          <p className="mt-1 text-sm text-muted">
            This is what customers see on your public profile.
          </p>
          <div className="mt-5 grid gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <Input
                label="Business name"
                required
                value={businessName}
                onChange={(event) => {
                  setBusinessName(event.target.value);
                  clearFieldError("businessName");
                }}
                placeholder="Lekki Cuts"
                error={fieldErrors.businessName}
              />
            </div>
            <label className="block text-sm">
              <span className="font-medium text-foreground">Category</span>
              <select
                value={categorySlug}
                onChange={(event) => setCategorySlug(event.target.value)}
                className={inputClass}
              >
                {categories.map((category) => (
                  <option key={category.slug} value={category.slug}>
                    {category.name}
                  </option>
                ))}
              </select>
            </label>
            <Input
              label="Phone"
              required
              value={phone}
              onChange={(event) => {
                setPhone(event.target.value);
                clearFieldError("phone");
              }}
              placeholder="+234 ..."
              error={fieldErrors.phone}
            />
            <div className="sm:col-span-2">
              <Textarea
                label="Description (optional)"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                rows={3}
                placeholder="What makes your business special?"
              />
            </div>
          </div>
        </BusinessPortalCard>

        <BusinessPortalCard>
          <h2 className="text-lg font-semibold text-foreground">Primary location</h2>
          <p className="mt-1 text-sm text-muted">
            Sets your public URL and which market you appear in.
          </p>
          <div className="mt-5 grid gap-4 sm:grid-cols-2">
            <div>
              <Input
                label="Public slug"
                required
                value={slug}
                onChange={(event) => {
                  setSlug(event.target.value);
                  clearFieldError("slug");
                }}
                placeholder="lekki-cuts"
                className="font-mono"
                error={fieldErrors.slug}
              />
              <span className="mt-1 block text-xs text-muted">
                adeni.com/businesses/{slug || "your-slug"}
              </span>
            </div>
            <Input
              label="Location name (optional)"
              value={locationName}
              onChange={(event) => setLocationName(event.target.value)}
              placeholder="Lekki Phase 1"
            />
            <div className="sm:col-span-2">
              <Input
                label="Address"
                required
                value={addressLine}
                onChange={(event) => {
                  setAddressLine(event.target.value);
                  clearFieldError("addressLine");
                }}
                error={fieldErrors.addressLine}
              />
            </div>
            <Input
              label="Area"
              required
              value={area}
              onChange={(event) => {
                setArea(event.target.value);
                clearFieldError("area");
              }}
              placeholder="Lekki"
              error={fieldErrors.area}
            />
            <label className="block text-sm">
              <span className="font-medium text-foreground">Market</span>
              <select
                value={marketId}
                onChange={(event) => setMarketId(event.target.value)}
                className={inputClass}
              >
                {marketOptions.map((market) => (
                  <option key={market.id} value={market.id}>
                    {market.name}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </BusinessPortalCard>

        <Button type="submit" size="lg" loading={isActive} loadingLabel="Creating business…">
          Create business profile
        </Button>
      </form>

      <aside className="lg:sticky lg:top-24 lg:self-start">
        <BusinessPortalCard className="bg-subtle/80">
          <p className="text-xs font-bold uppercase tracking-widest text-accent">How it works</p>
          <ol className="mt-4 space-y-5">
            {steps.map((step, index) => (
              <li key={step.title} className="flex gap-3">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-surface text-accent ring-1 ring-border">
                  <step.icon className="h-4 w-4" aria-hidden />
                </div>
                <div>
                  <p className="text-xs font-semibold text-muted">Step {index + 1}</p>
                  <p className="font-semibold text-foreground">{step.title}</p>
                  <p className="mt-0.5 text-sm text-muted">{step.description}</p>
                </div>
              </li>
            ))}
          </ol>
        </BusinessPortalCard>
      </aside>
    </div>
  );
}
