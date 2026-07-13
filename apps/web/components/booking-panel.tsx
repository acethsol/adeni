"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import type { BookingResponse, ServiceOffering } from "@adeni/shared";
import { LoadingPanel } from "@/components/loading-panel";
import { BackLink } from "@/components/ui/back-link";
import { FlowStepProgress } from "@/components/ui/flow-step-progress";
import { LoadingSpinner } from "@/components/ui/loading-spinner";
import { useActionLoading } from "@/contexts/action-loading-context";

type Props = {
  slug: string;
  tenantId: string;
  services: ServiceOffering[];
  bookingEnabled: boolean;
  loginHref: string;
};

type Step = "service" | "slot" | "confirm" | "done";

const BOOKING_STEPS = [
  { id: "service", label: "Service" },
  { id: "slot", label: "Time" },
  { id: "confirm", label: "Confirm" },
] as const;

function formatPrice(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat(undefined, {
      style: "currency",
      currency,
    }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

function formatSlotTime(iso: string) {
  const date = new Date(iso);
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
}

function slotRange(from: Date, days: number) {
  const start = new Date(from);
  start.setHours(0, 0, 0, 0);
  const end = new Date(start);
  end.setDate(end.getDate() + days);
  return { from: start.toISOString(), to: end.toISOString() };
}

export function BookingPanel({
  slug,
  tenantId,
  services,
  bookingEnabled,
  loginHref,
}: Props) {
  const { run } = useActionLoading();
  const [step, setStep] = useState<Step>("service");
  const [selectedService, setSelectedService] = useState<ServiceOffering | null>(null);
  const [slots, setSlots] = useState<{ startAt: string; endAt: string }[]>([]);
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null);
  const [notes, setNotes] = useState("");
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [booking, setBooking] = useState<BookingResponse | null>(null);

  const activeServices = useMemo(
    () => services.filter((service) => service.isActive),
    [services],
  );

  const loadSlots = useCallback(
    async (service: ServiceOffering) => {
      setLoadingSlots(true);
      setError(null);
      setSlots([]);
      setSelectedSlot(null);

      try {
        const range = slotRange(new Date(), 7);
        const query = new URLSearchParams({
          serviceId: service.id,
          from: range.from,
          to: range.to,
        });
        const response = await fetch(
          `/api/businesses/${encodeURIComponent(slug)}/slots?${query.toString()}`,
        );

        if (!response.ok) {
          throw new Error("Could not load available times.");
        }

        const payload = (await response.json()) as { items: { startAt: string; endAt: string }[] };
        setSlots(payload.items ?? []);
        setStep("slot");
      } catch {
        setError("Could not load available times. Try again in a moment.");
      } finally {
        setLoadingSlots(false);
      }
    },
    [slug],
  );

  async function handleSelectService(service: ServiceOffering) {
    setSelectedService(service);
    await loadSlots(service);
  }

  async function handleConfirmBooking() {
    if (!selectedService || !selectedSlot) {
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      await run("Confirming your booking…", async () => {
        const response = await fetch("/api/bookings", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            tenantId,
            serviceOfferingId: selectedService.id,
            startAt: selectedSlot,
            customerNotes: notes.trim() || undefined,
          }),
        });

        const payload = await response.json().catch(() => ({}));

        if (response.status === 401) {
          setError("Sign in to complete your booking.");
          throw new Error("Sign in to complete your booking.");
        }

        if (!response.ok) {
          const message =
            typeof payload.title === "string"
              ? payload.title
              : "Booking failed. That slot may have been taken.";
          throw new Error(message);
        }

        setBooking(payload as BookingResponse);
        setStep("done");
      });
    } catch (err) {
      if (err instanceof Error && err.message !== "Sign in to complete your booking.") {
        setError(err.message);
      } else if (!(err instanceof Error)) {
        setError("Booking failed. Please try again.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (activeServices.length === 0) {
    return (
      <section className="mt-8 rounded-2xl border border-[#1b4332]/10 bg-white p-8 shadow-sm">
        <h2 className="text-lg font-semibold">Book online</h2>
        <p className="mt-2 text-sm text-[#1b4332]/70">
          Online booking is not available yet for this business.
        </p>
      </section>
    );
  }

  if (step === "done" && booking) {
    return (
      <section className="mt-8 rounded-2xl border border-[#40916c]/30 bg-white p-8 shadow-sm">
        <p className="text-sm font-semibold uppercase tracking-widest text-[#40916c]">
          Booking submitted
        </p>
        <h2 className="mt-2 text-xl font-bold">{booking.serviceName}</h2>
        <p className="mt-2 text-[#1b4332]/80">{formatSlotTime(booking.startAt)}</p>
        <p className="mt-4 text-sm text-[#1b4332]/70">
          Status: pending confirmation from the business. You will be notified once they accept.
        </p>
      </section>
    );
  }

  return (
    <section className="mt-8 rounded-2xl border border-[#1b4332]/10 bg-white p-8 shadow-sm">
      <h2 className="text-lg font-semibold">Book online</h2>
      <p className="mt-1 text-sm text-[#1b4332]/70">
        Choose a service and pick an available time.
      </p>

      {step !== "service" ? (
        <FlowStepProgress steps={[...BOOKING_STEPS]} currentStepId={step} className="mt-5" />
      ) : null}

      {!bookingEnabled ? (
        <p className="mt-4 text-sm text-[#1b4332]/70">
          <Link href={loginHref} className="font-medium text-[#40916c] underline">
            Sign in
          </Link>{" "}
          to book an appointment.
        </p>
      ) : null}

      {error ? (
        <p className="mt-4 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-800">{error}</p>
      ) : null}

      {step === "service" && (
        <ul className="mt-6 space-y-3">
          {activeServices.map((service) => (
            <li key={service.id}>
              <button
                type="button"
                onClick={() => void handleSelectService(service)}
                disabled={loadingSlots}
                className="flex w-full items-start justify-between rounded-xl border border-[#1b4332]/10 px-4 py-4 text-left transition hover:border-[#40916c]/40 disabled:opacity-60"
              >
                <div>
                  <p className="font-medium">{service.name}</p>
                  {service.description ? (
                    <p className="mt-1 text-sm text-[#1b4332]/70">{service.description}</p>
                  ) : null}
                  <p className="mt-2 text-xs text-[#1b4332]/60">
                    {service.durationMinutes} min
                  </p>
                </div>
                <p className="ml-4 shrink-0 font-semibold text-[#40916c]">
                  {formatPrice(service.priceAmount, service.currency)}
                </p>
              </button>
            </li>
          ))}
        </ul>
      )}

      {step === "slot" && selectedService && (
        <div className="mt-6">
          <BackLink
            label="Change service"
            hint="Browse all services"
            onClick={() => {
              setStep("service");
              setSelectedService(null);
              setSlots([]);
            }}
          />
          <p className="mt-4 font-medium">{selectedService.name}</p>

          {loadingSlots ? (
            <LoadingPanel message="Loading available times…" variant="card" className="mt-4" />
          ) : slots.length === 0 ? (
            <p className="mt-4 text-sm text-[#1b4332]/70">
              No open slots in the next 7 days. Check back soon.
            </p>
          ) : (
            <ul className="mt-4 grid gap-2 sm:grid-cols-2">
              {slots.map((slot) => (
                <li key={slot.startAt}>
                  <button
                    type="button"
                    onClick={() => {
                      setSelectedSlot(slot.startAt);
                      setStep("confirm");
                    }}
                    className="w-full rounded-lg border border-[#1b4332]/15 px-3 py-2 text-sm font-medium transition hover:border-[#40916c]/40 hover:bg-[#f6f8f6]"
                  >
                    {formatSlotTime(slot.startAt)}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {step === "confirm" && selectedService && selectedSlot && (
        <div className="mt-6">
          <BackLink
            label="Pick another time"
            hint="Available slots"
            onClick={() => setStep("slot")}
          />

          <dl className="mt-4 space-y-2 text-sm">
            <div>
              <dt className="text-[#1b4332]/60">Service</dt>
              <dd className="font-medium">{selectedService.name}</dd>
            </div>
            <div>
              <dt className="text-[#1b4332]/60">Time</dt>
              <dd className="font-medium">{formatSlotTime(selectedSlot)}</dd>
            </div>
            <div>
              <dt className="text-[#1b4332]/60">Price</dt>
              <dd className="font-medium">
                {formatPrice(selectedService.priceAmount, selectedService.currency)}
              </dd>
            </div>
          </dl>

          <label className="mt-4 block text-sm">
            <span className="font-medium text-[#1b4332]/80">Notes (optional)</span>
            <textarea
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
              rows={3}
              maxLength={1000}
              className="mt-1 w-full rounded-lg border border-[#1b4332]/15 px-3 py-2 text-sm"
              placeholder="Anything the business should know?"
            />
          </label>

          {bookingEnabled ? (
            <button
              type="button"
              onClick={() => void handleConfirmBooking()}
              disabled={submitting}
              className="mt-6 inline-flex items-center justify-center gap-2 rounded-full bg-[#1b4332] px-6 py-3 text-sm font-semibold text-white disabled:opacity-60"
            >
              {submitting ? <LoadingSpinner size="sm" label="Booking" className="border-primary-foreground/30 border-t-primary-foreground" /> : null}
              {submitting ? "Booking…" : "Confirm booking"}
            </button>
          ) : (
            <Link
              href={loginHref}
              className="mt-6 inline-block rounded-full bg-[#1b4332] px-6 py-3 text-sm font-semibold text-white"
            >
              Sign in to confirm
            </Link>
          )}
        </div>
      )}
    </section>
  );
}
