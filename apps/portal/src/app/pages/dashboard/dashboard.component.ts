import { Component, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  BOOKING_STATUS_LABELS,
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
  subscriptionTierToSku,
  type BookingResponse,
  type BusinessProfile,
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

const DAY_LABELS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

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

  usagePercent(): number {
    const usage = this.usage();
    if (!usage?.bookingsLimitThisMonth) return 0;
    return Math.min(100, Math.round((usage.bookingsUsedThisMonth / usage.bookingsLimitThisMonth) * 100));
  }

  async load(): Promise<void> {
    this.loadError.set(false);
    try {
      const snapshot = await this.businessApi.withAuthorizedClient(async (client) => {
        const profile = await client.getTenantProfile();
        const [bookings, services, usage] = await Promise.all([
          client.getTenantBookings(),
          client.getTenantServices(),
          client.getTenantSubscriptionUsage().catch(() => null),
        ]);
        const caps = resolveCapabilities(
          profile.businessType,
          profile.categorySlug,
          profile.capabilities,
        );
        let collectedTotal: number | null = null;
        let collectedCurrency = services[0]?.currency ?? "NGN";
        if (hasCapability(caps, "deposits")) {
          try {
            const ledgerPage = await client.listPaymentLedger(profile.tenantId, 1, 100);
            collectedTotal = ledgerPage.items
              .filter((entry) => entry.status === "completed")
              .reduce((sum, entry) => sum + entry.amount, 0);
            if (ledgerPage.items[0]?.currency) {
              collectedCurrency = ledgerPage.items[0].currency;
            }
          } catch {
            collectedTotal = null;
          }
        }
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
        return { profile, bookings, services, usage, rating, collectedTotal, collectedCurrency };
      });

      this.profile.set(snapshot.profile);
      const useCollected = snapshot.collectedTotal != null;
      this.revenueUsesCollected.set(useCollected);
      this.applyBookings(snapshot.bookings, snapshot.services, useCollected);
      if (useCollected) {
        this.revenueLabel.set(formatPrice(snapshot.collectedTotal!, snapshot.collectedCurrency));
        this.revenueHint.set("Completed online payments (ledger)");
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

  private applyBookings(
    bookings: BookingResponse[],
    services: ServiceOffering[],
    revenueFromLedger: boolean,
  ): void {
    this.pendingCount.set(bookings.filter((item) => item.status === 0).length);
    this.activeServices.set(services.filter((item) => item.isActive).length);

    if (!revenueFromLedger) {
      const prices = new Map(services.map((service) => [service.id, service.priceAmount]));
      const booked = bookings
        .filter((item) => item.status === 1 || item.status === 4 || item.status === 5)
        .reduce((sum, item) => sum + (prices.get(item.serviceOfferingId) ?? 0), 0);
      const currency = services[0]?.currency ?? "NGN";
      this.revenueLabel.set(formatPrice(booked, currency));
      this.revenueHint.set("List price — scheduled & in-progress visits (not cash in bank)");
    }

    const weekStart = startOfWeek(new Date());
    const counts = DAY_LABELS.map((label, index) => {
      const dayDate = new Date(weekStart);
      dayDate.setDate(weekStart.getDate() + index);
      const count = bookings.filter((booking) => {
        if (booking.status === 2 || booking.status === 3) return false;
        return new Date(booking.startAt).toDateString() === dayDate.toDateString();
      }).length;
      return {
        label,
        count,
        isToday: dayDate.toDateString() === new Date().toDateString(),
      };
    });
    const max = Math.max(1, ...counts.map((day) => day.count));
    this.week.set(
      counts.map((day) => ({
        ...day,
        height: day.count === 0 ? 6 : Math.max(12, Math.round((day.count / max) * 100)),
      })),
    );

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

function startOfWeek(date: Date): Date {
  const result = new Date(date);
  const day = result.getDay();
  const diff = (day === 0 ? -6 : 1) - day;
  result.setDate(result.getDate() + diff);
  result.setHours(0, 0, 0, 0);
  return result;
}
