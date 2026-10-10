import { CurrencyPipe } from "@angular/common";
import { Component, computed, inject, input, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type {
  BookingResponse,
  PublicStaffMember,
  ServiceMenuGroup,
  ServiceOffering,
} from "@adeni/shared";
import { t } from "@adeni/shared";
import { AdeniApiError } from "@adeni/api-client";
import { AdeniFeedbackService, AdeniLocaleService, AdeniWizardComponent, type AdeniWizardStep } from "@adeni/ui";
import { CustomerApiService } from "../core/services/customer-api.service";

type Step = "service" | "staff" | "slot" | "guests" | "confirm" | "done";

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

const MAX_GUESTS = 6;

@Component({
  selector: "app-booking-panel",
  standalone: true,
  imports: [FormsModule, CurrencyPipe, AdeniWizardComponent],
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
  /** Ordered cart; primary = first non-add-on. */
  readonly selectedServices = signal<ServiceOffering[]>([]);
  readonly addingAnother = signal(false);
  readonly staffOptions = signal<PublicStaffMember[]>([]);
  /** null = any available; set only after staff step (or skipped). */
  readonly selectedStaffId = signal<string | null>(null);
  readonly selectedStaffName = signal<string | null>(null);
  readonly slots = signal<{ startAt: string; endAt: string }[]>([]);
  readonly selectedSlot = signal<string | null>(null);
  readonly guestCount = signal(1);
  readonly guestNames = signal<string[]>([""]);
  readonly notes = signal("");
  readonly serviceSearch = signal("");
  readonly policiesAccepted = signal(false);
  readonly loadingSlots = signal(false);
  readonly loadingStaff = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly booking = signal<BookingResponse | null>(null);

  readonly primaryService = computed(
    () => this.selectedServices().find((s) => !s.isAddOn) ?? null,
  );

  readonly availableAddOns = computed(() =>
    this.allActiveServices().filter((s) => s.isAddOn),
  );

  readonly cartTotalMinutes = computed(() =>
    this.selectedServices().reduce((sum, s) => sum + s.durationMinutes, 0),
  );

  readonly cartUnitPrice = computed(() =>
    this.selectedServices().reduce((sum, s) => sum + s.priceAmount, 0),
  );

  readonly cartTotalPrice = computed(() => this.cartUnitPrice() * this.guestCount());

  readonly cartCurrency = computed(
    () => this.primaryService()?.currency ?? this.selectedServices()[0]?.currency ?? "NGN",
  );

  readonly hasStaffStep = computed(() => this.staffOptions().length > 0);

  /** Service → Staff? → Guests → Time → Confirm (guests before time so slot duration matches party size). */
  readonly bookingWizardSteps = computed((): AdeniWizardStep[] => {
    this.locale();
    const steps: AdeniWizardStep[] = [
      {
        id: "service",
        label: this.label("business.booking.stepLabelService"),
        title: this.label("business.booking.titleService"),
        lede: this.label("business.booking.ledeServices", {
          count: this.activeServices().length,
        }),
      },
    ];
    if (this.hasStaffStep()) {
      steps.push({
        id: "staff",
        label: this.label("business.booking.stepLabelStaff"),
        title: this.label("business.booking.titleStaff"),
        lede: this.label("business.booking.ledeStaff"),
      });
    }
    const primary = this.primaryService();
    steps.push(
      {
        id: "guests",
        label: this.label("business.booking.stepLabelGuests"),
        title: this.label("business.booking.titleGuests"),
        lede: this.label("business.booking.ledeGuests"),
      },
      {
        id: "slot",
        label: this.label("business.booking.stepLabelTime"),
        title: this.label("business.booking.titleTime"),
        lede: primary
          ? this.label("business.booking.ledeService", {
              name: primary.name,
              minutes: this.cartTotalMinutes(),
            })
          : null,
      },
      {
        id: "confirm",
        label: this.label("business.booking.stepLabelConfirm"),
        title: this.label("business.booking.titleConfirm"),
      },
    );
    return steps;
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

  isInCart(serviceId: string): boolean {
    return this.selectedServices().some((s) => s.id === serviceId);
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

  selectService(service: ServiceOffering): void {
    this.error.set(null);

    if (service.isAddOn) {
      if (!this.primaryService()) {
        this.error.set(this.label("business.booking.addonRequiresParent"));
        return;
      }
      this.toggleAddOn(service);
      return;
    }

    if (this.addingAnother()) {
      if (this.isInCart(service.id)) {
        return;
      }
      this.selectedServices.update((cart) => [...cart, service]);
      this.addingAnother.set(false);
      return;
    }

    const primary = this.primaryService();
    const rest = this.selectedServices().filter(
      (s) => s.id !== primary?.id && s.id !== service.id,
    );
    this.selectedServices.set([service, ...rest]);
  }

  toggleAddOn(service: ServiceOffering): void {
    if (!this.primaryService()) {
      this.error.set(this.label("business.booking.addonRequiresParent"));
      return;
    }
    const cart = this.selectedServices();
    if (cart.some((s) => s.id === service.id)) {
      this.selectedServices.set(cart.filter((s) => s.id !== service.id));
    } else {
      this.selectedServices.set([...cart, service]);
    }
  }

  removeFromCart(service: ServiceOffering): void {
    const primary = this.primaryService();
    if (primary && service.id === primary.id) {
      this.selectedServices.set([]);
      return;
    }
    this.selectedServices.update((cart) => cart.filter((s) => s.id !== service.id));
  }

  startAddAnother(): void {
    this.addingAnother.set(true);
    this.error.set(null);
  }

  cancelAddAnother(): void {
    this.addingAnother.set(false);
  }

  async continueFromServices(): Promise<void> {
    const primary = this.primaryService();
    if (!primary) {
      this.error.set(this.label("business.booking.pickPrimaryFirst"));
      return;
    }

    this.addingAnother.set(false);
    this.selectedStaffId.set(null);
    this.selectedStaffName.set(null);
    this.staffOptions.set([]);
    this.loadingStaff.set(true);
    this.error.set(null);

    try {
      const staff = await this.api.createPublicClient().getBusinessStaff(this.slug(), {
        serviceId: primary.id,
      });
      this.staffOptions.set(staff);
      if (staff.length > 0) {
        this.step.set("staff");
      } else {
        this.step.set("guests");
      }
    } catch {
      this.error.set(this.label("business.booking.staffLoadFailed"));
      this.step.set("guests");
    } finally {
      this.loadingStaff.set(false);
    }
  }

  selectStaff(staffId: string | null, displayName: string | null): void {
    if (!this.primaryService()) {
      return;
    }
    this.selectedStaffId.set(staffId);
    this.selectedStaffName.set(displayName);
    this.step.set("guests");
  }

  async continueFromGuests(): Promise<void> {
    await this.loadSlots(this.selectedStaffId());
  }

  async loadSlots(staffMemberId: string | null): Promise<void> {
    const primary = this.primaryService();
    if (!primary) {
      return;
    }

    this.loadingSlots.set(true);
    this.error.set(null);
    this.slots.set([]);
    this.selectedSlot.set(null);

    const start = new Date();
    start.setHours(0, 0, 0, 0);
    const end = new Date(start);
    end.setDate(end.getDate() + 7);
    const serviceIds = this.selectedServices().map((s) => s.id);

    try {
      const items = await this.api.createPublicClient().getBusinessSlots(this.slug(), {
        serviceId: primary.id,
        from: start.toISOString(),
        to: end.toISOString(),
        staffMemberId: staffMemberId ?? undefined,
        serviceIds,
        guestCount: this.guestCount(),
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

  adjustGuests(delta: number): void {
    const next = Math.min(MAX_GUESTS, Math.max(1, this.guestCount() + delta));
    this.guestCount.set(next);
    const names = [...this.guestNames()];
    while (names.length < next) {
      names.push("");
    }
    this.guestNames.set(names.slice(0, next));
    // Party size changes appointment length — clear any prior time pick.
    this.slots.set([]);
    this.selectedSlot.set(null);
  }

  setGuestName(index: number, value: string): void {
    const names = [...this.guestNames()];
    names[index] = value;
    this.guestNames.set(names);
  }

  async joinWaitlist(): Promise<void> {
    const primary = this.primaryService();
    if (!primary || !this.bookingEnabled()) {
      return;
    }

    try {
      await this.api.withAuthorizedClient((c) =>
        c.joinWaitlist({
          tenantId: this.tenantId(),
          serviceOfferingId: primary.id,
        }),
      );
      this.error.set(null);
      this.feedback.success(this.label("business.booking.waitlistOk"));
    } catch {
      this.toastError(this.label("business.booking.waitlistError"));
    }
  }

  async confirmBooking(): Promise<void> {
    const primary = this.primaryService();
    const cart = this.selectedServices();
    const slot = this.selectedSlot();
    if (!primary || cart.length === 0 || !slot || !this.bookingEnabled()) {
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
      await this.loadSlots(this.selectedStaffId());
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const guests = this.guestNames()
      .slice(0, this.guestCount())
      .map((name) => ({
        displayName: name.trim() || null,
      }));

    try {
      const created = await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.createBooking({
              tenantId: this.tenantId(),
              serviceOfferingId: primary.id,
              startAt: slot,
              customerNotes: this.notes().trim() || undefined,
              staffMemberId: this.selectedStaffId(),
              guestCount: this.guestCount(),
              guests,
              lines: cart.map((s) => ({ serviceOfferingId: s.id })),
            }),
          ),
        this.label("business.booking.confirming"),
        this.label("business.booking.titleConfirm"),
      );
      this.booking.set(created);

      const needsDeposit =
        this.supportsDeposits() &&
        this.depositPercent() > 0 &&
        this.cartTotalPrice() > 0;

      if (needsDeposit) {
        const payment = await this.api.withAuthorizedClient((c) =>
          c.initializePayment({
            tenantId: this.tenantId(),
            bookingId: created.id,
            currency: this.cartCurrency(),
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
    this.selectedServices.set([]);
    this.addingAnother.set(false);
    this.staffOptions.set([]);
    this.selectedStaffId.set(null);
    this.selectedStaffName.set(null);
    this.slots.set([]);
    this.selectedSlot.set(null);
    this.guestCount.set(1);
    this.guestNames.set([""]);
  }

  backToStaff(): void {
    if (this.hasStaffStep()) {
      this.step.set("staff");
      this.slots.set([]);
      this.selectedSlot.set(null);
    } else {
      this.backToServices();
    }
  }

  backToGuests(): void {
    this.step.set("guests");
    this.slots.set([]);
    this.selectedSlot.set(null);
  }

  backToSlot(): void {
    this.step.set("slot");
  }

  openPolicy(event: Event, sectionId: string): void {
    event.preventDefault();
    event.stopPropagation();
    const el =
      document.getElementById(sectionId) ?? document.getElementById("policies");
    el?.scrollIntoView({ behavior: "smooth", block: "start" });
  }
}
