import { Component, computed, DestroyRef, inject, OnInit, PLATFORM_ID, signal } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { RouterLink } from "@angular/router";
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
import { CustomerAppSessionService } from "../../services/customer-app-session.service";
import { CustomerMessagingBadgeService } from "../../services/customer-messaging-badge.service";
import { bookAgainLink, pickBookAgainSource, pickUpcomingBookings } from "./customer-home.helpers";

const UPCOMING_PREVIEW = 3;

/**
 * `/app/home` — Phase A: next appointment + quick actions, built only from APIs that already exist
 * (customer bookings list; "Book again" deep-links to the canonical public booking page).
 * Messages, benefits snapshot and review prompts land in phase C.
 */
@Component({
  selector: "app-customer-home",
  standalone: true,
  imports: [
    RouterLink,
    PortalPageComponent,
    AdeniPillComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
  ],
  templateUrl: "./customer-home.component.html",
})
export class CustomerHomeComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly session = inject(CustomerAppSessionService);
  protected readonly badge = inject(CustomerMessagingBadgeService);
  private readonly api = inject(CustomerApiService);
  private readonly locale = inject(AdeniLocaleService).locale;
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly abort = new AbortController();

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly bookings = signal<CustomerBookingResponse[]>([]);

  private readonly now = signal(Date.now());

  readonly upcoming = computed(() => pickUpcomingBookings(this.bookings(), this.now()));
  readonly next = computed(() => this.upcoming()[0] ?? null);
  readonly later = computed(() => this.upcoming().slice(1, 1 + UPCOMING_PREVIEW));
  readonly bookAgainSource = computed(() => pickBookAgainSource(this.bookings(), this.now()));
  readonly bookAgain = computed(() => {
    const source = this.bookAgainSource();
    return source ? { source, link: bookAgainLink(source) } : null;
  });

  readonly title = computed(() => {
    const name = this.session.firstName();
    return name
      ? this.tr("customer.app.home.greetingName", { name })
      : this.tr("customer.app.home.greeting");
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
      if (this.abort.signal.aborted) {
        return;
      }
      this.now.set(Date.now());
      this.bookings.set(rows);
    } catch {
      if (!this.abort.signal.aborted) {
        this.loadFailed.set(true);
        this.bookings.set([]);
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

  dayPart(iso: string): { day: string; month: string; weekday: string } {
    const date = new Date(iso);
    const locale = this.locale();
    return {
      day: new Intl.DateTimeFormat(locale, { day: "numeric" }).format(date),
      month: new Intl.DateTimeFormat(locale, { month: "short" }).format(date),
      weekday: new Intl.DateTimeFormat(locale, { weekday: "short" }).format(date),
    };
  }
}
