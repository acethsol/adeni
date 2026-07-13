"use client";

import { useState } from "react";
import { MapPin } from "lucide-react";
import type { BusinessLocation, MarketConfig } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Input } from "@/components/ui/input";
import { useToast } from "@/contexts/toast-context";
import { useConfirm } from "@/contexts/confirm-context";

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

type DraftErrors = Partial<Record<keyof LocationDraft, string>>;

const EMPTY_DRAFT = (marketId: string): LocationDraft => ({
  slug: "",
  name: "",
  addressLine: "",
  area: "",
  marketId,
  isPrimary: false,
});

const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

function validateDraft(draft: LocationDraft): DraftErrors {
  const errors: DraftErrors = {};

  const slug = draft.slug.trim().toLowerCase();
  if (!slug) {
    errors.slug = "URL slug is required.";
  } else if (!SLUG_PATTERN.test(slug)) {
    errors.slug = "Use lowercase letters, numbers, and hyphens only.";
  }

  if (!draft.addressLine.trim()) {
    errors.addressLine = "Address is required.";
  }

  if (!draft.area.trim()) {
    errors.area = "Area is required.";
  }

  return errors;
}

export function BusinessLocationsManager({
  initialLocations,
  defaultMarketId = "lagos",
  markets,
}: Props) {
  const marketOptions = markets.map((market) => ({
    id: market.id,
    name: market.name,
  }));

  const toast = useToast();
  const confirm = useConfirm();

  const [locations, setLocations] = useState(initialLocations);
  const [draft, setDraft] = useState<LocationDraft>(() => EMPTY_DRAFT(defaultMarketId));
  const [draftErrors, setDraftErrors] = useState<DraftErrors>({});
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<LocationDraft | null>(null);
  const [editErrors, setEditErrors] = useState<DraftErrors>({});
  const [busy, setBusy] = useState<string | null>(null);

  async function handleCreate(event: React.FormEvent) {
    event.preventDefault();

    const errors = validateDraft(draft);
    setDraftErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    setBusy("create");

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
      setDraftErrors({});
      toast.success("Location added", { description: created.name });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not add location.");
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
    setEditErrors({});
  }

  async function handleUpdate(locationId: string) {
    if (!editDraft) {
      return;
    }

    const errors = validateDraft(editDraft);
    setEditErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    setBusy(locationId);

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
      toast.success("Location updated");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not update location.");
    } finally {
      setBusy(null);
    }
  }

  async function handleDeactivate(location: BusinessLocation) {
    const confirmed = await confirm({
      title: `Remove ${location.name}?`,
      description:
        "This location will no longer accept bookings or appear on your public profile. This can't be undone from here.",
      confirmLabel: "Remove location",
      tone: "destructive",
    });
    if (!confirmed) {
      return;
    }

    setBusy(`deactivate-${location.id}`);

    try {
      const response = await fetch(`/api/business/locations/${location.id}`, {
        method: "DELETE",
      });

      if (!response.ok) {
        const payload = await response.json().catch(() => ({}));
        throw new Error(
          typeof payload.title === "string" ? payload.title : "Could not remove location.",
        );
      }

      setLocations((current) => current.filter((item) => item.id !== location.id));
      toast.success("Location removed", { description: location.name });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not remove location.");
    } finally {
      setBusy(null);
    }
  }

  function renderDraftFields(
    value: LocationDraft,
    onChange: (next: LocationDraft) => void,
    errors: DraftErrors,
    slugDisabled = false,
  ) {
    return (
      <div className="grid gap-4 sm:grid-cols-2">
        <Input
          label="URL slug"
          required
          disabled={slugDisabled}
          value={value.slug}
          onChange={(event) => onChange({ ...value, slug: event.target.value })}
          placeholder="lekki-branch"
          error={errors.slug}
        />
        <Input
          label="Display name"
          value={value.name}
          onChange={(event) => onChange({ ...value, name: event.target.value })}
          placeholder="Optional — defaults to area"
        />
        <div className="sm:col-span-2">
          <Input
            label="Address"
            required
            value={value.addressLine}
            onChange={(event) => onChange({ ...value, addressLine: event.target.value })}
            error={errors.addressLine}
          />
        </div>
        <Input
          label="Area"
          required
          value={value.area}
          onChange={(event) => onChange({ ...value, area: event.target.value })}
          error={errors.area}
        />
        <label className="block">
          <span className="text-sm font-semibold text-muted-foreground">Market</span>
          <select
            required
            value={value.marketId}
            onChange={(event) => onChange({ ...value, marketId: event.target.value })}
            className="mt-2 w-full rounded-xl border border-border-strong bg-surface px-4 py-3 text-sm text-foreground outline-none transition-shadow focus:border-accent focus:ring-2 focus:ring-accent/20"
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
          <span className="text-sm text-muted">Set as primary location</span>
        </label>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <form
        onSubmit={(event) => void handleCreate(event)}
        className="rounded-xl border border-border bg-surface p-6 shadow-sm"
      >
        <h2 className="text-lg font-semibold text-foreground">Add location</h2>
        <p className="mt-2 text-sm text-muted">
          Each branch gets its own public URL at <code className="text-xs">/businesses/your-slug</code>.
        </p>
        <div className="mt-4">{renderDraftFields(draft, setDraft, draftErrors)}</div>
        <Button type="submit" loading={busy === "create"} loadingLabel="Adding…" className="mt-4">
          Add location
        </Button>
      </form>

      <section>
        <h2 className="text-lg font-semibold text-foreground">Your locations</h2>
        {locations.length === 0 ? (
          <EmptyState
            className="mt-3"
            icon={<MapPin className="h-6 w-6" aria-hidden />}
            title="No locations yet"
            description="Add your first branch above to start accepting bookings there."
          />
        ) : (
          <ul className="mt-4 space-y-3">
            {locations.map((location) => (
              <li
                key={location.id}
                className="rounded-xl border border-border bg-surface p-5 shadow-sm"
              >
                {editingId === location.id && editDraft ? (
                  <div>
                    {renderDraftFields(editDraft, setEditDraft, editErrors)}
                    <div className="mt-4 flex gap-2">
                      <Button
                        size="sm"
                        onClick={() => void handleUpdate(location.id)}
                        loading={busy === location.id}
                        loadingLabel="Saving…"
                      >
                        Save
                      </Button>
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => {
                          setEditingId(null);
                          setEditDraft(null);
                        }}
                      >
                        Cancel
                      </Button>
                    </div>
                  </div>
                ) : (
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="font-medium text-foreground">{location.name}</p>
                        {location.isPrimary ? (
                          <span className="rounded-full bg-accent/10 px-2 py-0.5 text-xs font-semibold uppercase tracking-wide text-accent">
                            Primary
                          </span>
                        ) : null}
                      </div>
                      <p className="mt-1 text-sm text-muted">{location.addressLine}</p>
                      <p className="text-sm text-muted-foreground">
                        {location.area} · {location.marketId}
                      </p>
                      <a
                        href={`/businesses/${location.slug}`}
                        className="mt-2 inline-block text-sm font-medium text-accent"
                      >
                        /businesses/{location.slug}
                      </a>
                    </div>
                    <div className="flex shrink-0 flex-wrap gap-2">
                      <Button variant="secondary" size="sm" onClick={() => startEdit(location)}>
                        Edit
                      </Button>
                      {locations.length > 1 ? (
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={() => void handleDeactivate(location)}
                          loading={busy === `deactivate-${location.id}`}
                          loadingLabel="Removing…"
                        >
                          Remove
                        </Button>
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
