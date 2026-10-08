import { Component, inject, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute } from "@angular/router";
import type { BookingResponse } from "@adeni/shared";
import { formatBookingStatus, formatSlotTime } from "@adeni/shared";
import { AdeniConfirmService, PortalPageComponent } from "@adeni/ui";
import { map } from "rxjs";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalTabsComponent } from "../../shared/portal-tabs.component";

const PENDING_STATUS = 0;
const CONFIRMED_STATUS = 1;

@Component({
  selector: "app-bookings",
  standalone: true,
  imports: [PortalPageComponent, PortalTabsComponent],
  templateUrl: "./bookings.component.html",
  styleUrl: "./bookings.component.scss",
})
export class BookingsComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly route = inject(ActivatedRoute);
  readonly tab = toSignal(this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? "pending")), {
    initialValue: this.route.snapshot.queryParamMap.get("tab") ?? "pending",
  });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly bookings = signal<BookingResponse[]>([]);
  readonly actionId = signal<string | null>(null);
  readonly tabs = [
    { id: "pending", label: "Pending" },
    { id: "upcoming", label: "Upcoming" },
    { id: "past", label: "Past" },
  ];

  readonly formatSlotTime = formatSlotTime;
  readonly formatBookingStatus = formatBookingStatus;

  ngOnInit(): void {
    void this.load();
  }

  visible(): BookingResponse[] {
    const today = startOfToday();
    const tab = this.tab();
    if (tab === "upcoming") {
      return this.bookings().filter(
        (booking) => booking.status === CONFIRMED_STATUS && new Date(booking.startAt) >= today,
      );
    }
    if (tab === "past") {
      return this.bookings().filter(
        (booking) =>
          booking.status === 2 ||
          booking.status === 3 ||
          (booking.status === CONFIRMED_STATUS && new Date(booking.startAt) < today),
      );
    }
    return this.bookings().filter((booking) => booking.status === PENDING_STATUS);
  }

  emptyLabel(): string {
    if (this.tab() === "upcoming") return "Nothing confirmed ahead.";
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
      const items = await this.api.withAuthorizedClient((client) => client.getTenantBookings());
      this.bookings.set(items);
    } catch {
      this.error.set("Could not load bookings. Check API and dev business sub.");
      this.bookings.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  async accept(booking: BookingResponse): Promise<void> {
    await this.runAction(booking, "accept");
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
    await this.runAction(booking, "reject");
  }

  private async runAction(booking: BookingResponse, action: "accept" | "reject"): Promise<void> {
    this.actionId.set(booking.id);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) =>
        action === "accept" ? client.acceptTenantBooking(booking.id) : client.rejectTenantBooking(booking.id),
      );
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
