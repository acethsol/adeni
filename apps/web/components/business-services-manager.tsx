"use client";

import { useState } from "react";
import { Sparkles } from "lucide-react";
import type { ServiceOffering } from "@adeni/shared";
import { EmptyState } from "@/components/ui/empty-state";

type Props = {
  initialServices: ServiceOffering[];
  defaultCurrency?: string;
};

type ServiceDraft = {
  name: string;
  description: string;
  priceAmount: string;
  currency: string;
  durationMinutes: string;
};

const EMPTY_DRAFT = (currency: string): ServiceDraft => ({
  name: "",
  description: "",
  priceAmount: "",
  currency,
  durationMinutes: "30",
});

function formatPrice(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

export function BusinessServicesManager({
  initialServices,
  defaultCurrency = "NGN",
}: Props) {
  const [services, setServices] = useState(initialServices);
  const [draft, setDraft] = useState<ServiceDraft>(() => EMPTY_DRAFT(defaultCurrency));
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<ServiceDraft | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function handleCreate(event: React.FormEvent) {
    event.preventDefault();
    setBusy("create");
    setError(null);
    setMessage(null);

    try {
      const response = await fetch("/api/business/services", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: draft.name.trim(),
          description: draft.description.trim() || null,
          priceAmount: Number(draft.priceAmount),
          currency: draft.currency.trim().toUpperCase(),
          durationMinutes: Number(draft.durationMinutes),
        }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(typeof payload.title === "string" ? payload.title : "Could not create service.");
      }

      setServices((current) => [...current, payload as ServiceOffering]);
      setDraft(EMPTY_DRAFT(defaultCurrency));
      setMessage("Service added.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create service.");
    } finally {
      setBusy(null);
    }
  }

  function startEdit(service: ServiceOffering) {
    setEditingId(service.id);
    setEditDraft({
      name: service.name,
      description: service.description ?? "",
      priceAmount: String(service.priceAmount),
      currency: service.currency,
      durationMinutes: String(service.durationMinutes),
    });
    setError(null);
    setMessage(null);
  }

  async function handleUpdate(serviceId: string) {
    if (!editDraft) {
      return;
    }

    setBusy(serviceId);
    setError(null);
    setMessage(null);

    try {
      const existing = services.find((item) => item.id === serviceId);
      if (!existing) {
        return;
      }

      const response = await fetch(`/api/business/services/${serviceId}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: editDraft.name.trim(),
          description: editDraft.description.trim() || null,
          priceAmount: Number(editDraft.priceAmount),
          currency: editDraft.currency.trim().toUpperCase(),
          durationMinutes: Number(editDraft.durationMinutes),
          isActive: existing.isActive,
        }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(typeof payload.title === "string" ? payload.title : "Could not update service.");
      }

      setServices((current) =>
        current.map((item) => (item.id === serviceId ? (payload as ServiceOffering) : item)),
      );
      setEditingId(null);
      setEditDraft(null);
      setMessage("Service updated.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not update service.");
    } finally {
      setBusy(null);
    }
  }

  async function handleDeactivate(serviceId: string) {
    setBusy(`deactivate-${serviceId}`);
    setError(null);
    setMessage(null);

    try {
      const response = await fetch(`/api/business/services/${serviceId}`, {
        method: "DELETE",
      });

      if (!response.ok) {
        const payload = await response.json().catch(() => ({}));
        throw new Error(
          typeof payload.title === "string" ? payload.title : "Could not deactivate service.",
        );
      }

      setServices((current) =>
        current.map((item) =>
          item.id === serviceId ? { ...item, isActive: false } : item,
        ),
      );
      setMessage("Service deactivated.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not deactivate service.");
    } finally {
      setBusy(null);
    }
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
        <h2 className="text-lg font-semibold">Add service</h2>
        <div className="mt-4 grid gap-4 sm:grid-cols-2">
          <label className="block sm:col-span-2">
            <span className="text-sm font-medium text-[#1b4332]/70">Name</span>
            <input
              required
              value={draft.name}
              onChange={(event) => setDraft({ ...draft, name: event.target.value })}
              className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
            />
          </label>
          <label className="block sm:col-span-2">
            <span className="text-sm font-medium text-[#1b4332]/70">Description</span>
            <textarea
              value={draft.description}
              onChange={(event) => setDraft({ ...draft, description: event.target.value })}
              rows={2}
              className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
            />
          </label>
          <label className="block">
            <span className="text-sm font-medium text-[#1b4332]/70">Price</span>
            <input
              required
              type="number"
              min="0"
              step="0.01"
              value={draft.priceAmount}
              onChange={(event) => setDraft({ ...draft, priceAmount: event.target.value })}
              className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
            />
          </label>
          <label className="block">
            <span className="text-sm font-medium text-[#1b4332]/70">Currency</span>
            <input
              required
              maxLength={3}
              value={draft.currency}
              onChange={(event) => setDraft({ ...draft, currency: event.target.value })}
              className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2 uppercase"
            />
          </label>
          <label className="block">
            <span className="text-sm font-medium text-[#1b4332]/70">Duration (minutes)</span>
            <input
              required
              type="number"
              min="1"
              value={draft.durationMinutes}
              onChange={(event) => setDraft({ ...draft, durationMinutes: event.target.value })}
              className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
            />
          </label>
        </div>
        <button
          type="submit"
          disabled={busy === "create"}
          className="mt-4 rounded-full bg-[#1b4332] px-5 py-2.5 text-sm font-medium text-white disabled:opacity-60"
        >
          {busy === "create" ? "Adding…" : "Add service"}
        </button>
      </form>

      <section>
        <h2 className="text-lg font-semibold">Your services</h2>
        {services.length === 0 ? (
          <EmptyState
            className="mt-3"
            icon={<Sparkles className="h-6 w-6" aria-hidden />}
            title="No services yet"
            description="Add your first service above so customers can start booking."
          />
        ) : (
          <ul className="mt-4 space-y-3">
            {services.map((service) => (
              <li
                key={service.id}
                className="rounded-xl border border-[#1b4332]/10 bg-white p-5 shadow-sm"
              >
                {editingId === service.id && editDraft ? (
                  <div className="grid gap-3 sm:grid-cols-2">
                    <label className="block sm:col-span-2">
                      <span className="text-sm font-medium text-[#1b4332]/70">Name</span>
                      <input
                        value={editDraft.name}
                        onChange={(event) =>
                          setEditDraft({ ...editDraft, name: event.target.value })
                        }
                        className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
                      />
                    </label>
                    <label className="block">
                      <span className="text-sm font-medium text-[#1b4332]/70">Price</span>
                      <input
                        type="number"
                        min="0"
                        step="0.01"
                        value={editDraft.priceAmount}
                        onChange={(event) =>
                          setEditDraft({ ...editDraft, priceAmount: event.target.value })
                        }
                        className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
                      />
                    </label>
                    <label className="block">
                      <span className="text-sm font-medium text-[#1b4332]/70">Duration (min)</span>
                      <input
                        type="number"
                        min="1"
                        value={editDraft.durationMinutes}
                        onChange={(event) =>
                          setEditDraft({ ...editDraft, durationMinutes: event.target.value })
                        }
                        className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
                      />
                    </label>
                    <div className="flex gap-2 sm:col-span-2">
                      <button
                        type="button"
                        onClick={() => void handleUpdate(service.id)}
                        disabled={busy === service.id}
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
                      <p className="font-medium">{service.name}</p>
                      <p className="text-sm text-[#1b4332]/60">
                        {service.durationMinutes} min ·{" "}
                        {formatPrice(service.priceAmount, service.currency)}
                      </p>
                      {service.description ? (
                        <p className="mt-1 text-sm text-[#1b4332]/70">{service.description}</p>
                      ) : null}
                    </div>
                    <div className="flex shrink-0 items-center gap-2">
                      <span
                        className={`text-xs font-semibold uppercase tracking-wide ${
                          service.isActive ? "text-[#40916c]" : "text-[#1b4332]/40"
                        }`}
                      >
                        {service.isActive ? "Active" : "Inactive"}
                      </span>
                      {service.isActive ? (
                        <>
                          <button
                            type="button"
                            onClick={() => startEdit(service)}
                            className="rounded-full border border-[#1b4332]/20 px-3 py-1.5 text-sm font-medium"
                          >
                            Edit
                          </button>
                          <button
                            type="button"
                            onClick={() => void handleDeactivate(service.id)}
                            disabled={busy === `deactivate-${service.id}`}
                            className="rounded-full border border-red-200 px-3 py-1.5 text-sm font-medium text-red-700 disabled:opacity-60"
                          >
                            Deactivate
                          </button>
                        </>
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
