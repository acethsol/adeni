import {
  CdkDrag,
  CdkDragDrop,
  CdkDropList,
  moveItemInArray,
} from "@angular/cdk/drag-drop";
import type { ChartTabularData } from "@carbon/charts/interfaces";
import { Component, computed, DestroyRef, inject, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { RouterLink } from "@angular/router";
import {
  addDashboardWidget,
  cloneDashboardLayout,
  DASHBOARD_LAYOUT_PRESET_ICONS,
  DASHBOARD_LAYOUT_PRESETS,
  DASHBOARD_RANGE_PRESETS,
  formatDashboardRangePresetLabel,
  formatDashboardWidgetSummary,
  formatDashboardWidgetTitle,
  formatBookingStatusLabel,
  filterDashboardLayoutByVisibility,
  dashboardLayoutFingerprint,
  getDashboardLayoutPresetDisplayCopy,
  resolveRecommendedDashboardLayoutPresetId,
  visibleDashboardLayoutFingerprint,
  t,
  defaultLocale,
  DASHBOARD_WIDGET_DEFINITIONS,
  getReviewCountLabel,
  resolveDashboardCapabilities,
  resolveDashboardLayoutPreset,
  type DashboardLayoutPreset,
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
  mergeTenantEntitlements,
  hasTenantEntitlement,
  isDashboardWidgetVisible,
  isNearBookingLimit,
  layoutIncludesWidget,
  normalizeDashboardWidgetWidth,
  removeDashboardWidget,
  updateDashboardWidgetWidth,
  resolveCapabilities,
  resolveDashboardLayout,
  formatDashboardRangeOverviewHint,
  formatDashboardRangeStatHint,
  type BookingResponse,
  type BusinessProfile,
  type ConversationSummary,
  type DashboardLayout,
  type DashboardLayoutWidget,
  type DashboardRangePreset,
  type DashboardWidgetId,
  type DashboardWidgetWidth,
  type QuoteRequestResponse,
  type ServiceOffering,
  type SubscriptionUsage,
  type TenantEntitlements,
  type TenantDashboardBreakdown,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  AdeniDashboardLayoutPresetCardComponent,
  AdeniModalComponent,
  PortalPageComponent,
  type AdeniCarbonIconName,
  injectAdeniTranslate,
  AdeniLocaleService,
} from "@adeni/ui";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";
import { BusinessActiveLocationService } from "../../core/services/business-active-location.service";
import { PORTAL_ALL_LOCATIONS_SCOPE } from "../../core/services/business-active-location-store";
import { MessagingRealtimeService, TenantOperationsRealtimeService } from "@adeni/ui";
import { BusinessSessionService } from "../../core/services/business-session.service";
import { DashboardDataService } from "./dashboard-data.service";
import { DashboardCarbonChartComponent } from "./dashboard-carbon-chart.component";
import { clearDashboardLayout } from "./dashboard-layout-store";
import {
  dashboardLayoutToApi,
} from "./dashboard-layout-sync";
import { DashboardWidgetEditChromeComponent } from "./dashboard-widget-edit-chrome.component";
import { DashboardWidgetComponent } from "./dashboard-widget.component";

type StatusSlice = { status: number; label: string; count: number; percent: number };

@Component({
  selector: "app-dashboard",
  host: { class: "business-dashboard-page" },
  standalone: true,
  providers: [DashboardDataService],
  imports: [
    RouterLink,
    CdkDropList,
    CdkDrag,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniStaffButtonComponent,
    AdeniDashboardLayoutPresetCardComponent,
    AdeniModalComponent,
    PortalPageComponent,
    DashboardWidgetEditChromeComponent,
    DashboardWidgetComponent,
    DashboardCarbonChartComponent,
  ],
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss",
})
export class DashboardComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly uiLocale = inject(AdeniLocaleService).locale;
  private readonly businessApi = inject(BusinessApiService);
  private readonly dashboardData = inject(DashboardDataService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messagingRealtime = inject(MessagingRealtimeService);
  private readonly operationsRealtime = inject(TenantOperationsRealtimeService);
  readonly session = inject(BusinessSessionService);
  private readonly activeLocation = inject(BusinessActiveLocationService);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly formatTenantStatus = formatTenantStatus;
  readonly formatSlotTime = formatSlotTime;
  readonly profile = signal<BusinessProfile | null>(null);
  readonly layout = signal<DashboardLayout>(resolveDashboardLayout(false));
  readonly editMode = signal(false);
  readonly pickerOpen = signal(false);
  readonly layoutPresetPulse = signal(false);
  readonly loadError = signal(false);
  readonly pendingCount = signal(0);
  readonly activeServices = signal(0);
  readonly revenueLabel = signal("—");
  readonly revenueHint = signal(this.tr("businessApp.dashboard.revenueHintListPrice"));
  readonly revenueUsesCollected = signal(false);
  readonly ratingLabel = signal(this.tr("businessApp.dashboard.ratingNew"));
  readonly ratingHint = signal(this.tr("businessApp.dashboard.ratingNoReviews"));
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
  readonly todayNextUpId = computed(() => {
    const now = Date.now();
    for (const booking of this.today()) {
      if (this.isBookingInProgress(booking)) {
        continue;
      }
      if (Date.parse(booking.startAt) > now) {
        return booking.id;
      }
    }
    return null;
  });
  readonly usage = signal<SubscriptionUsage | null>(null);
  readonly exportEntitlementsDirect = signal<TenantEntitlements | null>(null);
  readonly rangePreset = signal<DashboardRangePreset>("7d");
  readonly appointmentsInRange = signal(0);

  readonly rangePresetOptions = computed(() =>
    DASHBOARD_RANGE_PRESETS.map((entry) => ({
      id: entry.id,
      label: formatDashboardRangePresetLabel(this.uiLocale(), entry.id),
    })),
  );

  readonly rangeOverviewHint = computed(() =>
    formatDashboardRangeOverviewHint(this.rangePreset(), new Date(), this.uiLocale()),
  );

  readonly rangeStatHint = computed(() =>
    formatDashboardRangeStatHint(this.rangePreset(), new Date(), this.uiLocale()),
  );

  readonly categoryLine = computed(() => {
    const profile = this.profile();
    if (!profile) {
      return "";
    }
    return getCategoryLabel(this.uiLocale(), profile.categorySlug);
  });

  readonly overviewInsight = computed(() => {
    this.uiLocale();
    const pending = this.pendingCount();
    const appointments = this.appointmentsInRange();
    if (pending > 0) {
      return pending === 1
        ? this.tr("businessApp.dashboard.oneBookingRequestWaitingYou")
        : this.tr("businessApp.dashboard.bookingRequestsWaiting", { count: pending });
    }
    if (appointments > 0) {
      return appointments === 1
        ? this.tr("businessApp.dashboard.oneVisitScheduledWindow")
        : this.tr("businessApp.dashboard.visitsScheduledInWindow", { count: appointments });
    }
    return this.tr("businessApp.dashboard.noVisitsWindowYetShare");
  });

  readonly isNearBookingLimit = isNearBookingLimit;

  readonly visibleLayoutWidgets = computed(() =>
    this.layout().widgets.filter((entry) => this.widgetShown(entry.id)),
  );

  readonly layoutPresetsForPicker = computed(() =>
    this.isPractitionerView()
      ? DASHBOARD_LAYOUT_PRESETS.filter((preset) => preset.id === "practitioner")
      : DASHBOARD_LAYOUT_PRESETS.filter((preset) => preset.id !== "practitioner"),
  );

  readonly recommendedLayoutPresetId = computed(() =>
    resolveRecommendedDashboardLayoutPresetId({
      isPractitionerView: this.isPractitionerView(),
      permissionRole: this.session.permissionRole(),
    }),
  );

  readonly currentLayoutFingerprint = computed(() =>
    visibleDashboardLayoutFingerprint(this.layout(), this.widgetVisibilityContext()),
  );

  readonly addableWidgets = computed(() => {
    const layout = this.layout();
    const ctx = this.widgetVisibilityContext();
    return DASHBOARD_WIDGET_DEFINITIONS.filter(
      (definition) =>
        isDashboardWidgetVisible(definition.id, ctx)
        && !this.layoutOccupiesVisibleWidget(layout, definition.id),
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
    void this.session.ensureLoaded();
    void this.load();
    this.activeLocation.selectionChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        void this.activeLocation.runOnBranchSwitch(() => this.load());
      });
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
      entitlements: this.exportEntitlements(),
      capabilities: resolveDashboardCapabilities(this.profile()),
      canBusiness: (permission: Parameters<BusinessSessionService["can"]>[0]) =>
        this.session.can(permission)
        || (permission === "business.bookings" && this.session.can("business.bookings.self")),
    };
  }

  tenantMarketId(): string | null {
    const profile = this.profile();
    if (!profile) {
      return null;
    }
    const location = this.activeLocation.resolveContextLocation(profile);
    if (!location && this.activeLocation.selectedLocationId() === PORTAL_ALL_LOCATIONS_SCOPE) {
      const fallback = profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];
      return fallback?.marketId ?? null;
    }
    return location?.marketId ?? null;
  }

  marketLocale(): string {
    const profile = this.profile();
    if (!profile) {
      return defaultLocale;
    }
    const languages = getMarketById(this.tenantMarketId() ?? "")?.languages;
    const primary = languages?.[0];
    return primary === "fr" ? "fr" : defaultLocale;
  }

  presetUiCopy(key: "recommended" | "currentLayout" | "emptyPlan" | "useLayout", vars?: Record<string, string>): string {
    const messageKeys: Record<typeof key, string> = {
      recommended: "businessApp.dashboard.presets.recommended",
      currentLayout: "businessApp.dashboard.currentLayout",
      emptyPlan: "businessApp.dashboard.presets.emptyPlan",
      useLayout: "businessApp.dashboard.useLayout",
    };
    const translated = t(this.marketLocale(), messageKeys[key], vars);
    if (translated !== messageKeys[key]) {
      return translated;
    }
    const fallbacks: Record<typeof key, string> = {
      recommended: this.tr("businessApp.dashboard.recommendedYou"),
      currentLayout: this.tr("businessApp.dashboard.currentLayout"),
      emptyPlan: this.tr("businessApp.dashboard.noWidgetsPlanTemplate"),
      useLayout: this.tr("businessApp.dashboard.useLayout"),
    };
    let fallback = fallbacks[key];
    if (vars) {
      for (const [name, value] of Object.entries(vars)) {
        fallback = fallback.replaceAll(`{${name}}`, String(value));
      }
    }
    return fallback;
  }

  layoutVisibleForTenant(layout: DashboardLayout): DashboardLayout {
    return filterDashboardLayoutByVisibility(cloneDashboardLayout(layout), this.widgetVisibilityContext());
  }

  presetPreviewLayout(preset: DashboardLayoutPreset): DashboardLayout {
    return this.layoutVisibleForTenant(preset.layout);
  }

  presetRangeLabel(preset: DashboardLayoutPreset): string {
    return formatDashboardRangePresetLabel(this.uiLocale(), preset.layout.range);
  }

  presetPreviewWidgetTitles(preset: DashboardLayoutPreset): string[] {
    return this.presetPreviewLayout(preset).widgets.map((entry) =>
      formatDashboardWidgetTitle(this.uiLocale(), entry.id),
    );
  }

  widgetTitle(widgetId: DashboardWidgetId): string {
    return formatDashboardWidgetTitle(this.uiLocale(), widgetId);
  }

  widgetSummary(widgetId: DashboardWidgetId): string {
    return formatDashboardWidgetSummary(this.uiLocale(), widgetId);
  }

  bookingUsageText(usage: SubscriptionUsage): string {
    return formatBookingUsage(usage, this.uiLocale());
  }

  presetWireframeBlocks(preset: DashboardLayoutPreset): { w: number }[] {
    return this.presetPreviewLayout(preset).widgets.map((entry) => ({ w: entry.w }));
  }

  presetDisplayCopy(preset: DashboardLayoutPreset) {
    return getDashboardLayoutPresetDisplayCopy(
      this.marketLocale(),
      preset.id,
      this.tenantMarketId(),
      { focus: preset.focus, description: preset.description },
    );
  }

  presetMetaLine(preset: DashboardLayoutPreset): string {
    const count = this.presetPreviewWidgetTitles(preset).length;
    const rangeLabel = this.presetRangeLabel(preset);
    const widgetsKey =
      count === 1
        ? "businessApp.dashboard.presets.widgetsOnPlan"
        : "businessApp.dashboard.presets.widgetsOnPlan_plural";
    const widgetsFallback =
      count === 1 ? `${count} widget on your plan` : `${count} widgets on your plan`;
    const widgetsPart = t(this.marketLocale(), widgetsKey, { count });
    const rangePart = t(this.marketLocale(), "businessApp.dashboard.presets.rangeMeta", { range: rangeLabel });
    const resolvedWidgets = widgetsPart === widgetsKey ? widgetsFallback : widgetsPart;
    const resolvedRange =
      rangePart === "businessApp.dashboard.presets.rangeMeta" ? `${rangeLabel} range` : rangePart;
    return `${resolvedRange} · ${resolvedWidgets}`;
  }

  isPresetActive(preset: DashboardLayoutPreset): boolean {
    const preview = this.presetPreviewLayout(preset);
    return visibleDashboardLayoutFingerprint(preview, this.widgetVisibilityContext())
      === this.currentLayoutFingerprint();
  }

  isPresetRecommended(preset: DashboardLayoutPreset): boolean {
    return preset.id === this.recommendedLayoutPresetId() && !this.isPresetActive(preset);
  }

  presetCardDisabled(preset: DashboardLayoutPreset): boolean {
    return this.presetPreviewWidgetTitles(preset).length === 0;
  }

  presetCardIcon(presetId: DashboardLayoutPresetId): AdeniCarbonIconName {
    return DASHBOARD_LAYOUT_PRESET_ICONS[presetId] as AdeniCarbonIconName;
  }

  presetCtaLabel(preset: DashboardLayoutPreset): string {
    const label = this.presetUiCopy("useLayout", { label: preset.label });
    return label === "businessApp.dashboard.useLayout"
      ? this.tr("businessApp.dashboard.useLayout", { label: preset.label })
      : label;
  }

  widgetShown(id: DashboardWidgetId): boolean {
    if (!layoutIncludesWidget(this.layout(), id)) {
      return false;
    }
    return isDashboardWidgetVisible(id, this.widgetVisibilityContext());
  }

  widgetSpanClass(entry: DashboardLayoutWidget): string {
    return `widget-span-${normalizeDashboardWidgetWidth(entry.w)}`;
  }

  widgetLayoutWidth(entry: DashboardLayoutWidget): DashboardWidgetWidth {
    return normalizeDashboardWidgetWidth(entry.w);
  }

  widgetRemovable(_widgetId: DashboardWidgetId): boolean {
    return true;
  }

  setWidgetWidth(widgetId: DashboardWidgetId, width: DashboardWidgetWidth): void {
    const current = this.layout().widgets.find((entry) => entry.id === widgetId)?.w;
    if (current === width) {
      return;
    }
    this.persistLayout(updateDashboardWidgetWidth(this.layout(), widgetId, width));
  }

  primaryLocationName(): string | null {
    const profile = this.profile();
    if (!profile) return null;
    if (this.activeLocation.selectedLocationId() === PORTAL_ALL_LOCATIONS_SCOPE) {
      return this.activeLocation.switchDisplay().title;
    }
    const location = this.activeLocation.resolveContextLocation(profile);
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

  exportEntitlements() {
    return mergeTenantEntitlements(
      this.exportEntitlementsDirect(),
      this.usage()?.entitlements,
      this.profile()?.entitlements,
    );
  }

  planUsageLabel(): string {
    const usage = this.usage();
    if (!usage) return this.tr("admin.businesses.plan");
    return formatSubscriptionPlanLabel(usage, this.uiLocale());
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
    const location = this.activeLocation.resolveContextLocation(profile);
    if (!location) return null;
    return getMarketById(location.marketId)?.countryCode ?? null;
  }

  publicProfileUrl(): string | null {
    const profile = this.profile();
    if (!profile) return null;
    const location = this.activeLocation.resolveContextLocation(profile);
    if (!location) return null;
    return `${this.config.discoverWebUrl}/businesses/${location.slug}`;
  }

  isPractitionerView(): boolean {
    return this.session.can("business.staff.self") && !this.session.can("business.staff");
  }

  pageDescription(): string {
    if (this.isPractitionerView()) {
      return this.tr("businessApp.dashboard.yourScheduleBookingInboxAt");
    }
    if (this.revenueUsesCollected()) {
      return this.tr("businessApp.dashboard.planUsageAppointmentsCollectedRevenue");
    }
    return this.tr("businessApp.dashboard.planUsageAppointmentsBookedValue");
  }

  rangeLabel(): string {
    return formatDashboardRangePresetLabel(this.uiLocale(), this.rangePreset());
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
    if (next) {
      this.openPicker();
    } else {
      this.pickerOpen.set(false);
    }
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
    this.syncLayoutHiddenWidgets();
    this.pickerOpen.set(true);
  }

  /** Drop plan/capability-blocked widgets so add list matches what is on the grid. */
  private syncLayoutHiddenWidgets(): void {
    const pruned = filterDashboardLayoutByVisibility(
      this.layout(),
      this.widgetVisibilityContext(),
    );
    if (
      dashboardLayoutFingerprint(pruned)
      !== dashboardLayoutFingerprint(this.layout())
    ) {
      this.layout.set(pruned);
      void this.persistLayout(pruned);
    }
  }

  private layoutOccupiesVisibleWidget(
    layout: DashboardLayout,
    widgetId: DashboardWidgetId,
  ): boolean {
    return (
      layoutIncludesWidget(layout, widgetId)
      && isDashboardWidgetVisible(widgetId, this.widgetVisibilityContext())
    );
  }

  closePicker(): void {
    this.pickerOpen.set(false);
  }

  addWidget(widgetId: DashboardWidgetId): void {
    const width =
      widgetId === "today_schedule" || widgetId === "week_appointments"
        ? 6
        : widgetId === "pending_inbox" || widgetId === "messages_unread"
          ? 3
          : widgetId === "queue_now"
            ? 9
            : 12;
    this.pickerOpen.set(false);
    void this.persistLayout(addDashboardWidget(this.layout(), widgetId, width), true);
  }

  applyLayoutPreset(presetId: DashboardLayoutPresetId): void {
    const resolvedId =
      presetId === "practitioner" || this.isPractitionerView() ? "practitioner" : presetId;
    const layout = this.layoutVisibleForTenant(resolveDashboardLayoutPreset(resolvedId));
    this.rangePreset.set(layout.range);
    this.persistLayout(layout, true);
    this.pickerOpen.set(false);
    this.triggerLayoutPresetPulse();
  }

  private triggerLayoutPresetPulse(): void {
    this.layoutPresetPulse.set(true);
    window.setTimeout(() => this.layoutPresetPulse.set(false), 1200);
  }

  offersWalkInQueue(): boolean {
    return resolveDashboardCapabilities(this.profile()).includes("walk_in_queue");
  }

  verificationHint(): string {
    const profile = this.profile();
    if (!profile) return "";
    if (profile.status === 2) return this.tr("businessApp.dashboard.verifiedCustomersSeeYouDiscovery");
    if (profile.status === 1) return this.tr("businessApp.dashboard.pendingReviewWeLlEmail");
    return this.tr("businessApp.dashboard.completeVerificationAppearDiscovery");
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
      await this.feedback.runLoading(async () => {
      const snapshot = await this.dashboardData.load(this.rangePreset(), () => this.isPractitionerView());

      this.profile.set(snapshot.profile);
      this.exportEntitlementsDirect.set(snapshot.exportEntitlements);
      const visibility = {
        isPractitionerView: this.isPractitionerView(),
        entitlements: mergeTenantEntitlements(
          snapshot.exportEntitlements,
          snapshot.usage?.entitlements,
          snapshot.profile.entitlements,
        ),
        capabilities: resolveDashboardCapabilities(snapshot.profile),
        canBusiness: (permission: Parameters<BusinessSessionService["can"]>[0]) =>
          this.session.can(permission)
          || (permission === "business.bookings" && this.session.can("business.bookings.self")),
      };
      const normalizedLayout = filterDashboardLayoutByVisibility(snapshot.storedLayout, visibility);
      this.layout.set(normalizedLayout);
      if (
        visibleDashboardLayoutFingerprint(normalizedLayout, visibility)
        !== visibleDashboardLayoutFingerprint(snapshot.storedLayout, visibility)
      ) {
        void this.businessApi
          .withAuthorizedClient((client) =>
            client.putTenantDashboardLayout(dashboardLayoutToApi(normalizedLayout)),
          )
          .catch(() => undefined);
      }
      if (!this.editMode()) {
        this.rangePreset.set(snapshot.storedLayout.range);
      }
      this.pendingCount.set(snapshot.summary.pendingCount);
      this.appointmentsInRange.set(snapshot.summary.appointmentsCount);
      this.applyRevenueFromSummary(snapshot.summary);
      this.applyBookings(snapshot.bookings, snapshot.services, this.revenueUsesCollected());
      this.appointmentChartData.set(this.toSeriesChartData(snapshot.series?.points ?? []));
      this.collectedChartData.set(this.toSeriesChartData(snapshot.collectedSeries?.points ?? [], this.tr("businessApp.dashboard.collected")));
      this.serviceMixChartData.set(
        (snapshot.serviceBreakdown?.items ?? []).map((item: TenantDashboardBreakdown["items"][number]) => ({
          group: item.label,
          value: item.value,
        })),
      );
      this.bookedValueChartData.set(this.toSeriesChartData(snapshot.bookedSeries?.points ?? [], this.tr("businessApp.dashboard.booked")));
      this.staffLeaderboardChartData.set(
        (snapshot.staffBreakdown?.items ?? []).map((item: TenantDashboardBreakdown["items"][number]) => ({
          group: this.tr("common.bookings"),
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
        this.ratingHint.set(getReviewCountLabel(this.uiLocale(), snapshot.rating.count));
      } else {
        this.ratingLabel.set(this.tr("common.new"));
        this.ratingHint.set(this.tr("common.noReviewsYet"));
      }
      });
    } catch {
      this.loadError.set(true);
      this.profile.set(null);
    }
  }

  private persistLayout(layout: DashboardLayout, reload = false): Promise<void> {
    const normalized = filterDashboardLayoutByVisibility(layout, this.widgetVisibilityContext());
    this.layout.set(normalized);
    const tenantId = this.profile()?.tenantId;
    if (!tenantId) {
      return Promise.resolve();
    }
    return this.businessApi
      .withAuthorizedClient((client) =>
        client.putTenantDashboardLayout(dashboardLayoutToApi(normalized)),
      )
      .catch(() => undefined)
      .finally(() => {
        if (reload) {
          void this.load();
        }
      })
      .then(() => undefined);
  }

  private toSeriesChartData(
    points: { date: string; value: number }[],
    group = this.tr("businessApp.dashboard.appointments"),
  ): ChartTabularData {
    return points.slice(-21).map((point) => ({
      group,
      key: this.formatChartKey(point.date),
      value: point.value,
    }));
  }

  private formatChartKey(isoDate: string): string {
    const date = new Date(`${isoDate}T12:00:00Z`);
    const loc = this.uiLocale() === "fr" ? "fr-CA" : "en-CA";
    return date.toLocaleDateString(loc, { month: "short", day: "numeric" });
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
      this.revenueHint.set(this.tr("businessApp.dashboard.collectedOnlineIn", { range: this.rangeLabel().toLowerCase() }));
      return;
    }

    if (summary.analyticsIncluded && summary.bookedValueAmount != null) {
      this.revenueUsesCollected.set(false);
      this.revenueLabel.set(formatPrice(summary.bookedValueAmount, currency));
      this.revenueHint.set(this.tr("businessApp.dashboard.bookedListValueIn", { range: this.rangeLabel().toLowerCase() }));
      return;
    }

    this.revenueUsesCollected.set(false);
    this.revenueLabel.set("—");
    this.revenueHint.set(this.tr("businessApp.dashboard.revenueMetricsNotShownPeriod"));
  }

  isBookingInProgress(booking: BookingResponse): boolean {
    const now = Date.now();
    return now >= Date.parse(booking.startAt) && now < Date.parse(booking.endAt);
  }

  isNextUpBooking(booking: BookingResponse): boolean {
    return this.todayNextUpId() === booking.id;
  }

  bookingFocusQuery(booking: BookingResponse): { tab: string; focus: string } {
    return { tab: "upcoming", focus: booking.id };
  }

  bookingDurationLabel(booking: BookingResponse): string | null {
    const start = Date.parse(booking.startAt);
    const end = Date.parse(booking.endAt);
    if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) {
      return null;
    }
    const minutes = Math.round((end - start) / 60_000);
    if (minutes <= 0) {
      return null;
    }
    return this.tr("businessApp.dashboard.scheduleDurationMinutes", { minutes });
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
        label: formatBookingStatusLabel(this.uiLocale(), status),
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
