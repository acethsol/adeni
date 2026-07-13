"use client";

import { useState } from "react";
import type { AdminMarket } from "@adeni/shared";
import { useConfirm } from "@/contexts/confirm-context";
import { useToast } from "@/contexts/toast-context";

const SUPPORTED_LANGUAGES = ["en", "fr", "es", "pt"] as const;

type MarketDraft = {
  id: string;
  name: string;
  countryCode: string;
  currency: string;
  timeZoneId: string;
  defaultLat: string;
  defaultLng: string;
  languages: string[];
  isLive: boolean;
  launchNote: string;
};

function emptyDraft(): MarketDraft {
  return {
    id: "",
    name: "",
    countryCode: "",
    currency: "",
    timeZoneId: "",
    defaultLat: "",
    defaultLng: "",
    languages: ["en"],
    isLive: false,
    launchNote: "",
  };
}

function toDraft(market: AdminMarket): MarketDraft {
  return {
    id: market.id,
    name: market.name,
    countryCode: market.countryCode,
    currency: market.currency,
    timeZoneId: market.timeZoneId,
    defaultLat: String(market.defaultLat),
    defaultLng: String(market.defaultLng),
    languages: [...market.languages],
    isLive: market.isLive,
    launchNote: market.launchNote ?? "",
  };
}

