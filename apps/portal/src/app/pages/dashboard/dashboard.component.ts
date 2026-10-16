import { Component, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  BOOKING_STATUS_LABELS,
  DASHBOARD_RANGE_PRESETS,
  formatBookingUsage,
  formatPrice,
  formatSlotTime,
  formatTenantStatus,
  getCategoryLabel,
  getMarketById,
  getPlanDisplayForCountry,
  hasCapability,
  hasTenantEntitlement,
  isNearBookingLimit,
  resolveCapabilities,
  resolveDashboardRange,
  subscriptionTierToSku,
  type BookingResponse,
  type BusinessProfile,
  type DashboardRangePreset,
  type ServiceOffering,
  type SubscriptionUsage,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalSessionService } from "../../core/services/portal-session.service";

type DayBar = { label: string; count: number; isToday: boolean; height: number };
type StatusSlice = { status: number; label: string; count: number; percent: number };

@Component({
  selector: "app-dashboard",
  standalone: true,
  imports: [
    RouterLink,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    PortalPageComponent,
  ],
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss",
})
export class DashboardComponent implements OnInit {
  private readonly businessApi = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  readonly session = inject(PortalSessionService);

  readonly formatTenantStatus = formatTenantStatus;
  readonly formatSlotTime = formatSlotTime;
  readonly profile = signal<BusinessProfile | null>(null);
  readonly loadError = signal(false);
  readonly pendingCount = signal(0);
  readonly activeServices = signal(0);
  readonly revenueLabel = signal("—");
  readonly revenueHint = signal("List price — scheduled & in-progress visits (not cash in bank)");
  readonly revenueUsesCollected = signal(false);
  readonly ratingLabel = signal("New");
  readonly ratingHint = signal("No reviews yet");
  readonly week = signal<DayBar[]>([]);
  readonly mix = signal<StatusSlice[]>([]);
  readonly today = signal<BookingResponse[]>([]);
  readonly usage = signal<SubscriptionUsage | null>(null);
  readonly rangePreset = signal<DashboardRangePreset>("7d");
  readonly appointmentsInRange = signal(0);
  readonly rangePresets = DASHBOARD_RANGE_PRESETS;

  readonly formatBookingUsage = formatBookingUsage;
  readonly isNearBookingLimit = isNearBookingLimit;

  ngOnInit(): void {
    void this.load();
  }

  categoryLine(): string {
    const profile = this.profile();
    if (!profile) return "";
    return getCategoryLabel("en", profile.categorySlug);
  }

