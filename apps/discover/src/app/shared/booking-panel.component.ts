import { CurrencyPipe } from "@angular/common";
import { Component, computed, inject, input, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { BookingResponse, ServiceMenuGroup, ServiceOffering } from "@adeni/shared";
import { t } from "@adeni/shared";
import { AdeniApiError } from "@adeni/api-client";
import { AdeniFeedbackService, AdeniLocaleService } from "@adeni/ui";
import { CustomerApiService } from "../core/services/customer-api.service";

type Step = "service" | "slot" | "confirm" | "done";

type SlotDayGroup = {
  key: string;
  label: string;
  slots: { startAt: string; endAt: string }[];
};

type ServiceCollection = {
  id: string;
  name: string;
  services: ServiceOffering[];
};

@Component({
  selector: "app-booking-panel",
  standalone: true,
  imports: [FormsModule, CurrencyPipe],
  templateUrl: "./booking-panel.component.html",
  styleUrl: "./booking-panel.component.scss",
})
export class BookingPanelComponent {
  private readonly api = inject(CustomerApiService);
  private readonly localeService = inject(AdeniLocaleService);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly slug = input.required<string>();
  readonly tenantId = input.required<string>();
  readonly services = input.required<ServiceOffering[]>();
  readonly menuGroups = input<ServiceMenuGroup[]>([]);
  readonly bookingEnabled = input.required<boolean>();
  readonly supportsDeposits = input(false);
  readonly depositPercent = input(0);
  readonly requirePolicyAcceptance = input(false);
  readonly hasPolicies = input(false);

  readonly locale = this.localeService.locale;
  readonly step = signal<Step>("service");
  readonly selectedService = signal<ServiceOffering | null>(null);
  readonly slots = signal<{ startAt: string; endAt: string }[]>([]);
  readonly selectedSlot = signal<string | null>(null);
  readonly notes = signal("");
  readonly serviceSearch = signal("");
  readonly policiesAccepted = signal(false);
  readonly loadingSlots = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly booking = signal<BookingResponse | null>(null);

  readonly stepIndex = computed(() => {
    switch (this.step()) {
      case "service":
        return 0;
      case "slot":
        return 1;
      case "confirm":
      case "done":
        return 2;
    }
  });

  readonly stepEyebrow = computed(() => {
    this.locale();
    switch (this.step()) {
      case "service":
        return this.label("business.booking.stepService");
      case "slot":
        return this.label("business.booking.stepTime");
      case "confirm":
        return this.label("business.booking.stepConfirm");
      default:
        return this.label("business.booking.eyebrow");
    }
  });

  readonly stepTitle = computed(() => {
    this.locale();
    switch (this.step()) {
      case "service":
        return this.label("business.booking.titleService");
      case "slot":
        return this.label("business.booking.titleTime");
      case "confirm":
        return this.label("business.booking.titleConfirm");
      default:
        return this.label("business.booking.bookOnline");
    }
  });

  readonly stepLede = computed(() => {
    this.locale();
    if (this.step() === "service") {
      return this.label("business.booking.ledeServices", {
        count: this.activeServices().length,
      });
    }
    if (this.step() === "slot") {
      const svc = this.selectedService();
      return svc
        ? this.label("business.booking.ledeService", {
            name: svc.name,
            minutes: svc.durationMinutes,
          })
        : null;
    }
    return null;
  });

  readonly slotDays = computed((): SlotDayGroup[] => {
    const locale = this.locale();
    const groups = new Map<string, SlotDayGroup>();
    for (const slot of this.slots()) {
      const date = new Date(slot.startAt);
      const key = `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
      let group = groups.get(key);
      if (!group) {
        group = {
          key,
          label: new Intl.DateTimeFormat(locale, {
            weekday: "short",
            month: "short",
            day: "numeric",
          }).format(date),
          slots: [],
        };
        groups.set(key, group);
      }
      group.slots.push(slot);
    }
    return [...groups.values()];
  });

  label(key: string, vars?: Record<string, string | number>): string {
    return t(this.locale(), key, vars);
  }

  readonly allActiveServices = computed(() => {
    const items = this.services();
    const active = items.filter((s) => s.isActive !== false);
    return active.length > 0 ? active : items;
  });

  readonly filteredServices = computed(() => {
    const base = this.allActiveServices();
    const q = this.serviceSearch().trim().toLowerCase();
    if (!q) {
      return base;
    }
    return base.filter(
      (s) =>
        s.name.toLowerCase().includes(q) ||
        (s.description?.toLowerCase().includes(q) ?? false),
    );
  });

  readonly serviceCollections = computed((): ServiceCollection[] => {
    const items = this.filteredServices();
    const groups = [...this.menuGroups()].sort(
      (a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name),
    );
    const collections: ServiceCollection[] = [];
    const assigned = new Set<string>();

    for (const group of groups) {
      const services = items.filter((s) => s.menuGroupId === group.id);
      if (services.length === 0) {
        continue;
      }
      for (const s of services) {
        assigned.add(s.id);
      }
      collections.push({ id: group.id, name: group.name, services });
    }

    const ungrouped = items.filter((s) => !assigned.has(s.id));
    if (ungrouped.length > 0) {
      collections.push({
        id: "__other",
        name: groups.length > 0 ? "Other" : "Services",
        services: ungrouped,
      });
    }

    return collections;
  });

  activeServices(): ServiceOffering[] {
    return this.allActiveServices();
  }

  formatSlot(iso: string): string {
    return new Intl.DateTimeFormat(this.locale(), {
      weekday: "short",
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
    }).format(new Date(iso));
  }

  formatTime(iso: string): string {
    return new Intl.DateTimeFormat(this.locale(), {
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
      this.feedback.success(this.label("business.booking.waitlistOk"));
    } catch {
      this.toastError(this.label("business.booking.waitlistError"));
    }
  }

  async confirmBooking(): Promise<void> {
    const service = this.selectedService();
    const slot = this.selectedSlot();
    if (!service || !slot || !this.bookingEnabled()) {
      return;
    }

    if (this.requirePolicyAcceptance() && this.hasPolicies() && !this.policiesAccepted()) {
      this.toastError(this.label("business.booking.acceptRequired"));
      return;
    }

    if (new Date(slot).getTime() <= Date.now()) {
      this.toastError(this.label("business.booking.slotPassed"));
      this.step.set("slot");
      this.selectedSlot.set(null);
      await this.loadSlots(service);
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    try {
      const created = await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.createBooking({
              tenantId: this.tenantId(),
              serviceOfferingId: service.id,
              startAt: slot,
              customerNotes: this.notes().trim() || undefined,
            }),
          ),
        this.label("business.booking.confirming"),
        this.label("business.booking.titleConfirm"),
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
      this.feedback.success(
        created.status === 1
          ? this.label("business.booking.successConfirmed")
          : this.label("business.booking.successPending"),
        this.label("business.booking.successTitle"),
      );
    } catch (err) {
      if (err instanceof AdeniApiError) {
        this.toastError(err.message);
      } else if (err instanceof Error && err.message.includes("requires Auth0")) {
        this.toastError(this.label("business.booking.signInRequired"));
      } else {
        this.toastError(this.label("business.booking.bookFailed"));
      }
    } finally {
      this.submitting.set(false);
    }
  }

  private toastError(message: string): void {
    this.error.set(message);
    this.feedback.error(message, this.label("business.booking.errorTitle"));
  }

  signIn(): void {
    void this.api.login(window.location.pathname);
  }

  backToServices(): void {
    this.step.set("service");
    this.selectedService.set(null);
    this.slots.set([]);
  }

  openPolicy(event: Event, sectionId: string): void {
    event.preventDefault();
    event.stopPropagation();
    const el =
      document.getElementById(sectionId) ?? document.getElementById("policies");
    el?.scrollIntoView({ behavior: "smooth", block: "start" });
  }
}
