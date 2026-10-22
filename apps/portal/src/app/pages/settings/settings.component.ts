import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { map } from "rxjs";
import {
  getDefaultNotificationTemplate,
  hasCapability,
  hasTenantEntitlement,
  NOTIFICATION_LOCALES,
  notificationTemplateLabel,
  notificationLocaleDisplayName,
  PORTAL_EDITABLE_NOTIFICATION_TEMPLATE_IDS,
  type BusinessProfile,
  type NotificationLanguageMode,
  type NotificationLocale,
  type NotificationTemplateId,
  type NotificationTemplateOverride,
} from "@adeni/shared";
import { filterPortalTabs } from "../../core/portal-access";
import { findPortalNavItem } from "../../core/portal-nav";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniTabsComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-settings",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniTabsComponent,
    AdeniCarbonIconComponent,
    RouterLink,
  ],
  templateUrl: "./settings.component.html",
  styleUrl: "./settings.component.scss",
})
export class SettingsComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly tab = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? this.defaultTabId())),
    { initialValue: this.route.snapshot.queryParamMap.get("tab") ?? this.defaultTabId() },
  );

  private readonly allTabs = findPortalNavItem("/settings")?.item.tabs ?? [
    { id: "bookings", label: "Bookings" },
    { id: "notifications", label: "Notifications" },
    { id: "policies", label: "Policies" },
    { id: "messaging", label: "Messaging", entitlement: "messaging" as const },
  ];
  readonly tabs = computed(() => {
    const entitlements = this.profile()?.entitlements ?? null;
    const base = filterPortalTabs(this.allTabs, entitlements);
    if (hasCapability(this.profile()?.capabilities, "walk_in_queue")) {
      return [...base, { id: "walk-ins", label: "Walk-ins" }];
    }
    return base;
  });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notificationsError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly profile = signal<BusinessProfile | null>(null);

  readonly smsEntitled = computed(() => this.profile()?.entitlements?.smsEnabled === true);
  readonly templateOverridesEntitled = computed(
    () => this.profile()?.entitlements?.configurableReminders === true,
  );
  readonly localeOptions = NOTIFICATION_LOCALES;
  readonly editableTemplateIds = PORTAL_EDITABLE_NOTIFICATION_TEMPLATE_IDS;

  defaultNotificationLocale: NotificationLocale = "en";
  secondaryNotificationLocale: NotificationLocale = "fr";
  notificationLanguageMode: NotificationLanguageMode = "single";
  templateOverrideEditLocale: NotificationLocale = "en";
  templateOverrides: NotificationTemplateOverride[] = [];

  autoConfirm = false;
  depositPercent = 0;

  emailEnabled = true;
  pushEnabled = true;
  smsRemindersEnabled = false;

  policyBooking = "";
  policyPayment = "";
  policyCancellation = "";
  policyTerms = "";
  requireAcceptance = false;

  faqAutoResponderEnabled = false;

  walkInsEnabled = true;
  maxWalkInWaiting: number | null = null;
  walkInCalendarPolicy: "relaxed" | "strict" = "relaxed";

  ngOnInit(): void {
    const routeTab = this.route.snapshot.data["settingsTab"] as string | undefined;
    if (routeTab && !this.route.snapshot.queryParamMap.get("tab")) {
      void this.router.navigate(["/settings"], {
        queryParams: { tab: routeTab },
        replaceUrl: true,
      });
    }
    void this.load();
  }

  private defaultTabId(): string {
    const fromData = this.route.snapshot.data["settingsTab"];
    return typeof fromData === "string" ? fromData : "bookings";
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (client) => {
          const profile = await client.getTenantProfile();
          this.profile.set(profile);
          this.autoConfirm = profile.autoConfirmBookings ?? false;
          this.depositPercent = profile.depositPercent ?? 0;
          this.policyBooking = profile.policies?.booking ?? "";
          this.policyPayment = profile.policies?.payment ?? "";
          this.policyCancellation = profile.policies?.cancellation ?? "";
          this.policyTerms = profile.policies?.terms ?? "";
          this.requireAcceptance = profile.policies?.requireAcceptance ?? false;
          try {
            const prefs = await client.getNotificationPreferences();
            this.emailEnabled = prefs.emailEnabled;
            this.pushEnabled = prefs.pushEnabled;
            this.smsRemindersEnabled = prefs.smsRemindersEnabled;
            this.defaultNotificationLocale = prefs.defaultNotificationLocale ?? "en";
            this.secondaryNotificationLocale =
              prefs.secondaryNotificationLocale ??
              (this.defaultNotificationLocale === "en" ? "fr" : "en");
            this.notificationLanguageMode = prefs.notificationLanguageMode ?? "single";
            this.syncTemplateOverrideEditLocale();
            this.templateOverrides = [...(prefs.templateOverrides ?? [])];
            this.ensureEditableTemplateRows();
            this.notificationsError.set(null);
          } catch {
            this.notificationsError.set("Could not load notification preferences.");
          }

          if (hasTenantEntitlement(profile.entitlements, "messaging")) {
            const messaging = await client.getMessagingSettings();
            this.faqAutoResponderEnabled = messaging.faqAutoResponderEnabled;
          }
          if (profile.walkInSettings) {
            this.walkInsEnabled = profile.walkInSettings.walkInsEnabled;
            this.maxWalkInWaiting = profile.walkInSettings.maxWalkInWaiting ?? null;
            this.walkInCalendarPolicy = profile.walkInSettings.calendarPolicy;
          }
        });
      });
    } catch {
      this.error.set("Could not load settings.");
    } finally {
      this.loading.set(false);
    }
  }

  async saveBookings(): Promise<void> {
    this.saving.set(true);
    try {
      await this.feedback.runLoading(async () => {
        const profile = await this.api.withAuthorizedClient((c) =>
          c.updateTenantSettings({
            autoConfirmBookings: this.autoConfirm,
            depositPercent: Math.min(100, Math.max(0, Math.round(this.depositPercent) || 0)),
          }),
        );
        this.profile.set(profile);
        this.feedback.success("Booking settings saved");
      });
    } catch {
      this.feedback.error("Could not save booking settings");
    } finally {
      this.saving.set(false);
    }
  }

  async saveNotifications(): Promise<void> {
    this.saving.set(true);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient((c) =>
          c.updateNotificationPreferences({
            emailEnabled: this.emailEnabled,
            pushEnabled: this.pushEnabled,
            smsRemindersEnabled: this.smsEntitled()
              ? this.smsRemindersEnabled
              : false,
            ...this.notificationLocalePrefsPayload(),
            templateOverrides: this.templateOverridesEntitled()
              ? this.templateOverrides.filter((row) => this.isPersistedOverride(row))
              : [],
          }),
        );
        this.feedback.success("Notification preferences saved");
      });
    } catch {
      this.feedback.error("Could not save notification preferences");
    } finally {
      this.saving.set(false);
    }
  }

  async savePolicies(): Promise<void> {
    this.saving.set(true);
    try {
      await this.feedback.runLoading(async () => {
        const profile = await this.api.withAuthorizedClient((c) =>
          c.updateTenantPolicies({
            booking: this.policyBooking.trim() || null,
            payment: this.policyPayment.trim() || null,
            cancellation: this.policyCancellation.trim() || null,
            terms: this.policyTerms.trim() || null,
            requireAcceptance: this.requireAcceptance,
          }),
        );
        this.profile.set(profile);
        this.feedback.success("Policies saved");
      });
    } catch {
      this.feedback.error("Could not save policies");
    } finally {
      this.saving.set(false);
    }
  }

  async saveWalkIns(): Promise<void> {
    this.saving.set(true);
    try {
      await this.feedback.runLoading(async () => {
        const settings = await this.api.withAuthorizedClient((c) =>
          c.updateWalkInSettings({
            walkInsEnabled: this.walkInsEnabled,
            maxWalkInWaiting: this.maxWalkInWaiting,
            calendarPolicy: this.walkInCalendarPolicy,
          }),
        );
        this.walkInsEnabled = settings.walkInsEnabled;
        this.maxWalkInWaiting = settings.maxWalkInWaiting ?? null;
        this.walkInCalendarPolicy = settings.calendarPolicy;
        this.feedback.success("Walk-in settings saved");
      });
    } catch {
      this.feedback.error("Could not save walk-in settings");
    } finally {
      this.saving.set(false);
    }
  }

  templateLabel(id: NotificationTemplateId): string {
    return notificationTemplateLabel(id, this.defaultNotificationLocale);
  }

  overrideFor(templateId: NotificationTemplateId): NotificationTemplateOverride {
    const locale = this.templateOverrideEditLocale;
    const existing = this.templateOverrides.find(
      (row) => row.templateId === templateId && row.locale === locale,
    );
    if (existing) {
      this.prefillOverrideFromCatalogIfEmpty(existing);
      return existing;
    }

    const created = this.createOverrideRow(templateId, locale);
    this.templateOverrides = [...this.templateOverrides, created];
    return created;
  }

  localeLabel(locale: NotificationLocale): string {
    return notificationLocaleDisplayName(locale, "label");
  }

  onNotificationLanguageModeChange(): void {
    if (this.notificationLanguageMode === "single") {
      this.syncTemplateOverrideEditLocale();
      return;
    }

    if (this.secondaryNotificationLocale === this.defaultNotificationLocale) {
      this.secondaryNotificationLocale = this.pickDistinctLocale(this.defaultNotificationLocale);
    }
    this.syncTemplateOverrideEditLocale();
    this.ensureEditableTemplateRows();
  }

  onPrimaryNotificationLocaleChange(): void {
    if (
      this.notificationLanguageMode === "bilingual" &&
      this.secondaryNotificationLocale === this.defaultNotificationLocale
    ) {
      this.secondaryNotificationLocale = this.pickDistinctLocale(this.defaultNotificationLocale);
    }
    this.syncTemplateOverrideEditLocale();
    this.ensureEditableTemplateRows();
  }

  onSecondaryNotificationLocaleChange(): void {
    if (this.secondaryNotificationLocale === this.defaultNotificationLocale) {
      this.secondaryNotificationLocale = this.pickDistinctLocale(this.defaultNotificationLocale);
    }
    this.ensureEditableTemplateRows();
  }

  onTemplateOverrideEditLocaleChange(): void {
    this.ensureEditableTemplateRows();
  }

  templateOverrideLocaleOptions(): NotificationLocale[] {
    if (this.notificationLanguageMode === "bilingual") {
      return [this.defaultNotificationLocale, this.secondaryNotificationLocale];
    }

    return [this.defaultNotificationLocale];
  }

  private ensureEditableTemplateRows(): void {
    for (const templateId of this.editableTemplateIds) {
      this.overrideFor(templateId);
    }
  }

  private syncTemplateOverrideEditLocale(): void {
    const options = this.templateOverrideLocaleOptions();
    if (!options.includes(this.templateOverrideEditLocale)) {
      this.templateOverrideEditLocale = options[0] ?? this.defaultNotificationLocale;
    }
  }

  private notificationLocalePrefsPayload(): {
    defaultNotificationLocale: NotificationLocale;
    secondaryNotificationLocale?: NotificationLocale;
    notificationLanguageMode: NotificationLanguageMode;
  } {
    if (this.notificationLanguageMode === "bilingual") {
      return {
        defaultNotificationLocale: this.defaultNotificationLocale,
        secondaryNotificationLocale: this.secondaryNotificationLocale,
        notificationLanguageMode: "bilingual",
      };
    }

    return {
      defaultNotificationLocale: this.defaultNotificationLocale,
      notificationLanguageMode: "single",
    };
  }

  private pickDistinctLocale(exclude: NotificationLocale): NotificationLocale {
    const alternate = this.localeOptions.find((entry) => entry.id !== exclude);
    return alternate?.id ?? (exclude === "en" ? "fr" : "en");
  }

  private createOverrideRow(
    templateId: NotificationTemplateId,
    locale: NotificationLocale,
  ): NotificationTemplateOverride {
    const defaults = getDefaultNotificationTemplate(templateId, locale);
    return {
      templateId,
      locale,
      subject: defaults?.subject ?? "",
      body: defaults?.body ?? "",
    };
  }

  private prefillOverrideFromCatalogIfEmpty(row: NotificationTemplateOverride): void {
    const defaults = getDefaultNotificationTemplate(row.templateId, row.locale);
    if (!defaults) {
      return;
    }

    if (!row.subject?.trim()) {
      row.subject = defaults.subject;
    }
    if (!row.body?.trim()) {
      row.body = defaults.body;
    }
  }

  private isPersistedOverride(row: NotificationTemplateOverride): boolean {
    const subject = row.subject?.trim() ?? "";
    const body = row.body?.trim() ?? "";
    if (!subject && !body) {
      return false;
    }

    const defaults = getDefaultNotificationTemplate(row.templateId, row.locale);
    if (!defaults) {
      return Boolean(subject || body);
    }

    return subject !== defaults.subject.trim() || body !== defaults.body.trim();
  }

  async saveMessaging(): Promise<void> {
    if (!hasTenantEntitlement(this.profile()?.entitlements, "messaging")) {
      return;
    }
    this.saving.set(true);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient((c) =>
          c.updateMessagingSettings({
            faqAutoResponderEnabled: this.faqAutoResponderEnabled,
          }),
        );
        this.feedback.success("Messaging settings saved");
      });
    } catch {
      this.feedback.error("Could not save messaging settings");
    } finally {
      this.saving.set(false);
    }
  }
}
