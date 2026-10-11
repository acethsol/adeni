import { Component, HostListener, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";
import type { BookingResponse, KioskStatusResponse } from "@adeni/shared";
import { formatSlotTime, hasCapability } from "@adeni/shared";
import { AdeniCarbonIconComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

const CONFIRMED = 1;
const CHECKED_IN = 4;

@Component({
  selector: "app-kiosk",
  standalone: true,
  imports: [FormsModule, AdeniCarbonIconComponent],
  templateUrl: "./kiosk.component.html",
  styleUrl: "./kiosk.component.scss",
})
export class KioskComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly checkingIn = signal<string | null>(null);
  readonly exiting = signal(false);
  readonly error = signal<string | null>(null);
  readonly status = signal<KioskStatusResponse | null>(null);
  readonly bookings = signal<BookingResponse[]>([]);
  readonly checkedIn = signal<BookingResponse | null>(null);
  readonly exitOpen = signal(false);
  readonly walkInOpen = signal(false);
  readonly businessName = signal("Check in");
  readonly queueEnabled = signal(false);

  search = "";
  exitPin = "";
  walkInName = "";
  walkInPhone = "";

  readonly formatSlotTime = formatSlotTime;

  ngOnInit(): void {
    void this.bootstrap();
  }

  @HostListener("document:keydown.escape")
  onEscape(): void {
    this.openExit();
  }

  async bootstrap(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [status, profile, bookings] = await Promise.all([
          client.getTenantKioskStatus(),
          client.getTenantProfile(),
          client.getTenantBookings(),
        ]);
        this.status.set(status);
        this.businessName.set(profile.businessName || "Check in");
        this.queueEnabled.set(
          status.queueEnabled || hasCapability(profile.capabilities, "walk_in_queue"),
        );
        this.bookings.set(bookings);
      });
    } catch {
      this.error.set("Could not open check-in.");
    } finally {
      this.loading.set(false);
    }
  }

  arrivals(): BookingResponse[] {
    const q = this.search.trim().toLowerCase();
    const today = startOfLocalDay();
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);

    return this.bookings()
      .filter((b) => b.status === CONFIRMED || b.status === CHECKED_IN)
      .filter((b) => {
        const start = new Date(b.startAt);
        return start >= today && start < tomorrow;
      })
      .filter((b) => {
        if (!q) return true;
        const hay = [
          b.customerName,
          b.customerPhoneMasked,
          b.serviceName,
          b.staffDisplayName,
          ...(b.guests?.map((g) => g.displayName) ?? []),
        ]
          .filter(Boolean)
          .join(" ")
          .toLowerCase();
        return hay.includes(q);
      })
      .sort((a, b) => +new Date(a.startAt) - +new Date(b.startAt));
  }

  clientLabel(booking: BookingResponse): string {
    return (
      booking.customerName?.trim() ||
      booking.guests?.find((g) => g.displayName?.trim())?.displayName?.trim() ||
      "Guest"
    );
  }

  async checkIn(booking: BookingResponse): Promise<void> {
    if (booking.status === CHECKED_IN) {
      this.checkedIn.set(booking);
      return;
    }
    this.checkingIn.set(booking.id);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) =>
        client.checkInTenantBooking(booking.id),
      );
      this.bookings.update((list) =>
        list.map((item) => (item.id === booking.id ? updated : item)),
      );
      this.checkedIn.set(updated);
    } catch {
      this.error.set("Could not check this client in. Ask the front desk.");
    } finally {
      this.checkingIn.set(null);
    }
  }

  nextGuest(): void {
    this.checkedIn.set(null);
    this.search = "";
    this.error.set(null);
  }

  openExit(): void {
    this.exitOpen.set(true);
    this.exitPin = "";
    this.error.set(null);
  }

  cancelExit(): void {
    this.exitOpen.set(false);
    this.exitPin = "";
  }

  async confirmExit(): Promise<void> {
    const hasPin = this.status()?.hasPin === true;
    if (!hasPin) {
      if (!window.confirm("Leave check-in mode?")) {
        return;
      }
      await this.router.navigateByUrl("/bookings?tab=upcoming");
      return;
    }

    if (!/^\d{4,8}$/.test(this.exitPin.trim())) {
      this.error.set("Enter the staff PIN.");
      return;
    }

    this.exiting.set(true);
    this.error.set(null);
    try {
      const result = await this.api.withAuthorizedClient((client) =>
        client.verifyTenantKioskPin(this.exitPin.trim()),
      );
      if (!result.ok) {
        this.error.set("Incorrect PIN.");
        return;
      }
      await this.router.navigateByUrl("/bookings?tab=upcoming");
    } catch {
      this.error.set("Could not verify PIN.");
    } finally {
      this.exiting.set(false);
    }
  }

  async addWalkIn(): Promise<void> {
    if (!this.walkInName.trim() || !this.walkInPhone.trim()) {
      this.error.set("Name and phone are required for walk-ins.");
      return;
    }
    this.checkingIn.set("walk-in");
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient((client) =>
        client.addTenantQueueTicket({
          displayName: this.walkInName.trim(),
          phone: this.walkInPhone.trim(),
          partySize: 1,
        }),
      );
      this.walkInName = "";
      this.walkInPhone = "";
      this.walkInOpen.set(false);
    } catch {
      this.error.set("Could not add walk-in. Use the Queue page if this continues.");
    } finally {
      this.checkingIn.set(null);
    }
  }
}

function startOfLocalDay(): Date {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  return d;
}
