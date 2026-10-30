import {
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { merge } from "rxjs";
import { isPlatformBrowser } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import {
  formatBookingStatusLabel,
  formatPrice,
  formatSlotTime,
  type BusinessGiftCard,
  type ConversationSummary,
  type CustomerBookingResponse,
  type CustomerPackage,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniLocaleService,
  AdeniModalComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniMessagingRealtimeService,
  PortalPageComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
  type AdeniPillTone,
} from "@adeni/ui";
import { CustomerApiService } from "../../../core/services/customer-api.service";
import { CustomerAppSessionService } from "../../services/customer-app-session.service";
import { CustomerMessagingBadgeService } from "../../services/customer-messaging-badge.service";
import {
  bookAgainLink,
  hasBenefitsSnapshot,
  pickBookAgainSource,
  pickHomeConversations,
  pickReviewCandidate,
  pickUpcomingBookings,
  summarizeBenefits,
} from "./customer-dashboard.helpers";

const UPCOMING_PREVIEW = 3;

/**
 * `/app/dashboard` — portal overview: Up next, messages, benefits, review prompt (C2).
 * Each card loads independently so one failing API never blanks the page.
 */
@Component({
  selector: "app-customer-dashboard",
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    PortalPageComponent,
    AdeniPillComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniModalComponent,
  ],
  templateUrl: "./customer-dashboard.component.html",
  styleUrl: "./customer-dashboard.component.scss",
})
export class CustomerDashboardComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly session = inject(CustomerAppSessionService);
  protected readonly badge = inject(CustomerMessagingBadgeService);
  private readonly api = inject(CustomerApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly apiErr = injectAdeniApiError();
  private readonly locale = inject(AdeniLocaleService).locale;
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly realtime = inject(AdeniMessagingRealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly abort = new AbortController();

  readonly bookingsLoading = signal(true);
  readonly bookingsFailed = signal(false);
  readonly bookings = signal<CustomerBookingResponse[]>([]);

  readonly messagesLoading = signal(true);
  readonly messagesFailed = signal(false);
  readonly conversations = signal<ConversationSummary[]>([]);

  readonly benefitsLoading = signal(true);
  readonly benefitsFailed = signal(false);
  readonly packages = signal<CustomerPackage[]>([]);
  readonly giftCards = signal<BusinessGiftCard[]>([]);

  readonly reviewOpen = signal(false);
  readonly reviewSubmitting = signal(false);
  reviewRating = 5;
  reviewComment = "";

  private readonly now = signal(Date.now());

  readonly upcoming = computed(() => pickUpcomingBookings(this.bookings(), this.now()));
  readonly upcomingCount = computed(() => this.upcoming().length);
  readonly next = computed(() => this.upcoming()[0] ?? null);
  readonly later = computed(() => this.upcoming().slice(1, 1 + UPCOMING_PREVIEW));
  readonly bookAgainSource = computed(() => pickBookAgainSource(this.bookings(), this.now()));
  readonly bookAgain = computed(() => {
    const source = this.bookAgainSource();
    return source ? { source, link: bookAgainLink(source) } : null;
  });
  readonly reviewCandidate = computed(() => pickReviewCandidate(this.bookings(), this.now()));
  readonly homeConversations = computed(() => pickHomeConversations(this.conversations(), 2));
  readonly unreadMessagesCount = computed(
    () => this.conversations().filter((c) => c.needsAttention).length,
  );
  readonly benefitsSnapshot = computed(() => summarizeBenefits(this.packages(), this.giftCards()));
  readonly showBenefitsCard = computed(
    () =>
      this.benefitsLoading()
      || this.benefitsFailed()
      || hasBenefitsSnapshot(this.benefitsSnapshot()),
  );

  readonly title = computed(() => {
    const name = this.session.firstName();
    return name
      ? this.tr("customer.app.dashboard.greetingName", { name })
      : this.tr("customer.app.dashboard.greeting");
  });

  constructor() {
    this.destroyRef.onDestroy(() => this.abort.abort());
    merge(this.realtime.messageCreated$, this.realtime.conversationUpdated$)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        void this.loadMessages();
      });
  }

  ngOnInit(): void {
    if (this.browser) {
      void this.loadBookings();
      void this.loadMessages();
      void this.loadBenefits();
    }
  }

  async loadBookings(): Promise<void> {
    this.bookingsLoading.set(true);
    this.bookingsFailed.set(false);
    try {
      const rows = await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((client) => client.getMyBookings()),
        this.tr("customer.app.nav.dashboard"),
        this.tr("toast.loadingDetail"),
      );
      if (this.abort.signal.aborted) {
        return;
      }
      this.now.set(Date.now());
      this.bookings.set(rows);
    } catch {
      if (!this.abort.signal.aborted) {
        this.bookingsFailed.set(true);
        this.bookings.set([]);
      }
    } finally {
      if (!this.abort.signal.aborted) {
        this.bookingsLoading.set(false);
      }
    }
  }

  async loadMessages(): Promise<void> {
    this.messagesLoading.set(true);
    this.messagesFailed.set(false);
    try {
      const rows = await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((client) => client.listCustomerConversations()),
        this.tr("customer.app.nav.messages"),
        this.tr("toast.loadingDetail"),
      );
      if (this.abort.signal.aborted) {
        return;
      }
      this.conversations.set(rows);
      this.badge.setFromConversations(rows);
    } catch {
      if (!this.abort.signal.aborted) {
        this.messagesFailed.set(true);
        this.conversations.set([]);
      }
    } finally {
      if (!this.abort.signal.aborted) {
        this.messagesLoading.set(false);
      }
    }
  }

  async loadBenefits(): Promise<void> {
    this.benefitsLoading.set(true);
    this.benefitsFailed.set(false);
    try {
      const [packagesResult, giftsResult] = await this.feedback.runLoading(() =>
        Promise.allSettled([
          this.api.withAuthorizedClient((c) => c.retention.myPackages()),
          this.api.withAuthorizedClient((c) => c.commerce.purchasedCards()),
        ]),
        this.tr("customer.app.nav.benefits"),
        this.tr("toast.loadingDetail"),
      );
      if (this.abort.signal.aborted) {
        return;
      }
      if (packagesResult.status === "fulfilled") {
        this.packages.set(packagesResult.value.items);
      } else {
        this.packages.set([]);
      }
      if (giftsResult.status === "fulfilled") {
        this.giftCards.set(giftsResult.value);
      } else {
        this.giftCards.set([]);
      }
      if (packagesResult.status === "rejected" && giftsResult.status === "rejected") {
        this.benefitsFailed.set(true);
      }
    } catch {
      if (!this.abort.signal.aborted) {
        this.benefitsFailed.set(true);
        this.packages.set([]);
        this.giftCards.set([]);
      }
    } finally {
      if (!this.abort.signal.aborted) {
        this.benefitsLoading.set(false);
      }
    }
  }

  openReview(): void {
    this.reviewRating = 5;
    this.reviewComment = "";
    this.reviewOpen.set(true);
  }

  closeReview(): void {
    this.reviewOpen.set(false);
  }

  async submitReview(): Promise<void> {
    const target = this.reviewCandidate();
    if (!target || this.reviewSubmitting()) {
      return;
    }
    this.reviewSubmitting.set(true);
    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((client) =>
            client.createBookingReview(target.id, {
              rating: this.reviewRating,
              comment: this.reviewComment.trim() || undefined,
            }),
          ),
        this.tr("customer.app.dashboard.submitReview"),
        target.serviceName,
      );
      this.feedback.success(this.tr("customer.app.dashboard.reviewThanks"));
      this.reviewOpen.set(false);
      this.bookings.update((rows) =>
        rows.map((b) =>
          b.id === target.id ? { ...b, hasReview: true, canReview: false, reviewRating: this.reviewRating } : b,
        ),
      );
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "customer.app.dashboard.reviewError"));
    } finally {
      this.reviewSubmitting.set(false);
    }
  }

  when(iso: string): string {
    return formatSlotTime(iso, this.locale());
  }

  businessInitials(name: string | null | undefined): string {
    const parts = (name ?? "").trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return "?";
    }
    if (parts.length === 1) {
      return parts[0]!.slice(0, 2).toUpperCase();
    }
    return `${parts[0]![0] ?? ""}${parts[1]![0] ?? ""}`.toUpperCase();
  }

  money(amount: number, currency: string): string {
    return formatPrice(amount, currency, this.locale());
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
