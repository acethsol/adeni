import { DatePipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import type { CustomerBookingResponse } from "@adeni/shared";
import { formatBookingStatus } from "@adeni/shared";
import { AdeniConfirmService } from "@adeni/ui";
import { CustomerApiService } from "../../core/services/customer-api.service";
import {
  ADENI_DISCOVER_CONFIG,
  isAuth0Configured,
  isDiscoverCustomerDevMode,
} from "../../core/adeni-config";

@Component({
  selector: "app-my-bookings",
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: "./my-bookings.component.html",
  styleUrl: "./my-bookings.component.scss",
})
export class MyBookingsComponent implements OnInit {
  private readonly api = inject(CustomerApiService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly bookings = signal<CustomerBookingResponse[]>([]);
  readonly cancelId = signal<string | null>(null);

  readonly devMode = isDiscoverCustomerDevMode(this.config);
  readonly auth0Mode = isAuth0Configured(this.config);
  readonly formatBookingStatus = formatBookingStatus;

  canCancel(booking: CustomerBookingResponse): boolean {
    return (
      (booking.status === 0 || booking.status === 1) &&
      new Date(booking.startAt).getTime() > Date.now()
    );
  }

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const items = await this.api.withAuthorizedClient((c) => c.getMyBookings());
      this.bookings.set(items);
    } catch {
      this.error.set("Could not load your bookings.");
      this.bookings.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  async cancel(id: string): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: "Cancel booking?",
      message: "This appointment will be cancelled. You can book again if the slot is still open.",
      confirmLabel: "Cancel booking",
      danger: true,
    });
    if (!ok) {
      return;
    }

    this.cancelId.set(id);
    try {
      await this.api.withAuthorizedClient((c) => c.cancelMyBooking(id));
      await this.load();
    } catch {
      this.error.set("Could not cancel booking.");
    } finally {
      this.cancelId.set(null);
    }
  }

  logout(): void {
    void this.api.logout();
  }
}
