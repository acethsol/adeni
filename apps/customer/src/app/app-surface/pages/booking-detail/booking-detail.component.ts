import { Component, computed, DestroyRef, inject, OnInit, PLATFORM_ID, signal } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { map } from "rxjs";
import { formatBookingStatusLabel, formatSlotTime, type CustomerBookingResponse } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniLocaleService,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  type AdeniPillTone,
} from "@adeni/ui";
import { CustomerApiService } from "../../../core/services/customer-api.service";
import { bookAgainLink } from "../home/customer-home.helpers";

/**
 * `/app/bookings/:bookingId` — read-only detail built from the bookings list
 * (there is no customer "get booking by id" API yet; spec §3.2 gap 9).
 * Cancel stays on the list; booking itself always happens on the canonical public business page.
 */
@Component({
  selector: "app-booking-detail",
  standalone: true,
  imports: [
    RouterLink,
    PortalPageComponent,
    AdeniPillComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
  ],
  templateUrl: "./booking-detail.component.html",
})
export class BookingDetailComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly api = inject(CustomerApiService);
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
      const rows = await this.api.withAuthorizedClient((client) => client.getMyBookings());
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

  statusLabel(status: number): string {
    return formatBookingStatusLabel(this.locale(), status);
  }

  statusTone(status: number): AdeniPillTone {
    return status === 1 ? "success" : status === 0 ? "warning" : "neutral";
  }
}
