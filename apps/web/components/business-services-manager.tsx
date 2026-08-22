"use client";

import { useState } from "react";
import { Clock3, Pencil, PlusCircle, Sparkles, Tag, Trash2 } from "lucide-react";
import type { ServiceOffering } from "@adeni/shared";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Input, Textarea } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { useConfirm } from "@/contexts/confirm-context";
import { useToast } from "@/contexts/toast-context";

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

type DraftErrors = Partial<Record<keyof ServiceDraft, string>>;

const EMPTY_DRAFT = (currency: string): ServiceDraft => ({
  name: "",
  description: "",
  priceAmount: "",
  currency,
  durationMinutes: "30",
});

function validateDraft(draft: ServiceDraft): DraftErrors {
  const errors: DraftErrors = {};

  if (!draft.name.trim()) {
    errors.name = "Service name is required.";
  }

  const price = Number(draft.priceAmount);
  if (!draft.priceAmount.trim() || Number.isNaN(price) || price < 0) {
    errors.priceAmount = "Enter a valid price.";
  }

  if (!draft.currency.trim() || draft.currency.trim().length !== 3) {
    errors.currency = "Use a 3-letter currency code.";
  }

  const duration = Number(draft.durationMinutes);
  if (!draft.durationMinutes.trim() || Number.isNaN(duration) || duration <= 0) {
    errors.durationMinutes = "Enter a valid duration.";
  }

  return errors;
}

