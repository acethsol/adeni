import { Component, inject, OnInit, signal } from "@angular/core";
import type { BookingResponse } from "@adeni/shared";
import { formatBookingStatus } from "@adeni/shared";
import { PortalPageComponent } from "../../shared/portal-page.component";
import { BusinessApiService } from "../../core/services/business-api.service";
import { formatSlotTime } from "../../shared/portal-format";

const PENDING_STATUS = 0;

@Component({
  selector: "app-bookings",
  standalone: true,
  imports: [PortalPageComponent],
  templateUrl: "./bookings.component.html",
  styleUrl: "./bookings.component.scss",
})
export class BookingsComponent implements OnInit {
  private readonly api = inject(BusinessApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly bookings = signal<BookingResponse[]>([]);
  readonly actionId = signal<string | null>(null);

  readonly formatSlotTime = formatSlotTime;
  readonly formatBookingStatus = formatBookingStatus;

  ngOnInit(): void {
    void this.load();
  }

  pending(): BookingResponse[] {
    return this.bookings().filter((b) => b.status === PENDING_STATUS);
  }

  recent(): BookingResponse[] {
    return this.bookings().filter((b) => b.status !== PENDING_STATUS);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const items = await this.api.withAuthorizedClient((c) => c.getTenantBookings());
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
    if (!confirm(`Reject ${booking.serviceName} on ${formatSlotTime(booking.startAt)}?`)) {
      return;
    }
    await this.runAction(booking, "reject");
  }

  private async runAction(booking: BookingResponse, action: "accept" | "reject"): Promise<void> {
    this.actionId.set(booking.id);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((c) =>
        action === "accept"
          ? c.acceptTenantBooking(booking.id)
          : c.rejectTenantBooking(booking.id),
      );
      this.bookings.update((list) => list.map((item) => (item.id === booking.id ? updated : item)));
    } catch {
      this.error.set(`Could not ${action} this booking.`);
    } finally {
      this.actionId.set(null);
    }
  }
}