export function AdminMarketsPanel({
  initialItems,
  initialError,
}: {
  initialItems: AdminMarket[];
  initialError: string | null;
}) {
  const confirm = useConfirm();
  const toast = useToast();
  const [markets, setMarkets] = useState(initialItems);
  const [error, setError] = useState<string | null>(initialError);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [createDraft, setCreateDraft] = useState<MarketDraft>(emptyDraft);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<MarketDraft | null>(null);

  async function refreshMarkets() {
    const response = await fetch("/api/admin/markets");
    if (!response.ok) {
      throw new Error("Could not refresh markets.");
    }
    const payload = (await response.json()) as { items: AdminMarket[] };
    setMarkets(payload.items ?? []);
  }

  async function handleToggleLive(market: AdminMarket) {
    if (market.isLive) {
      const confirmed = await confirm({
        title: `Take ${market.name} offline?`,
        description:
          "Customers in this market will no longer see businesses in discovery until it's turned live again.",
        confirmLabel: "Take offline",
        tone: "destructive",
      });
      if (!confirmed) {
        return;
      }
    }

    setBusyId(market.id);
    setError(null);

    try {
      const response = await fetch(`/api/admin/markets/${encodeURIComponent(market.id)}/live`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ isLive: !market.isLive }),
      });
      if (!response.ok) {
        throw new Error("Could not update live status.");
      }
      setMarkets((current) =>
        current.map((item) =>
          item.id === market.id ? { ...item, isLive: !market.isLive } : item,
        ),
      );
      toast.success(`${market.name} is now ${market.isLive ? "off" : "live"}`);
    } catch {
      toast.error("Could not update live status.");
    } finally {
      setBusyId(null);
    }
  }

  async function handleCreate(event: React.FormEvent) {
    event.preventDefault();
    setBusyId("create");
    setError(null);

    try {
      const response = await fetch("/api/admin/markets", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          id: createDraft.id.trim().toLowerCase(),
          name: createDraft.name.trim(),
          countryCode: createDraft.countryCode.trim().toUpperCase(),
          currency: createDraft.currency.trim().toUpperCase(),
          timeZoneId: createDraft.timeZoneId.trim(),
          defaultLat: Number(createDraft.defaultLat),
          defaultLng: Number(createDraft.defaultLng),
          languages: createDraft.languages,
          isLive: createDraft.isLive,
          launchNote: createDraft.launchNote.trim() || null,
        }),
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(
          typeof payload.title === "string" ? payload.title : "Could not create market.",
        );
      }
      await refreshMarkets();
      setShowCreate(false);
      setCreateDraft(emptyDraft());
      toast.success("Market created", { description: createDraft.name });
    } catch (err) {
      const messageText = err instanceof Error ? err.message : "Could not create market.";
      setError(messageText);
      toast.error(messageText);
    } finally {
      setBusyId(null);
    }
  }

  async function handleUpdate(marketId: string) {
    if (!editDraft) {
      return;
    }

    setBusyId(marketId);
    setError(null);

    try {
      const response = await fetch(`/api/admin/markets/${encodeURIComponent(marketId)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: editDraft.name.trim(),
          countryCode: editDraft.countryCode.trim().toUpperCase(),
          currency: editDraft.currency.trim().toUpperCase(),
          timeZoneId: editDraft.timeZoneId.trim(),
          defaultLat: Number(editDraft.defaultLat),
          defaultLng: Number(editDraft.defaultLng),
          languages: editDraft.languages,
          launchNote: editDraft.launchNote.trim() || null,
        }),
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(
          typeof payload.title === "string" ? payload.title : "Could not update market.",
        );
      }
      const updated = payload as AdminMarket;
      setMarkets((current) =>
        current.map((item) => (item.id === marketId ? updated : item)),
      );
      setEditingId(null);
      setEditDraft(null);
      toast.success("Market updated", { description: updated.name });
    } catch (err) {
      const messageText = err instanceof Error ? err.message : "Could not update market.";
      setError(messageText);
      toast.error(messageText);
    } finally {
      setBusyId(null);
    }
  }

  function toggleLanguage(
    draft: MarketDraft,
    setDraft: (value: MarketDraft) => void,
    language: string,
  ) {
    const hasLanguage = draft.languages.includes(language);
    const nextLanguages = hasLanguage
      ? draft.languages.filter((item) => item !== language)
      : [...draft.languages, language];
    setDraft({ ...draft, languages: nextLanguages.length > 0 ? nextLanguages : draft.languages });
  }

  function renderLanguageToggles(draft: MarketDraft, setDraft: (value: MarketDraft) => void) {
    return (
      <div className="flex flex-wrap gap-2">
        {SUPPORTED_LANGUAGES.map((language) => (
          <button
            key={language}
            type="button"
            onClick={() => toggleLanguage(draft, setDraft, language)}
            className={`rounded-full px-3 py-1 text-xs font-semibold ${
              draft.languages.includes(language)
                ? "bg-[#1b4332] text-white"
                : "border border-[#1b4332]/20 text-[#1b4332]/70"
            }`}
          >
            {language}
          </button>
        ))}
      </div>
    );
  }

  return (
    <section className="mt-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">Markets</h2>
          <p className="mt-1 text-sm text-[#1b4332]/70">
            Manage launch markets, locales, and default map centers.
          </p>
        </div>
        <button
          type="button"
          onClick={() => setShowCreate((current) => !current)}
          className="rounded-full bg-[#1b4332] px-4 py-2 text-sm font-medium text-white"
        >
          {showCreate ? "Cancel" : "Add market"}
        </button>
      </div>

      {error ? <p className="mt-4 text-sm text-red-700">{error}</p> : null}

      {showCreate ? (
        <form
          onSubmit={handleCreate}
          className="mt-4 grid gap-3 rounded-xl border border-[#1b4332]/10 bg-white p-4 shadow-sm md:grid-cols-2"
        >
          <input
            className="rounded-lg border px-3 py-2 text-sm"
            placeholder="id (e.g. montreal)"
            value={createDraft.id}
            onChange={(event) => setCreateDraft({ ...createDraft, id: event.target.value })}
            required
          />
          <input
            className="rounded-lg border px-3 py-2 text-sm"
            placeholder="Name"
            value={createDraft.name}
            onChange={(event) => setCreateDraft({ ...createDraft, name: event.target.value })}
            required
          />
          <input
            className="rounded-lg border px-3 py-2 text-sm"
            placeholder="Country code"
            value={createDraft.countryCode}
            onChange={(event) => setCreateDraft({ ...createDraft, countryCode: event.target.value })}
            required
          />
          <input
            className="rounded-lg border px-3 py-2 text-sm"
            placeholder="Currency"
            value={createDraft.currency}
            onChange={(event) => setCreateDraft({ ...createDraft, currency: event.target.value })}
            required
          />
          <input
            className="rounded-lg border px-3 py-2 text-sm md:col-span-2"
            placeholder="Time zone (IANA)"
            value={createDraft.timeZoneId}
            onChange={(event) => setCreateDraft({ ...createDraft, timeZoneId: event.target.value })}
            required
          />
          <input
            className="rounded-lg border px-3 py-2 text-sm"
            placeholder="Default lat"
            value={createDraft.defaultLat}
            onChange={(event) => setCreateDraft({ ...createDraft, defaultLat: event.target.value })}
            required
          />
          <input
            className="rounded-lg border px-3 py-2 text-sm"
            placeholder="Default lng"
            value={createDraft.defaultLng}
            onChange={(event) => setCreateDraft({ ...createDraft, defaultLng: event.target.value })}
            required
          />
          <div className="md:col-span-2">{renderLanguageToggles(createDraft, setCreateDraft)}</div>
          <label className="flex items-center gap-2 text-sm md:col-span-2">
            <input
              type="checkbox"
              checked={createDraft.isLive}
              onChange={(event) => setCreateDraft({ ...createDraft, isLive: event.target.checked })}
            />
            Live at creation
          </label>
          <button
            type="submit"
            disabled={busyId === "create"}
            className="rounded-full bg-[#1b4332] px-4 py-2 text-sm font-medium text-white md:col-span-2"
          >
            {busyId === "create" ? "Creating…" : "Create market"}
          </button>
        </form>
      ) : null}

      <div className="mt-4 overflow-x-auto rounded-xl border border-[#1b4332]/10 bg-white shadow-sm">
        <table className="min-w-full text-left text-sm">
          <thead className="border-b border-[#1b4332]/10 bg-[#f8faf8] text-xs uppercase tracking-wide text-[#1b4332]/60">
            <tr>
              <th className="px-4 py-3">Market</th>
              <th className="px-4 py-3">Country</th>
              <th className="px-4 py-3">Languages</th>
              <th className="px-4 py-3">Live</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {markets.map((market) => {
              const isEditing = editingId === market.id && editDraft;
              return (
                <tr key={market.id} className="border-b border-[#1b4332]/5 align-top">
                  <td className="px-4 py-3">
                    {isEditing ? (
                      <div className="grid gap-2">
                        <input
                          className="rounded border px-2 py-1"
                          value={editDraft.name}
                          onChange={(event) =>
                            setEditDraft({ ...editDraft, name: event.target.value })
                          }
                        />
                        <input
                          className="rounded border px-2 py-1"
                          value={editDraft.timeZoneId}
                          onChange={(event) =>
                            setEditDraft({ ...editDraft, timeZoneId: event.target.value })
                          }
                        />
                      </div>
                    ) : (
                      <div>
                        <p className="font-medium">{market.name}</p>
                        <p className="text-xs text-[#1b4332]/60">{market.id}</p>
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    {isEditing ? (
                      <div className="grid gap-2">
                        <input
                          className="rounded border px-2 py-1"
                          value={editDraft.countryCode}
                          onChange={(event) =>
                            setEditDraft({ ...editDraft, countryCode: event.target.value })
                          }
                        />
                        <input
                          className="rounded border px-2 py-1"
                          value={editDraft.currency}
                          onChange={(event) =>
                            setEditDraft({ ...editDraft, currency: event.target.value })
                          }
                        />
                      </div>
                    ) : (
                      <span>
                        {market.countryCode} · {market.currency}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    {isEditing ? (
                      renderLanguageToggles(editDraft, setEditDraft)
                    ) : (
                      market.languages.join(", ")
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <button
                      type="button"
                      disabled={busyId === market.id}
                      onClick={() => void handleToggleLive(market)}
                      className={`rounded-full px-3 py-1 text-xs font-semibold ${
                        market.isLive
                          ? "bg-emerald-100 text-emerald-800"
                          : "bg-slate-100 text-slate-600"
                      }`}
                    >
                      {market.isLive ? "Live" : "Off"}
                    </button>
                  </td>
                  <td className="px-4 py-3">
                    {isEditing ? (
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          onClick={() => void handleUpdate(market.id)}
                          disabled={busyId === market.id}
                          className="rounded-full bg-[#1b4332] px-3 py-1 text-xs font-medium text-white"
                        >
                          Save
                        </button>
                        <button
                          type="button"
                          onClick={() => {
                            setEditingId(null);
                            setEditDraft(null);
                          }}
                          className="rounded-full border px-3 py-1 text-xs"
                        >
                          Cancel
                        </button>
                      </div>
                    ) : (
                      <button
                        type="button"
                        onClick={() => {
                          setEditingId(market.id);
                          setEditDraft(toDraft(market));
                        }}
                        className="rounded-full border px-3 py-1 text-xs font-medium"
                      >
                        Edit
                      </button>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}