function formatPrice(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

export function BusinessServicesManager({ initialServices, defaultCurrency = "NGN" }: Props) {
  const toast = useToast();
  const confirm = useConfirm();

  const [services, setServices] = useState(initialServices);
  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState<ServiceDraft>(() => EMPTY_DRAFT(defaultCurrency));
  const [originalDraft, setOriginalDraft] = useState<ServiceDraft | null>(null);
  const [draftErrors, setDraftErrors] = useState<DraftErrors>({});
  const [busy, setBusy] = useState<string | null>(null);

  const isUnchanged =
    editingId !== null &&
    originalDraft !== null &&
    Object.keys(draft).every((key) => draft[key as keyof ServiceDraft] === originalDraft[key as keyof ServiceDraft]);

  function openCreateModal() {
    setEditingId(null);
    setDraft(EMPTY_DRAFT(defaultCurrency));
    setOriginalDraft(null);
    setDraftErrors({});
    setModalOpen(true);
  }

  function openEditModal(service: ServiceOffering) {
    const snapshot: ServiceDraft = {
      name: service.name,
      description: service.description ?? "",
      priceAmount: String(service.priceAmount),
      currency: service.currency,
      durationMinutes: String(service.durationMinutes),
    };
    setEditingId(service.id);
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

    const body = {
      name: draft.name.trim(),
      description: draft.description.trim() || null,
      priceAmount: Number(draft.priceAmount),
      currency: draft.currency.trim().toUpperCase(),
      durationMinutes: Number(draft.durationMinutes),
    };

    setBusy("submit");

    try {
      if (editingId) {
        const existing = services.find((item) => item.id === editingId);
        const response = await fetch(`/api/business/services/${editingId}`, {
          method: "PATCH",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ ...body, isActive: existing?.isActive ?? true }),
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(typeof payload.title === "string" ? payload.title : "Could not update service.");
        }

        setServices((current) => current.map((item) => (item.id === editingId ? (payload as ServiceOffering) : item)));
        toast.success("Service updated");
      } else {
        const response = await fetch("/api/business/services", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body),
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(typeof payload.title === "string" ? payload.title : "Could not create service.");
        }

        setServices((current) => [...current, payload as ServiceOffering]);
        toast.success("Service added", { description: body.name });
      }

      closeModal();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save service.");
    } finally {
      setBusy(null);
    }
  }

  async function handleDeactivate(service: ServiceOffering) {
    const confirmed = await confirm({
      title: `Deactivate ${service.name}?`,
      description: "Customers won't be able to book this service anymore. You can't undo this from here.",
      confirmLabel: "Deactivate",
      tone: "destructive",
    });
    if (!confirmed) {
      return;
    }

    setBusy(`deactivate-${service.id}`);

    try {
      const response = await fetch(`/api/business/services/${service.id}`, { method: "DELETE" });

      if (!response.ok) {
        const payload = await response.json().catch(() => ({}));
        throw new Error(typeof payload.title === "string" ? payload.title : "Could not deactivate service.");
      }

      setServices((current) =>
        current.map((item) => (item.id === service.id ? { ...item, isActive: false } : item)),
      );
      toast.success("Service deactivated", { description: service.name });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not deactivate service.");
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-lg font-semibold text-foreground">Your services</h2>
        <Button onClick={openCreateModal} size="sm" className="gap-1.5">
          <PlusCircle className="h-4 w-4" aria-hidden />
          Add service
        </Button>
      </div>

      {services.length === 0 ? (
        <EmptyState
          icon={<Sparkles className="h-6 w-6" aria-hidden />}
          title="No services yet"
          description="Add your first service so customers can start booking."
          actionLabel="Add service"
          onAction={openCreateModal}
        />
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {services.map((service) => (
            <li
              key={service.id}
              className="flex flex-col justify-between gap-4 rounded-2xl border border-border bg-surface p-5 shadow-sm transition-all hover:border-accent/30 hover:shadow-md"
            >
              <div>
                <div className="flex items-start justify-between gap-2">
                  <p className="font-semibold text-foreground">{service.name}</p>
                  <Badge tone={service.isActive ? "success" : "default"}>
                    {service.isActive ? "Active" : "Inactive"}
                  </Badge>
                </div>
                <div className="mt-2 flex flex-wrap items-center gap-3 text-sm text-muted">
                  <span className="inline-flex items-center gap-1.5">
                    <Clock3 className="h-3.5 w-3.5" aria-hidden />
                    {service.durationMinutes} min
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <Tag className="h-3.5 w-3.5" aria-hidden />
                    {formatPrice(service.priceAmount, service.currency)}
                  </span>
                </div>
                {service.description ? (
                  <p className="mt-3 text-sm leading-relaxed text-muted-foreground">{service.description}</p>
                ) : null}
              </div>

              {service.isActive ? (
                <div className="flex gap-2">
                  <Button variant="secondary" size="sm" onClick={() => openEditModal(service)} className="gap-1.5">
                    <Pencil className="h-3.5 w-3.5" aria-hidden />
                    Edit
                  </Button>
                  <Button
                    variant="destructive"
                    size="sm"
                    onClick={() => void handleDeactivate(service)}
                    loading={busy === `deactivate-${service.id}`}
                    loadingLabel="Deactivating…"
                    className="gap-1.5"
                  >
                    <Trash2 className="h-3.5 w-3.5" aria-hidden />
                    Deactivate
                  </Button>
                </div>
              ) : null}
            </li>
          ))}
        </ul>
      )}

      <Modal
        open={modalOpen}
        onClose={closeModal}
        title={editingId ? "Edit service" : "Add service"}
        description="Set what customers can book and how much it costs."
        footer={
          <>
            <Button variant="secondary" onClick={closeModal}>
              Cancel
            </Button>
            <Button
              type="submit"
              form="service-draft-form"
              loading={busy === "submit"}
              loadingLabel={editingId ? "Saving…" : "Adding…"}
              disabled={isUnchanged}
            >
              {editingId ? "Save changes" : "Add service"}
            </Button>
          </>
        }
      >
        <form id="service-draft-form" onSubmit={(event) => void handleSubmit(event)} className="space-y-4">
          <Input
            label="Name"
            required
            autoFocus
            value={draft.name}
            onChange={(event) => setDraft({ ...draft, name: event.target.value })}
            placeholder="Classic haircut"
            error={draftErrors.name}
          />
          <Textarea
            label="Description"
            value={draft.description}
            onChange={(event) => setDraft({ ...draft, description: event.target.value })}
            rows={2}
            placeholder="Optional details customers see before booking"
          />
          <div className="grid gap-4 sm:grid-cols-3">
            <Input
              label="Price"
              required
              type="number"
              min="0"
              step="0.01"
              value={draft.priceAmount}
              onChange={(event) => setDraft({ ...draft, priceAmount: event.target.value })}
              error={draftErrors.priceAmount}
            />
            <Input
              label="Currency"
              required
              maxLength={3}
              className="uppercase"
              value={draft.currency}
              onChange={(event) => setDraft({ ...draft, currency: event.target.value })}
              error={draftErrors.currency}
            />
            <Input
              label="Duration (min)"
              required
              type="number"
              min="1"
              value={draft.durationMinutes}
              onChange={(event) => setDraft({ ...draft, durationMinutes: event.target.value })}
              error={draftErrors.durationMinutes}
            />
          </div>
        </form>
      </Modal>
    </div>
  );
}
