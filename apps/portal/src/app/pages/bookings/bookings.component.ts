import { Component, inject, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import type { BookingResponse, KioskStatusResponse } from "@adeni/shared";
import { formatBookingStatus, formatSlotTime } from "@adeni/shared";
import { AdeniCarbonIconComponent, AdeniConfirmService, PortalPageComponent } from "@adeni/ui";
import { map } from "rxjs";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalSessionService } from "../../core/services/portal-session.service";
import { PortalTabsComponent } from "../../shared/portal-tabs.component";

const PENDING_STATUS = 0;
const CONFIRMED_STATUS = 1;
const CHECKED_IN_STATUS = 4;
const COMPLETED_STATUS = 5;
const PAID_STATUS = 6;

@Component({
  selector: "app-bookings",
  standalone: true,
  imports: [PortalPageComponent, PortalTabsComponent, AdeniCarbonIconComponent, FormsModule],
  templateUrl: "./bookings.component.html",
  styleUrl: "./bookings.component.scss",
})
export class BookingsComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly session = inject(PortalSessionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly tab = toSignal(this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? "pending")), {
    initialValue: this.route.snapshot.queryParamMap.get("tab") ?? "pending",
  });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly bookings = signal<BookingResponse[]>([]);
  readonly kioskStatus = signal<KioskStatusResponse | null>(null);
  readonly savingPin = signal(false);
  readonly actionId = signal<string | null>(null);
  kioskPin = "";
  readonly tabs = [
    { id: "pending", label: "Pending" },
    { id: "upcoming", label: "Upcoming" },
    { id: "past", label: "Past" },
  ];

  readonly formatSlotTime = formatSlotTime;
  readonly formatBookingStatus = formatBookingStatus;

  pageDescription(): string {
    if (this.session.can("portal.bookings.self") && !this.session.can("portal.bookings")) {
      return "Your assigned appointments — accept, check in, complete, and close payment.";
    }
    return "Accept requests, check clients in, complete visits, and collect or mark payment.";
  }

  ngOnInit(): void {
    void this.load();
  }

  visible(): BookingResponse[] {
    const today = startOfToday();
    const tab = this.tab();
    if (tab === "upcoming") {
      return this.bookings().filter(
        (booking) =>
          (booking.status === CONFIRMED_STATUS ||
            booking.status === CHECKED_IN_STATUS ||
            booking.status === COMPLETED_STATUS) &&
          new Date(booking.startAt) >= today,
      );
    }
    if (tab === "past") {
      return this.bookings().filter(
        (booking) =>
          booking.status === 2 ||
          booking.status === 3 ||
          booking.status === PAID_STATUS ||
          ((booking.status === CONFIRMED_STATUS ||
            booking.status === CHECKED_IN_STATUS ||
            booking.status === COMPLETED_STATUS) &&
            new Date(booking.startAt) < today),
      );
    }
    return this.bookings().filter((booking) => booking.status === PENDING_STATUS);
  }

  emptyLabel(): string {
    if (this.tab() === "upcoming") return "Nothing on the floor ahead.";
    if (this.tab() === "past") return "No past bookings yet.";
    return "No pending bookings.";
  }

  lineSummary(booking: BookingResponse): string {
    if (!booking.lines?.length) {
      return booking.serviceName;
    }
    return booking.lines.map((line) => line.serviceName).join(" · ");
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [items, kiosk] = await Promise.all([
          client.getTenantBookings(),
          client.getTenantKioskStatus().catch(() => null),
        ]);
        this.bookings.set(items);
        this.kioskStatus.set(kiosk);
      });
    } catch {
      this.error.set("Could not load bookings. Check API and dev business sub.");
      this.bookings.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  openKiosk(): void {
    void this.router.navigateByUrl("/kiosk");
  }

  async saveKioskPin(): Promise<void> {
    if (!/^\d{4,8}$/.test(this.kioskPin.trim())) {
      this.error.set("Exit PIN must be 4–8 digits.");
      return;
    }
    this.savingPin.set(true);
    this.error.set(null);
    try {
      const status = await this.api.withAuthorizedClient((client) =>
        client.setTenantKioskPin(this.kioskPin.trim()),
      );
      this.kioskStatus.set(status);
      this.kioskPin = "";
    } catch {
      this.error.set("Could not save exit PIN.");
    } finally {
      this.savingPin.set(false);
    }
  }

  async clearKioskPin(): Promise<void> {
    this.savingPin.set(true);
    this.error.set(null);
    try {
      const status = await this.api.withAuthorizedClient((client) =>
        client.setTenantKioskPin(null),
      );
      this.kioskStatus.set(status);
      this.kioskPin = "";
    } catch {
      this.error.set("Could not clear exit PIN.");
    } finally {
      this.savingPin.set(false);
    }
  }

  async accept(booking: BookingResponse): Promise<void> {
    await this.runStatusAction(booking, "accept");
  }

  async reject(booking: BookingResponse): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: "Reject booking?",
      message: `Reject ${booking.serviceName} on ${formatSlotTime(booking.startAt)}? The customer will be notified.`,
      confirmLabel: "Reject",
      danger: true,
    });
    if (!ok) {
      return;
    }
    await this.runStatusAction(booking, "reject");
  }

  async checkIn(booking: BookingResponse): Promise<void> {
    await this.runStatusAction(booking, "check-in");
  }

  async complete(booking: BookingResponse): Promise<void> {
    await this.runStatusAction(booking, "complete");
  }

  async markPaid(booking: BookingResponse): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: "Mark paid at till?",
      message: `Mark ${booking.serviceName} as paid outside Adeni (cash/transfer)?`,
      confirmLabel: "Mark paid",
    });
    if (!ok) {
      return;
    }
    this.actionId.set(booking.id);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) =>
        client.markTenantBookingPaid(booking.id, {}),
      );
      this.bookings.update((list) => list.map((item) => (item.id === booking.id ? updated : item)));
    } catch {
      this.error.set("Could not mark this booking paid.");
    } finally {
      this.actionId.set(null);
    }
  }

  async collectBalance(booking: BookingResponse): Promise<void> {
    this.actionId.set(booking.id);
    this.error.set(null);
    try {
      const result = await this.api.withAuthorizedClient((client) =>
        client.collectTenantBookingBalance(booking.id),
      );
      this.bookings.update((list) =>
        list.map((item) => (item.id === booking.id ? result.booking : item)),
      );
      if (result.checkoutUrl) {
        window.open(result.checkoutUrl, "_blank", "noopener");
      }
    } catch {
      this.error.set("Could not start balance collection.");
    } finally {
      this.actionId.set(null);
    }
  }

  private async runStatusAction(
    booking: BookingResponse,
    action: "accept" | "reject" | "check-in" | "complete",
  ): Promise<void> {
    this.actionId.set(booking.id);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) => {
        switch (action) {
          case "accept":
            return client.acceptTenantBooking(booking.id);
          case "reject":
            return client.rejectTenantBooking(booking.id);
          case "check-in":
            return client.checkInTenantBooking(booking.id);
          case "complete":
            return client.completeTenantBooking(booking.id);
        }
      });
      this.bookings.update((list) => list.map((item) => (item.id === booking.id ? updated : item)));
    } catch {
      this.error.set(`Could not ${action} this booking.`);
    } finally {
      this.actionId.set(null);
    }
  }
}

function startOfToday(): Date {
  const date = new Date();
  date.setHours(0, 0, 0, 0);
  return date;
}
