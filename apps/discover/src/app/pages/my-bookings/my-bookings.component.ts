import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import type { CustomerBookingResponse } from "@adeni/shared";
import {
  formatBookingStatusLabel,
  formatSlotTime,
  t,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniFeedbackService,
  AdeniLocaleService,
} from "@adeni/ui";
import { CustomerApiService } from "../../core/services/customer-api.service";
import {
  ADENI_DISCOVER_CONFIG,
  isAuth0Configured,
  isDiscoverCustomerDevMode,
} from "../../core/adeni-config";

type BookingsTab = "upcoming" | "past";

@Component({
  selector: "app-my-bookings",
  standalone: true,
  imports: [RouterLink, AdeniCarbonIconComponent],
  templateUrl: "./my-bookings.component.html",
  styleUrl: "./my-bookings.component.scss",
})
export class MyBookingsComponent implements OnInit {
  private readonly api = inject(CustomerApiService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly localeService = inject(AdeniLocaleService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly bookings = signal<CustomerBookingResponse[]>([]);
  readonly cancelId = signal<string | null>(null);
  readonly tab = signal<BookingsTab>("upcoming");

  readonly locale = this.localeService.locale;
  readonly devMode = isDiscoverCustomerDevMode(this.config);
  readonly auth0Mode = isAuth0Configured(this.config);

  readonly upcoming = computed(() => {
    const now = Date.now();
    return this.bookings()
      .filter((b) => new Date(b.startAt).getTime() >= now && b.status !== 2 && b.status !== 3)
      .sort((a, b) => +new Date(a.startAt) - +new Date(b.startAt));
  });

  readonly past = computed(() => {
    const now = Date.now();
    return this.bookings()
      .filter((b) => new Date(b.startAt).getTime() < now || b.status === 2 || b.status === 3)
      .sort((a, b) => +new Date(b.startAt) - +new Date(a.startAt));
  });

  readonly visible = computed(() =>
    this.tab() === "upcoming" ? this.upcoming() : this.past(),
  );

  label(key: string, vars?: Record<string, string | number>): string {
    return t(this.locale(), key, vars);
  }

  statusLabel(status: number): string {
    return formatBookingStatusLabel(this.locale(), status);
  }

  statusTone(status: number): string {
    switch (status) {
      case 0:
        return "pending";
      case 1:
        return "confirmed";
      case 2:
        return "rejected";
      case 3:
        return "cancelled";
      default:
        return "unknown";
    }
  }

  formatWhen(iso: string): string {
    return formatSlotTime(iso, this.locale());
  }

  namedGuests(booking: CustomerBookingResponse): string[] {
    return (booking.guests ?? [])
      .map((g) => g.displayName?.trim())
      .filter((name): name is string => !!name);
  }

  dayPart(iso: string): { day: string; month: string; weekday: string } {
    const date = new Date(iso);
    const locale = this.locale();
    return {
      day: new Intl.DateTimeFormat(locale, { day: "numeric" }).format(date),
      month: new Intl.DateTimeFormat(locale, { month: "short" }).format(date),
      weekday: new Intl.DateTimeFormat(locale, { weekday: "short" }).format(date),
    };
  }

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
    this.loadFailed.set(false);
    try {
      const items = await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.getMyBookings()),
        this.label("bookings.title"),
        this.label("bookings.description"),
      );
      this.bookings.set(items);
      if (this.upcoming().length === 0 && this.past().length > 0) {
        this.tab.set("past");
      }
    } catch {
      this.loadFailed.set(true);
      this.bookings.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  async cancel(booking: CustomerBookingResponse): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: this.label("bookings.cancelBooking") + "?",
      message: `${booking.serviceName} · ${this.formatWhen(booking.startAt)}`,
      confirmLabel: this.label("bookings.cancelBooking"),
      danger: true,
    });
    if (!ok) {
      return;
    }

    this.cancelId.set(booking.id);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.cancelMyBooking(booking.id)),
        this.label("bookings.cancelling"),
        booking.serviceName,
      );
      this.feedback.success(this.label("bookings.cancelSuccess"));
      await this.load();
    } catch {
      this.feedback.error(this.label("bookings.cancelError"));
    } finally {
      this.cancelId.set(null);
    }
  }

  logout(): void {
    void this.api.logout();
  }
}
