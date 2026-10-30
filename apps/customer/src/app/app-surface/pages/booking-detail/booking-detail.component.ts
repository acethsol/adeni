import { Component, computed, DestroyRef, inject, OnInit, PLATFORM_ID, signal } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { map } from "rxjs";
import { formatBookingStatusLabel, formatSlotTime, type CustomerBookingResponse } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniLocaleService,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  type AdeniCarbonIconName,
} from "@adeni/ui";
import { CustomerApiService } from "../../../core/services/customer-api.service";
import { bookAgainLink } from "../dashboard/customer-dashboard.helpers";

@Component({
  selector: "app-booking-detail",
  standalone: true,
  imports: [
    RouterLink,
    PortalPageComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
  ],
  templateUrl: "./booking-detail.component.html",
  styleUrl: "./booking-detail.component.scss",
})
export class BookingDetailComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly api = inject(CustomerApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly route = inject(ActivatedRoute);
  private readonly locale = inject(AdeniLocaleService).locale;
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly abort = new AbortController();

  readonly bookingId = toSignal(this.route.paramMap.pipe(map((params) => params.get("bookingId") ?? "")), {
    initialValue: this.route.snapshot.paramMap.get("bookingId") ?? "",
  });

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly bookings = signal<CustomerBookingResponse[]>([]);

  readonly booking = computed(() => this.bookings().find((b) => b.id === this.bookingId()) ?? null);
  readonly canBookAgain = computed(() => {
    const b = this.booking();
    return !!b && b.status === 1 && new Date(b.startAt).getTime() < Date.now() && !!b.businessSlug;
  });
  readonly bookAgain = computed(() => {
    const b = this.booking();
    return b && this.canBookAgain() ? bookAgainLink(b) : null;
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.abort.abort());
  }

  ngOnInit(): void {
    if (this.browser) {
      void this.load();
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadFailed.set(false);
    try {
      const rows = await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((client) => client.getMyBookings()),
        this.tr("bookings.title"),
        this.tr("toast.loadingDetail"),
      );
      if (!this.abort.signal.aborted) {
        this.bookings.set(rows);
      }
    } catch {
      if (!this.abort.signal.aborted) {
        this.loadFailed.set(true);
      }
    } finally {
      if (!this.abort.signal.aborted) {
        this.loading.set(false);
      }
    }
  }

  when(iso: string): string {
    return formatSlotTime(iso, this.locale());
  }

  formatTimeOnly(iso: string): string {
    return new Intl.DateTimeFormat(this.locale(), {
      hour: "numeric",
      minute: "2-digit",
    }).format(new Date(iso));
  }

  dayPart(iso: string): { day: string; month: string; weekday: string; year: string } {
    const date = new Date(iso);
    const loc = this.locale();
    return {
      day: new Intl.DateTimeFormat(loc, { day: "numeric" }).format(date),
      month: new Intl.DateTimeFormat(loc, { month: "long" }).format(date),
      weekday: new Intl.DateTimeFormat(loc, { weekday: "long" }).format(date),
      year: new Intl.DateTimeFormat(loc, { year: "numeric" }).format(date),
    };
  }

  statusLabel(status: number): string {
    return formatBookingStatusLabel(this.locale(), status);
  }

  bookingStatusView(status: number): {
    label: string;
    icon: AdeniCarbonIconName;
    tone: "pending" | "confirmed" | "closed";
  } {
    switch (status) {
      case 0:
        return { label: this.statusLabel(status), icon: "time", tone: "pending" };
      case 1:
        return { label: this.statusLabel(status), icon: "calendar", tone: "confirmed" };
      case 2:
        return { label: this.statusLabel(status), icon: "misuse", tone: "closed" };
      case 3:
        return { label: this.statusLabel(status), icon: "close", tone: "closed" };
      default:
        return { label: this.statusLabel(status), icon: "time", tone: "closed" };
    }
  }
}
