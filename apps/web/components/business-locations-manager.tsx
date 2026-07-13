"use client";

import { useState } from "react";
import type { BusinessLocation, MarketConfig } from "@adeni/shared";

type Props = {
  initialLocations: BusinessLocation[];
  defaultMarketId?: string;
  markets: MarketConfig[];
};

type LocationDraft = {
  slug: string;
  name: string;
  addressLine: string;
  area: string;
  marketId: string;
  isPrimary: boolean;
};

const EMPTY_DRAFT = (marketId: string): LocationDraft => ({
  slug: "",
  name: "",
  addressLine: "",
  area: "",
  marketId,
  isPrimary: false,
});

export function BusinessLocationsManager({
  initialLocations,
  defaultMarketId = "lagos",
  markets,
}: Props) {
  const marketOptions = markets.map((market) => ({
    id: market.id,
    name: market.name,
  }));

  const [locations, setLocations] = useState(initialLocations);
  const [draft, setDraft] = useState<LocationDraft>(() => EMPTY_DRAFT(defaultMarketId));
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<LocationDraft | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function handleCreate(event: React.FormEvent) {
    event.preventDefault();
    setBusy("create");
    setError(null);
    setMessage(null);

    try {
      const response = await fetch("/api/business/locations", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          slug: draft.slug.trim().toLowerCase(),
          name: draft.name.trim() || undefined,
          addressLine: draft.addressLine.trim(),
          area: draft.area.trim(),
          marketId: draft.marketId,
          isPrimary: draft.isPrimary || undefined,
        }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(
          typeof payload.title === "string" ? payload.title : "Could not add location.",
        );
      }

      const created = payload as BusinessLocation;
      setLocations((current) => {
        const next = draft.isPrimary
          ? current.map((item) => ({ ...item, isPrimary: false }))
          : current;
        return [...next, created].sort((a, b) =>
          a.isPrimary === b.isPrimary ? a.name.localeCompare(b.name) : a.isPrimary ? -1 : 1,
        );
      });
      setDraft(EMPTY_DRAFT(defaultMarketId));
      setMessage("Location added.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not add location.");
    } finally {
      setBusy(null);
    }
  }

  function startEdit(location: BusinessLocation) {
    setEditingId(location.id);
    setEditDraft({
      slug: location.slug,
      name: location.name,
      addressLine: location.addressLine,
      area: location.area,
      marketId: location.marketId,
      isPrimary: location.isPrimary,
    });
    setError(null);
    setMessage(null);
  }

  async function handleUpdate(locationId: string) {
    if (!editDraft) {
      return;
    }

    setBusy(locationId);
    setError(null);
    setMessage(null);

    try {
      const response = await fetch(`/api/business/locations/${locationId}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          slug: editDraft.slug.trim().toLowerCase(),
          name: editDraft.name.trim() || undefined,
          addressLine: editDraft.addressLine.trim(),
          area: editDraft.area.trim(),
          marketId: editDraft.marketId,
          isPrimary: editDraft.isPrimary,
        }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(
          typeof payload.title === "string" ? payload.title : "Could not update location.",
        );
      }

      const updated = payload as BusinessLocation;
      setLocations((current) =>
        current
          .map((item) => {
            if (item.id === locationId) {
              return updated;
            }
            if (updated.isPrimary) {
              return { ...item, isPrimary: false };
            }
            return item;
          })
          .sort((a, b) =>
            a.isPrimary === b.isPrimary ? a.name.localeCompare(b.name) : a.isPrimary ? -1 : 1,
          ),
      );
      setEditingId(null);
      setEditDraft(null);
      setMessage("Location updated.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not update location.");
    } finally {
      setBusy(null);
    }
  }

  async function handleDeactivate(locationId: string) {
    setBusy(`deactivate-${locationId}`);
    setError(null);
    setMessage(null);

    try {
      const response = await fetch(`/api/business/locations/${locationId}`, {
        method: "DELETE",
      });

      if (!response.ok) {
        const payload = await response.json().catch(() => ({}));
        throw new Error(
          typeof payload.title === "string" ? payload.title : "Could not remove location.",
        );
      }

      setLocations((current) => current.filter((item) => item.id !== locationId));
      setMessage("Location removed.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not remove location.");
    } finally {
      setBusy(null);
    }
  }

  function renderDraftFields(
    value: LocationDraft,
    onChange: (next: LocationDraft) => void,
    slugDisabled = false,
  ) {
    return (
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="block">
          <span className="text-sm font-medium text-[#1b4332]/70">URL slug</span>
          <input
            required
            disabled={slugDisabled}
            value={value.slug}
            onChange={(event) => onChange({ ...value, slug: event.target.value })}
            placeholder="lekki-branch"
            className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2 disabled:opacity-60"
          />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-[#1b4332]/70">Display name</span>
          <input
            value={value.name}
            onChange={(event) => onChange({ ...value, name: event.target.value })}
            placeholder="Optional — defaults to area"
            className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
          />
        </label>
        <label className="block sm:col-span-2">
          <span className="text-sm font-medium text-[#1b4332]/70">Address</span>
          <input
            required
            value={value.addressLine}
            onChange={(event) => onChange({ ...value, addressLine: event.target.value })}
            className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
          />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-[#1b4332]/70">Area</span>
          <input
            required
            value={value.area}
            onChange={(event) => onChange({ ...value, area: event.target.value })}
            className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
          />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-[#1b4332]/70">Market</span>
          <select
            required
            value={value.marketId}
            onChange={(event) => onChange({ ...value, marketId: event.target.value })}
            className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
          >
            {marketOptions.map((market) => (
              <option key={market.id} value={market.id}>
                {market.name}
              </option>
            ))}
          </select>
        </label>
        <label className="flex items-center gap-2 sm:col-span-2">
          <input
            type="checkbox"
            checked={value.isPrimary}
            onChange={(event) => onChange({ ...value, isPrimary: event.target.checked })}
          />
          <span className="text-sm text-[#1b4332]/80">Set as primary location</span>
        </label>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      {message ? (
        <p className="rounded-lg bg-green-50 px-4 py-3 text-sm text-green-800">{message}</p>
      ) : null}
      {error ? (
        <p className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">{error}</p>
      ) : null}

      <form
        onSubmit={(event) => void handleCreate(event)}
        className="rounded-xl border border-[#1b4332]/10 bg-white p-6 shadow-sm"
      >
        <h2 className="text-lg font-semibold">Add location</h2>
        <p className="mt-2 text-sm text-[#1b4332]/70">
          Each branch gets its own public URL at{" "}
          <code className="text-xs">/businesses/your-slug</code>.
        </p>
        <div className="mt-4">{renderDraftFields(draft, setDraft)}</div>
        <button
          type="submit"
          disabled={busy === "create"}
          className="mt-4 rounded-full bg-[#1b4332] px-5 py-2.5 text-sm font-medium text-white disabled:opacity-60"
        >
          {busy === "create" ? "Adding…" : "Add location"}
        </button>
      </form>

      <section>
        <h2 className="text-lg font-semibold">Your locations</h2>
        {locations.length === 0 ? (
          <p className="mt-3 text-sm text-[#1b4332]/70">No locations yet.</p>
        ) : (
          <ul className="mt-4 space-y-3">
            {locations.map((location) => (
              <li
                key={location.id}
                className="rounded-xl border border-[#1b4332]/10 bg-white p-5 shadow-sm"
              >
                {editingId === location.id && editDraft ? (
                  <div>
                    {renderDraftFields(editDraft, setEditDraft)}
                    <div className="mt-4 flex gap-2">
                      <button
                        type="button"
                        onClick={() => void handleUpdate(location.id)}
                        disabled={busy === location.id}
                        className="rounded-full bg-[#1b4332] px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
                      >
                        Save
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setEditingId(null);
                          setEditDraft(null);
                        }}
                        className="rounded-full border border-[#1b4332]/20 px-4 py-2 text-sm font-medium"
                      >
                        Cancel
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="font-medium">{location.name}</p>
                        {location.isPrimary ? (
                          <span className="rounded-full bg-[#40916c]/10 px-2 py-0.5 text-xs font-semibold uppercase tracking-wide text-[#40916c]">
                            Primary
                          </span>
                        ) : null}
                      </div>
                      <p className="mt-1 text-sm text-[#1b4332]/70">{location.addressLine}</p>
                      <p className="text-sm text-[#1b4332]/60">
                        {location.area} · {location.marketId}
                      </p>
                      <a
                        href={`/businesses/${location.slug}`}
                        className="mt-2 inline-block text-sm font-medium text-[#40916c]"
                      >
                        /businesses/{location.slug}
                      </a>
                    </div>
                    <div className="flex shrink-0 flex-wrap gap-2">
                      <button
                        type="button"
                        onClick={() => startEdit(location)}
                        className="rounded-full border border-[#1b4332]/20 px-3 py-1.5 text-sm font-medium"
                      >
                        Edit
                      </button>
                      {locations.length > 1 ? (
                        <button
                          type="button"
                          onClick={() => void handleDeactivate(location.id)}
                          disabled={busy === `deactivate-${location.id}`}
                          className="rounded-full border border-red-200 px-3 py-1.5 text-sm font-medium text-red-700 disabled:opacity-60"
                        >
                          Remove
                        </button>
                      ) : null}
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
