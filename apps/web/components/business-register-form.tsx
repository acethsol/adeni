"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import type { Category } from "@adeni/shared";
import { markets } from "@adeni/shared";

type Props = {
  categories: Category[];
};

export function BusinessRegisterForm({ categories }: Props) {
  const router = useRouter();
  const [businessName, setBusinessName] = useState("");
  const [categorySlug, setCategorySlug] = useState(categories[0]?.slug ?? "barbers");
  const [phone, setPhone] = useState("");
  const [description, setDescription] = useState("");
  const [slug, setSlug] = useState("");
  const [locationName, setLocationName] = useState("");
  const [addressLine, setAddressLine] = useState("");
  const [area, setArea] = useState("");
  const [marketId, setMarketId] = useState("lagos");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

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

  const marketOptions = Object.values(markets);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
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
    } catch (err) {
      setError(err instanceof Error ? err.message : "Registration failed.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={(event) => void handleSubmit(event)} className="space-y-6">
      {error ? (
        <p className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">{error}</p>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2">
        <label className="block text-sm">
          <span className="font-medium">Business name</span>
          <input
            required
            value={businessName}
            onChange={(event) => setBusinessName(event.target.value)}
            className="mt-2 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
          />
        </label>
        <label className="block text-sm">
          <span className="font-medium">Category</span>
          <select
            value={categorySlug}
            onChange={(event) => setCategorySlug(event.target.value)}
            className="mt-2 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
          >
            {categories.map((category) => (
              <option key={category.slug} value={category.slug}>
                {category.name}
              </option>
            ))}
          </select>
        </label>
        <label className="block text-sm">
          <span className="font-medium">Phone</span>
          <input
            required
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
            className="mt-2 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
          />
        </label>
        <label className="block text-sm sm:col-span-2">
          <span className="font-medium">Description (optional)</span>
          <textarea
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            rows={3}
            className="mt-2 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
          />
        </label>
      </div>

      <div className="rounded-xl border border-[#1b4332]/10 bg-white p-5">
        <h2 className="text-lg font-semibold">Primary location</h2>
        <div className="mt-4 grid gap-4 sm:grid-cols-2">
          <label className="block text-sm">
            <span className="font-medium">Public slug</span>
            <input
              required
              value={slug}
              onChange={(event) => setSlug(event.target.value)}
              placeholder="lekki-cuts"
              className="mt-2 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
            />
          </label>
          <label className="block text-sm">
            <span className="font-medium">Location name (optional)</span>
            <input
              value={locationName}
              onChange={(event) => setLocationName(event.target.value)}
              className="mt-2 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
            />
          </label>
          <label className="block text-sm sm:col-span-2">
            <span className="font-medium">Address</span>
            <input
              required
              value={addressLine}
              onChange={(event) => setAddressLine(event.target.value)}
              className="mt-2 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
            />
          </label>
          <label className="block text-sm">
            <span className="font-medium">Area</span>
            <input
              required
              value={area}
              onChange={(event) => setArea(event.target.value)}
              className="mt-2 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
            />
          </label>
          <label className="block text-sm">
            <span className="font-medium">Market</span>
            <select
              value={marketId}
              onChange={(event) => setMarketId(event.target.value)}
              className="mt-2 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
            >
              {marketOptions.map((market) => (
                <option key={market.id} value={market.id}>
                  {market.name}
                </option>
              ))}
            </select>
          </label>
        </div>
      </div>

      <button
        type="submit"
        disabled={submitting}
        className="rounded-full bg-[#1b4332] px-6 py-3 text-sm font-medium text-white disabled:opacity-60"
      >
        {submitting ? "Creating business…" : "Register business"}
      </button>
    </form>
  );
}