  primaryLocationName(): string | null {
    const profile = this.profile();
    if (!profile) return null;
    const location = profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];
    return location ? `${location.name} · ${location.area}` : null;
  }

  offers(capability: "quotes" | "calendar" | "deposits"): boolean {
    const profile = this.profile();
    if (!profile) return false;
    return hasCapability(
      resolveCapabilities(profile.businessType, profile.categorySlug, profile.capabilities),
      capability,
    );
  }

  hasEntitlement(key: "messaging" | "staffManagement" | "analytics"): boolean {
    const profile = this.profile();
    return hasTenantEntitlement(profile?.entitlements ?? null, key);
  }

  planUsageLabel(): string {
    const usage = this.usage();
    if (!usage) return "Plan";
    return `${usage.tier} plan`;
  }

  planPriceHint(): string | null {
    const profile = this.profile();
    const usage = this.usage();
    if (!profile || !usage) return null;
    const countryCode = this.primaryCountryCode();
    if (!countryCode) return null;
    const sku = subscriptionTierToSku(usage.tier);
    return getPlanDisplayForCountry(countryCode, sku)?.priceLabel ?? null;
  }

  primaryCountryCode(): string | null {
    const profile = this.profile();
    if (!profile) return null;
    const location = profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];
    if (!location) return null;
    return getMarketById(location.marketId)?.countryCode ?? null;
  }

  publicProfileUrl(): string | null {
    const profile = this.profile();
    if (!profile) return null;
    const location = profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];
    if (!location) return null;
    return `${this.config.discoverWebUrl}/businesses/${location.slug}`;
  }

  isPractitionerView(): boolean {
    return this.session.can("portal.staff.self") && !this.session.can("portal.staff");
  }

  pageDescription(): string {
    if (this.isPractitionerView()) {
      return "Your schedule and booking inbox at a glance.";
    }
    if (this.revenueUsesCollected()) {
      return "Plan usage, appointments, and collected revenue from your online payment ledger.";
    }
    return "Plan usage and appointments — booked value is list price, not cash in bank until you collect payment.";
  }

  rangeLabel(): string {
    return this.rangePresets.find((entry) => entry.id === this.rangePreset())?.label ?? "Range";
  }

  setRange(preset: DashboardRangePreset): void {
    if (preset === this.rangePreset()) {
      return;
    }
    this.rangePreset.set(preset);
    void this.load();
  }

  usagePercent(): number {
    const usage = this.usage();
    if (!usage?.bookingsLimitThisMonth) return 0;
    return Math.min(100, Math.round((usage.bookingsUsedThisMonth / usage.bookingsLimitThisMonth) * 100));
  }

  async load(): Promise<void> {
    this.loadError.set(false);
    try {
      const range = resolveDashboardRange(this.rangePreset());
      const snapshot = await this.businessApi.withAuthorizedClient(async (client) => {
        const profile = await client.getTenantProfile();
        const [bookings, services, usage, summary, series] = await Promise.all([
          client.getTenantBookings(),
          client.getTenantServices(),
          client.getTenantSubscriptionUsage().catch(() => null),
          client.getTenantDashboardSummary(range.from, range.to),
          client.getTenantDashboardSeries("appointments", range.from, range.to).catch(() => null),
        ]);
        const location = profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];
        let rating: { avg: number | null; count: number } | null = null;
        if (location) {
          try {
            const publicProfile = await client.getBusinessProfile(location.slug);
            rating = {
              avg: publicProfile.ratingAvg ?? null,
              count: publicProfile.reviewCount ?? 0,
            };
          } catch {
            rating = null;
          }
        }
        return { profile, bookings, services, usage, rating, summary, series };
      });

      this.profile.set(snapshot.profile);
      this.pendingCount.set(snapshot.summary.pendingCount);
      this.appointmentsInRange.set(snapshot.summary.appointmentsCount);
      this.applyRevenueFromSummary(snapshot.summary);
      this.applyBookings(snapshot.bookings, snapshot.services, this.revenueUsesCollected());
      if (snapshot.series?.points.length) {
        this.applySeriesBars(snapshot.series.points);
      }
      this.usage.set(snapshot.usage);
      if (snapshot.rating?.avg) {
        this.ratingLabel.set(snapshot.rating.avg.toFixed(1));
        const count = snapshot.rating.count;
        this.ratingHint.set(count === 1 ? "1 review" : `${count} reviews`);
      } else {
        this.ratingLabel.set("New");
        this.ratingHint.set("No reviews yet");
      }
    } catch {
      this.loadError.set(true);
      this.profile.set(null);
    }
  }

  private applyRevenueFromSummary(summary: {
    analyticsIncluded: boolean;
    collectedAmount?: number | null;
    bookedValueAmount?: number | null;
    currency?: string | null;
  }): void {
    const currency = summary.currency ?? "NGN";
    if (summary.analyticsIncluded && summary.collectedAmount != null && summary.collectedAmount > 0) {
      this.revenueUsesCollected.set(true);
      this.revenueLabel.set(formatPrice(summary.collectedAmount, currency));
      this.revenueHint.set(`Collected online in ${this.rangeLabel().toLowerCase()}`);
      return;
    }

    if (summary.analyticsIncluded && summary.bookedValueAmount != null) {
      this.revenueUsesCollected.set(false);
      this.revenueLabel.set(formatPrice(summary.bookedValueAmount, currency));
      this.revenueHint.set(`Booked list value in ${this.rangeLabel().toLowerCase()} (not cash in bank)`);
      return;
    }

    this.revenueUsesCollected.set(false);
    this.revenueLabel.set("—");
    this.revenueHint.set("Upgrade to Professional for revenue analytics in this range.");
  }

  private applySeriesBars(points: { date: string; value: number }[]): void {
    const trimmed = points.slice(-14);
    const max = Math.max(1, ...trimmed.map((point) => point.value));
    const todayKey = new Date().toISOString().slice(0, 10);
    this.week.set(
      trimmed.map((point) => {
        const date = new Date(`${point.date}T12:00:00Z`);
        const label = date.toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" });
        const count = point.value;
        return {
          label,
          count,
          isToday: point.date === todayKey,
          height: count === 0 ? 6 : Math.max(12, Math.round((count / max) * 100)),
        };
      }),
    );
  }

  private applyBookings(
    bookings: BookingResponse[],
    services: ServiceOffering[],
    _revenueFromLedger: boolean,
  ): void {
    this.activeServices.set(services.filter((item) => item.isActive).length);

    const total = bookings.length || 1;
    this.mix.set(
      [0, 1, 2, 3].map((status) => {
        const count = bookings.filter((item) => item.status === status).length;
        return {
          status,
          label: BOOKING_STATUS_LABELS[status] ?? "Unknown",
          count,
          percent: Math.round((count / total) * 100),
        };
      }),
    );

    const todayKey = new Date().toDateString();
    this.today.set(
      bookings
        .filter((booking) => {
          if (booking.status === 2 || booking.status === 3) return false;
          return new Date(booking.startAt).toDateString() === todayKey;
        })
        .sort((a, b) => a.startAt.localeCompare(b.startAt)),
    );
  }
}
