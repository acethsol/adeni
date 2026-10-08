import { CurrencyPipe } from "@angular/common";
import { Component, inject, input, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { BookingResponse, ServiceOffering } from "@adeni/shared";
import { AdeniApiError } from "@adeni/api-client";
import { CustomerApiService } from "../core/services/customer-api.service";

type Step = "service" | "slot" | "confirm" | "done";

@Component({
  selector: "app-booking-panel",
  standalone: true,
  imports: [FormsModule, CurrencyPipe],
  templateUrl: "./booking-panel.component.html",
  styleUrl: "./booking-panel.component.scss",
})
export class BookingPanelComponent {
  private readonly api = inject(CustomerApiService);

  readonly slug = input.required<string>();
  readonly tenantId = input.required<string>();
  readonly services = input.required<ServiceOffering[]>();
  readonly bookingEnabled = input.required<boolean>();
  readonly supportsDeposits = input(false);
  readonly depositPercent = input(0);

  readonly step = signal<Step>("service");
  readonly selectedService = signal<ServiceOffering | null>(null);
  readonly slots = signal<{ startAt: string; endAt: string }[]>([]);
  readonly selectedSlot = signal<string | null>(null);
  readonly notes = signal("");
  readonly loadingSlots = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly booking = signal<BookingResponse | null>(null);

  activeServices(): ServiceOffering[] {
    const items = this.services();
    const active = items.filter((s) => s.isActive !== false);
    return active.length > 0 ? active : items;
  }

  formatSlot(iso: string): string {
    return new Intl.DateTimeFormat(undefined, {
      weekday: "short",
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
    }).format(new Date(iso));
  }

  async selectService(service: ServiceOffering): Promise<void> {
    this.selectedService.set(service);
    await this.loadSlots(service);
  }

  async loadSlots(service: ServiceOffering): Promise<void> {
    this.loadingSlots.set(true);
    this.error.set(null);
    this.slots.set([]);
    this.selectedSlot.set(null);

    const start = new Date();
    start.setHours(0, 0, 0, 0);
    const end = new Date(start);
    end.setDate(end.getDate() + 7);

    try {
      const items = await this.api.createPublicClient().getBusinessSlots(this.slug(), {
        serviceId: service.id,
        from: start.toISOString(),
        to: end.toISOString(),
      });
      const fresh = items.filter((slot) => new Date(slot.startAt).getTime() > Date.now());
      this.slots.set(fresh);
      this.step.set("slot");
    } catch {
      this.error.set("Could not load available times. Try again in a moment.");
    } finally {
      this.loadingSlots.set(false);
    }
  }

  async joinWaitlist(): Promise<void> {
    const service = this.selectedService();
    if (!service || !this.bookingEnabled()) {
      return;
    }

    try {
      await this.api.withAuthorizedClient((c) =>
        c.joinWaitlist({
          tenantId: this.tenantId(),
          serviceOfferingId: service.id,
        }),
      );
      this.error.set(null);
      alert("You're on the waitlist. We'll notify you when a slot opens.");
    } catch {
      this.error.set("Could not join waitlist. Try again.");
    }
  }

  async confirmBooking(): Promise<void> {
    const service = this.selectedService();
    const slot = this.selectedSlot();
    if (!service || !slot || !this.bookingEnabled()) {
      return;
    }

    if (new Date(slot).getTime() <= Date.now()) {
      this.error.set("That time slot has passed. Please choose a new time.");
      this.step.set("slot");
      this.selectedSlot.set(null);
      await this.loadSlots(service);
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    try {
      const created = await this.api.withAuthorizedClient((c) =>
        c.createBooking({
          tenantId: this.tenantId(),
          serviceOfferingId: service.id,
          startAt: slot,
          customerNotes: this.notes().trim() || undefined,
        }),
      );
      this.booking.set(created);

      const needsDeposit =
        this.supportsDeposits() &&
        this.depositPercent() > 0 &&
        service.priceAmount > 0;

      if (needsDeposit) {
        const payment = await this.api.withAuthorizedClient((c) =>
          c.initializePayment({
            tenantId: this.tenantId(),
            bookingId: created.id,
            currency: service.currency,
            type: "deposit",
          }),
        );
        const checkoutUrl = payment.checkoutUrl;
        if (checkoutUrl.startsWith("http")) {
          window.location.href = checkoutUrl;
        } else {
          window.location.href = checkoutUrl.startsWith("/")
            ? checkoutUrl
            : `/checkout/stub/${payment.providerReference}`;
        }
        return;
      }

      this.step.set("done");
    } catch (err) {
      if (err instanceof AdeniApiError) {
        this.error.set(err.message);
      } else if (err instanceof Error && err.message.includes("requires Auth0")) {
        this.error.set("Sign in to complete your booking.");
      } else {
        this.error.set("Booking failed. That slot may have been taken.");
      }
    } finally {
      this.submitting.set(false);
    }
  }

  signIn(): void {
    void this.api.login(window.location.pathname);
  }

  backToServices(): void {
    this.step.set("service");
    this.selectedService.set(null);
    this.slots.set([]);
  }
}
