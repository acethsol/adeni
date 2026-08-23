"use client";

import { useState } from "react";
import { MapPin, Pencil, PlusCircle, Star, Trash2 } from "lucide-react";
import type { BusinessLocation, MarketConfig } from "@adeni/shared";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { useToast } from "@/contexts/toast-context";
import { useConfirm } from "@/contexts/confirm-context";
import { useApiErrorMessage } from "@/lib/api-error";

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
  const { formatApiError } = useApiErrorMessage();

  const [locations, setLocations] = useState(initialLocations);
  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState<LocationDraft>(() => EMPTY_DRAFT(defaultMarketId));
  const [originalDraft, setOriginalDraft] = useState<LocationDraft | null>(null);
  const [draftErrors, setDraftErrors] = useState<DraftErrors>({});
  const [busy, setBusy] = useState<string | null>(null);

  const isUnchanged =
    editingId !== null &&
    originalDraft !== null &&
    Object.keys(draft).every(
      (key) => draft[key as keyof LocationDraft] === originalDraft[key as keyof LocationDraft],
    );

  function openCreateModal() {
    setEditingId(null);
    setDraft(EMPTY_DRAFT(defaultMarketId));
    setOriginalDraft(null);
    setDraftErrors({});
    setModalOpen(true);
  }

  function openEditModal(location: BusinessLocation) {
    const snapshot: LocationDraft = {
      slug: location.slug,
      name: location.name,
      addressLine: location.addressLine,
      area: location.area,
      marketId: location.marketId,
      isPrimary: location.isPrimary,
    };
    setEditingId(location.id);
    setDraft(snapshot);
    setOriginalDraft(snapshot);
    setDraftErrors({});
    setModalOpen(true);
  }

  function closeModal() {
    setModalOpen(false);
    setEditingId(null);
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();

    if (isUnchanged) {
      return;
    }

    const errors = validateDraft(draft);
    setDraftErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    setBusy("submit");

    try {
      if (editingId) {
        const response = await fetch(`/api/business/locations/${editingId}`, {
          method: "PATCH",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            slug: draft.slug.trim().toLowerCase(),
            name: draft.name.trim() || undefined,
            addressLine: draft.addressLine.trim(),
            area: draft.area.trim(),
            marketId: draft.marketId,
            isPrimary: draft.isPrimary,
          }),
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(formatApiError(payload, "Could not update location."));
        }

        const updated = payload as BusinessLocation;
        setLocations((current) =>
          current
            .map((item) => {
              if (item.id === editingId) {
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
        toast.success("Location updated");
      } else {
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
          throw new Error(formatApiError(payload, "Could not add location."));
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
        toast.success("Location added", { description: created.name });
      }

      closeModal();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save location.");
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
          formatApiError(payload, "Could not remove location."),
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

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-lg font-semibold text-foreground">Your locations</h2>
        <Button onClick={openCreateModal} size="sm" className="gap-1.5">
          <PlusCircle className="h-4 w-4" aria-hidden />
          Add location
        </Button>
      </div>

      {locations.length === 0 ? (
        <EmptyState
          icon={<MapPin className="h-6 w-6" aria-hidden />}
          title="No locations yet"
          description="Add your first branch to start accepting bookings there."
          actionLabel="Add location"
          onAction={openCreateModal}
        />
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {locations.map((location) => (
            <li
              key={location.id}
              className="flex flex-col justify-between gap-4 rounded-2xl border border-border bg-surface p-5 shadow-sm transition-all hover:border-accent/30 hover:shadow-md"
            >
              <div>
                <div className="flex flex-wrap items-center gap-2">
                  <p className="font-semibold text-foreground">{location.name}</p>
                  {location.isPrimary ? (
                    <Badge tone="accent" className="gap-1">
                      <Star className="h-3 w-3" aria-hidden />
                      Primary
                    </Badge>
                  ) : null}
                </div>
                <p className="mt-2 text-sm text-muted">{location.addressLine}</p>
                <p className="text-sm text-muted-foreground">
                  {location.area} · {location.marketId}
                </p>
                <a
                  href={`/businesses/${location.slug}`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-3 inline-block text-sm font-medium text-accent hover:underline"
                >
                  /businesses/{location.slug}
                </a>
              </div>

              <div className="flex gap-2">
                <Button variant="secondary" size="sm" onClick={() => openEditModal(location)} className="gap-1.5">
                  <Pencil className="h-3.5 w-3.5" aria-hidden />
                  Edit
                </Button>
                {locations.length > 1 ? (
                  <Button
                    variant="destructive"
                    size="sm"
                    onClick={() => void handleDeactivate(location)}
                    loading={busy === `deactivate-${location.id}`}
                    loadingLabel="Removing…"
                    className="gap-1.5"
                  >
                    <Trash2 className="h-3.5 w-3.5" aria-hidden />
                    Remove
                  </Button>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
      )}

      <Modal
        open={modalOpen}
        onClose={closeModal}
        title={editingId ? "Edit location" : "Add location"}
        description="Each branch gets its own public URL at /businesses/your-slug."
        footer={
          <>
            <Button variant="secondary" onClick={closeModal}>
              Cancel
            </Button>
            <Button
              type="submit"
              form="location-draft-form"
              loading={busy === "submit"}
              loadingLabel={editingId ? "Saving…" : "Adding…"}
              disabled={isUnchanged}
            >
              {editingId ? "Save changes" : "Add location"}
            </Button>
          </>
        }
      >
        <form id="location-draft-form" onSubmit={(event) => void handleSubmit(event)} className="grid gap-4 sm:grid-cols-2">
          <Input
            label="URL slug"
            required
            autoFocus
            value={draft.slug}
            onChange={(event) => setDraft({ ...draft, slug: event.target.value })}
            placeholder="lekki-branch"
            error={draftErrors.slug}
          />
          <Input
            label="Display name"
            value={draft.name}
            onChange={(event) => setDraft({ ...draft, name: event.target.value })}
            placeholder="Optional — defaults to area"
          />
          <div className="sm:col-span-2">
            <Input
              label="Address"
              required
              value={draft.addressLine}
              onChange={(event) => setDraft({ ...draft, addressLine: event.target.value })}
              error={draftErrors.addressLine}
            />
          </div>
          <Input
            label="Area"
            required
            value={draft.area}
            onChange={(event) => setDraft({ ...draft, area: event.target.value })}
            error={draftErrors.area}
          />
          <label className="block">
            <span className="text-sm font-semibold text-muted-foreground">Market</span>
            <select
              required
              value={draft.marketId}
              onChange={(event) => setDraft({ ...draft, marketId: event.target.value })}
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
              checked={draft.isPrimary}
              onChange={(event) => setDraft({ ...draft, isPrimary: event.target.checked })}
            />
            <span className="text-sm text-muted">Set as primary location</span>
          </label>
        </form>
      </Modal>
    </div>
  );
}
