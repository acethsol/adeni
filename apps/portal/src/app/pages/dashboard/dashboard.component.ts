import {
  CdkDrag,
  CdkDragDrop,
  CdkDragHandle,
  CdkDropList,
  moveItemInArray,
} from "@angular/cdk/drag-drop";
import type { ChartTabularData } from "@carbon/charts/interfaces";
import { Component, computed, DestroyRef, inject, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { RouterLink } from "@angular/router";
import {
  addDashboardWidget,
  BOOKING_STATUS_LABELS,
  cloneDashboardLayout,
  DASHBOARD_LAYOUT_PRESETS,
  DASHBOARD_RANGE_PRESETS,
  DASHBOARD_WIDGET_DEFINITIONS,
  dashboardWidgetTitle,
  resolveDashboardCapabilities,
  resolveDashboardLayoutPreset,
  type DashboardLayoutPresetId,
  type QueueTicketResponse,
  formatBookingUsage,
  formatSubscriptionPlanLabel,
  formatPrice,
  formatSlotTime,
  formatTenantStatus,
  getCategoryLabel,
  getMarketById,
  getPlanDisplayForCountry,
  hasCapability,
  hasTenantEntitlement,
  isDashboardWidgetVisible,
  isNearBookingLimit,
  layoutIncludesWidget,
  removeDashboardWidget,
  resolveCapabilities,
  resolveDashboardLayout,
  resolveDashboardRange,
  type BookingResponse,
  type BusinessProfile,
  type ConversationSummary,
  type DashboardLayout,
  type DashboardLayoutWidget,
  type DashboardRangePreset,
  type DashboardWidgetId,
  type QuoteRequestResponse,
  type ServiceOffering,
  type SubscriptionUsage,
  type TenantDashboardBreakdown,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";
import { MessagingRealtimeService, TenantOperationsRealtimeService } from "@adeni/ui";
import { PortalSessionService } from "../../core/services/portal-session.service";
import { DashboardCarbonChartComponent } from "./dashboard-carbon-chart.component";
import { clearDashboardLayout } from "./dashboard-layout-store";
import {
  dashboardLayoutToApi,
  resolveDashboardLayoutFromApi,
} from "./dashboard-layout-sync";
import { DashboardWidgetComponent } from "./dashboard-widget.component";

type StatusSlice = { status: number; label: string; count: number; percent: number };

@Component({
  selector: "app-dashboard",
  standalone: true,
  imports: [
    RouterLink,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniStaffButtonComponent,
    PortalPageComponent,
    DashboardWidgetComponent,
    DashboardCarbonChartComponent,
  ],
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss",
})
export class DashboardComponent implements OnInit {
  private readonly businessApi = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messagingRealtime = inject(MessagingRealtimeService);
  private readonly operationsRealtime = inject(TenantOperationsRealtimeService);
  readonly session = inject(PortalSessionService);

  readonly formatTenantStatus = formatTenantStatus;
  readonly formatSlotTime = formatSlotTime;
  readonly dashboardWidgetTitle = dashboardWidgetTitle;
  readonly profile = signal<BusinessProfile | null>(null);
  readonly layout = signal<DashboardLayout>(resolveDashboardLayout(false));
  readonly editMode = signal(false);
  readonly pickerOpen = signal(false);
  readonly loadError = signal(false);
  readonly pendingCount = signal(0);
  readonly activeServices = signal(0);
  readonly revenueLabel = signal("—");
  readonly revenueHint = signal("List price — scheduled & in-progress visits (not cash in bank)");
  readonly revenueUsesCollected = signal(false);
  readonly ratingLabel = signal("New");
  readonly ratingHint = signal("No reviews yet");
  readonly appointmentChartData = signal<ChartTabularData>([]);
  readonly collectedChartData = signal<ChartTabularData>([]);
  readonly serviceMixChartData = signal<ChartTabularData>([]);
  readonly bookedValueChartData = signal<ChartTabularData>([]);
  readonly staffLeaderboardChartData = signal<ChartTabularData>([]);
  readonly unreadMessagesCount = signal(0);
  readonly messageInboxPreview = signal<ConversationSummary[]>([]);
  readonly openQuotesCount = signal(0);
  readonly queueWaiting = signal<QueueTicketResponse[]>([]);
  readonly mix = signal<StatusSlice[]>([]);
  readonly bookingMixChartData = signal<ChartTabularData>([]);
  readonly today = signal<BookingResponse[]>([]);
  readonly usage = signal<SubscriptionUsage | null>(null);
  readonly rangePreset = signal<DashboardRangePreset>("7d");
  readonly appointmentsInRange = signal(0);
  readonly rangePresets = DASHBOARD_RANGE_PRESETS;

  readonly formatBookingUsage = formatBookingUsage;
  readonly isNearBookingLimit = isNearBookingLimit;

  readonly visibleLayoutWidgets = computed(() =>
    this.layout().widgets.filter((entry) => this.widgetShown(entry.id)),
  );

  readonly layoutPresets = DASHBOARD_LAYOUT_PRESETS;

  readonly addableWidgets = computed(() => {
    const layout = this.layout();
    return DASHBOARD_WIDGET_DEFINITIONS.filter(
      (definition) =>
        !layoutIncludesWidget(layout, definition.id)
        && isDashboardWidgetVisible(definition.id, this.widgetVisibilityContext()),
    );
  });

  ngOnInit(): void {
    this.rangePreset.set(resolveDashboardLayout(this.isPractitionerView()).range);
    this.messagingRealtime.conversationUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((summary) => {
        this.messageInboxPreview.update((items) => {
          const index = items.findIndex((item) => item.id === summary.id);
          if (index < 0) {
            return [summary, ...items].slice(0, 4);
          }
          const copy = [...items];
          copy[index] = summary;
          copy.sort((a, b) => b.lastMessageAt.localeCompare(a.lastMessageAt));
          return copy.slice(0, 4);
        });
        void this.refreshUnreadMessagesCount();
      });
    this.operationsRealtime.pendingBookingsChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.pendingCount.set(event.pendingCount);
      });
    this.operationsRealtime.quotePipelineChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.openQuotesCount.set(event.openCount);
      });
    void this.load();
  }

  private async refreshUnreadMessagesCount(): Promise<void> {
    try {
      const count = await this.businessApi.withAuthorizedClient((client) =>
        client.getTenantMessageUnreadCount(),
      );
      this.unreadMessagesCount.set(count);
    } catch {
      // keep prior count
    }
  }

  widgetVisibilityContext() {
    return {
      isPractitionerView: this.isPractitionerView(),
      entitlements: this.profile()?.entitlements ?? null,
      capabilities: resolveDashboardCapabilities(this.profile()),
      canPortal: (permission: Parameters<PortalSessionService["can"]>[0]) =>
        this.session.can(permission)
        || (permission === "portal.bookings" && this.session.can("portal.bookings.self")),
    };
  }

  widgetShown(id: DashboardWidgetId): boolean {
    if (!layoutIncludesWidget(this.layout(), id)) {
      return false;
    }
    return isDashboardWidgetVisible(id, this.widgetVisibilityContext());
  }

  widgetSpanClass(entry: DashboardLayoutWidget): string {
    const width = entry.w === 4 || entry.w === 6 || entry.w === 8 ? entry.w : 12;
    return `widget-span-${width}`;
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

  hasEntitlement(key: "messaging" | "advancedAnalytics" | "staffPerformanceAnalytics"): boolean {
    const profile = this.profile();
    return hasTenantEntitlement(profile?.entitlements ?? null, key);
  }

  planUsageLabel(): string {
    const usage = this.usage();
    if (!usage) return "Plan";
    return formatSubscriptionPlanLabel(usage);
  }

  planPriceHint(): string | null {
    const profile = this.profile();
    const usage = this.usage();
    if (!profile || !usage) return null;
    const countryCode = this.primaryCountryCode();
    if (!countryCode) return null;
    const sku = usage.subscriptionSku;
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
    this.persistLayout({ ...this.layout(), range: preset });
    void this.load();
  }

  toggleCustomize(): void {
    const next = !this.editMode();
    this.editMode.set(next);
    this.pickerOpen.set(false);
  }

  finishCustomize(): void {
    this.editMode.set(false);
    this.pickerOpen.set(false);
  }

  resetLayoutDefault(): void {
    const profile = this.profile();
    if (profile) {
      clearDashboardLayout(profile.tenantId, this.isPractitionerView());
    }
    let fallback = cloneDashboardLayout(resolveDashboardLayout(this.isPractitionerView()));
    while (layoutIncludesWidget(fallback, "quick_actions")) {
      fallback = removeDashboardWidget(fallback, "quick_actions");
    }
    this.rangePreset.set(fallback.range);
    void this.persistLayout(fallback, true);
  }

  openPicker(): void {
    this.pickerOpen.set(true);
  }

  closePicker(): void {
    this.pickerOpen.set(false);
  }

  addWidget(widgetId: DashboardWidgetId): void {
    const width =
      widgetId === "today_schedule" || widgetId === "pending_inbox" || widgetId === "messages_unread"
        ? 4
        : widgetId === "week_appointments" || widgetId === "queue_now"
          ? 8
          : 12;
    this.persistLayout(addDashboardWidget(this.layout(), widgetId, width));
    this.pickerOpen.set(false);
    void this.load();
  }

  applyLayoutPreset(presetId: DashboardLayoutPresetId): void {
    const layout = resolveDashboardLayoutPreset(
      presetId === "practitioner" || this.isPractitionerView() ? "practitioner" : presetId,
    );
    this.rangePreset.set(layout.range);
    this.persistLayout(layout, true);
    this.pickerOpen.set(false);
  }

  offersWalkInQueue(): boolean {
    return resolveDashboardCapabilities(this.profile()).includes("walk_in_queue");
  }

  verificationHint(): string {
    const profile = this.profile();
    if (!profile) return "";
    if (profile.status === 2) return "Verified — customers see you in discovery.";
    if (profile.status === 1) return "Pending review — we’ll email you when you’re live.";
    return "Complete verification to appear in discovery.";
  }

  removeWidget(widgetId: DashboardWidgetId): void {
    this.persistLayout(removeDashboardWidget(this.layout(), widgetId));
  }

  onWidgetDrop(event: CdkDragDrop<DashboardLayoutWidget[]>): void {
    if (event.previousIndex === event.currentIndex) {
      return;
    }
    const visible = [...this.visibleLayoutWidgets()];
    moveItemInArray(visible, event.previousIndex, event.currentIndex);
    const hidden = this.layout().widgets.filter((entry) => !this.widgetShown(entry.id));
    this.persistLayout({ ...this.layout(), widgets: [...visible, ...hidden] });
  }

  customerInitials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return "?";
    }
    if (parts.length === 1) {
      return parts[0]!.slice(0, 2).toUpperCase();
    }
    return `${parts[0]![0] ?? ""}${parts[1]![0] ?? ""}`.toUpperCase();
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
        const storedLayout = await resolveDashboardLayoutFromApi(
          () => client.getTenantDashboardLayout(),
          (layout) => client.putTenantDashboardLayout(layout),
          profile.tenantId,
          this.isPractitionerView(),
        );
        const wantsServiceMix = layoutIncludesWidget(storedLayout, "service_mix");
        const wantsCollected = layoutIncludesWidget(storedLayout, "collected_revenue");
        const wantsBookedValue = layoutIncludesWidget(storedLayout, "booked_value");
        const wantsStaffBoard = layoutIncludesWidget(storedLayout, "staff_leaderboard");
        const wantsQueue = layoutIncludesWidget(storedLayout, "queue_now");
        const wantsQuotes = layoutIncludesWidget(storedLayout, "quote_pipeline");
        const wantsUnread = layoutIncludesWidget(storedLayout, "messages_unread");
        const analytics = hasTenantEntitlement(profile.entitlements ?? null, "advancedAnalytics");
        const messaging = hasTenantEntitlement(profile.entitlements ?? null, "messaging");
        const staffBoard = hasTenantEntitlement(profile.entitlements ?? null, "staffPerformanceAnalytics");
        const caps = resolveDashboardCapabilities(profile);

        const [
          bookings,
          services,
          usage,
          summary,
          series,
          collectedSeries,
          serviceBreakdown,
          bookedSeries,
          staffBreakdown,
          unreadCount,
          messageInbox,
          queueFloor,
          quotes,
        ] = await Promise.all([
          client.getTenantBookings(),
          client.getTenantServices(),
          client.getTenantSubscriptionUsage().catch(() => null),
          client.getTenantDashboardSummary(range.from, range.to),
          client.getTenantDashboardSeries("appointments", range.from, range.to),
          analytics && wantsCollected
            ? client.getTenantDashboardSeries("collected", range.from, range.to).catch(() => null)
            : Promise.resolve(null),
          analytics && wantsServiceMix
            ? client.getTenantDashboardBreakdown("service", range.from, range.to).catch(() => null)
            : Promise.resolve(null),
          analytics && wantsBookedValue
            ? client.getTenantDashboardSeries("booked_value", range.from, range.to).catch(() => null)
            : Promise.resolve(null),
          staffBoard && wantsStaffBoard
            ? client.getTenantDashboardBreakdown("staff", range.from, range.to).catch(() => null)
            : Promise.resolve(null),
          wantsUnread && messaging
            ? client.getTenantMessageUnreadCount().catch(() => 0)
            : Promise.resolve(0),
          wantsUnread && messaging
            ? client
                .listTenantConversations({ page: 1, pageSize: 4 })
                .catch(() => ({ items: [] as ConversationSummary[], totalCount: 0, page: 1, pageSize: 4 }))
            : Promise.resolve({ items: [] as ConversationSummary[], totalCount: 0, page: 1, pageSize: 4 }),
          wantsQueue && caps.includes("walk_in_queue")
            ? client.getTenantQueueFloor().catch(() => null)
            : Promise.resolve(null),
          wantsQuotes && caps.includes("quotes")
            ? client.listTenantQuotes().catch(() => [] as QuoteRequestResponse[])
            : Promise.resolve([] as QuoteRequestResponse[]),
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
        return {
          profile,
          storedLayout,
          bookings,
          services,
          usage,
          rating,
          summary,
          series,
          collectedSeries,
          serviceBreakdown,
          bookedSeries,
          staffBreakdown,
          unreadCount,
          messageInbox,
          queueFloor,
          quotes,
        };
      });

      this.profile.set(snapshot.profile);
      this.layout.set(snapshot.storedLayout);
      if (!this.editMode()) {
        this.rangePreset.set(snapshot.storedLayout.range);
      }
      this.pendingCount.set(snapshot.summary.pendingCount);
      this.appointmentsInRange.set(snapshot.summary.appointmentsCount);
      this.applyRevenueFromSummary(snapshot.summary);
      this.applyBookings(snapshot.bookings, snapshot.services, this.revenueUsesCollected());
      this.appointmentChartData.set(this.toSeriesChartData(snapshot.series?.points ?? []));
      this.collectedChartData.set(this.toSeriesChartData(snapshot.collectedSeries?.points ?? [], "Collected"));
      this.serviceMixChartData.set(
        (snapshot.serviceBreakdown?.items ?? []).map((item: TenantDashboardBreakdown["items"][number]) => ({
          group: item.label,
          value: item.value,
        })),
      );
      this.bookedValueChartData.set(this.toSeriesChartData(snapshot.bookedSeries?.points ?? [], "Booked"));
      this.staffLeaderboardChartData.set(
        (snapshot.staffBreakdown?.items ?? []).map((item: TenantDashboardBreakdown["items"][number]) => ({
          group: "Bookings",
          key: item.label,
          value: item.value,
        })),
      );
      this.unreadMessagesCount.set(snapshot.unreadCount ?? 0);
      this.messageInboxPreview.set(snapshot.messageInbox?.items ?? []);
      this.openQuotesCount.set(
        snapshot.quotes.filter(
          (quote: QuoteRequestResponse) => quote.status === "submitted" || quote.status === "quoted",
        ).length,
      );
      this.queueWaiting.set(
        (snapshot.queueFloor?.items ?? [])
          .filter((ticket: QueueTicketResponse) => ticket.status === "waiting")
          .slice(0, 6),
      );
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

  private persistLayout(layout: DashboardLayout, reload = false): void {
    this.layout.set(layout);
    const tenantId = this.profile()?.tenantId;
    if (!tenantId) {
      return;
    }
    void this.businessApi
      .withAuthorizedClient((client) =>
        client.putTenantDashboardLayout(dashboardLayoutToApi(layout)),
      )
      .catch(() => undefined)
      .finally(() => {
        if (reload) {
          void this.load();
        }
      });
  }

  private toSeriesChartData(
    points: { date: string; value: number }[],
    group = "Appointments",
  ): ChartTabularData {
    return points.slice(-21).map((point) => ({
      group,
      key: this.formatChartKey(point.date),
      value: point.value,
    }));
  }

  private formatChartKey(isoDate: string): string {
    const date = new Date(`${isoDate}T12:00:00Z`);
    return date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
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
    this.revenueHint.set("Revenue metrics are not shown for this period.");
  }

  private applyBookings(
    bookings: BookingResponse[],
    services: ServiceOffering[],
    _revenueFromLedger: boolean,
  ): void {
    this.activeServices.set(services.filter((item) => item.isActive).length);

    const total = bookings.length || 1;
    const slices = [0, 1, 2, 3].map((status) => {
      const count = bookings.filter((item) => item.status === status).length;
      return {
        status,
        label: BOOKING_STATUS_LABELS[status] ?? "Unknown",
        count,
        percent: Math.round((count / total) * 100),
      };
    });
    this.mix.set(slices);
    this.bookingMixChartData.set(
      slices
        .filter((slice) => slice.count > 0)
        .map((slice) => ({ group: slice.label, value: slice.count })),
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
