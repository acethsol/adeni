import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { map } from "rxjs";
import { hasCapability, hasTenantEntitlement, type BusinessProfile } from "@adeni/shared";
import { filterPortalTabs } from "../../core/portal-access";
import { findPortalNavItem } from "../../core/portal-nav";
import { AdeniCarbonIconComponent, AdeniFeedbackService, PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalTabsComponent } from "../../shared/portal-tabs.component";

@Component({
  selector: "app-settings",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    PortalTabsComponent,
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

  readonly tab = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? "bookings")),
    { initialValue: this.route.snapshot.queryParamMap.get("tab") ?? "bookings" },
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
  readonly saving = signal(false);
  readonly profile = signal<BusinessProfile | null>(null);

  autoConfirm = false;
  depositPercent = 0;

  emailEnabled = true;
  pushEnabled = true;
  smsWhatsAppReminderEnabled = false;

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
    void this.load();
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
          const prefs = await client.getNotificationPreferences();
          this.emailEnabled = prefs.emailEnabled;
          this.pushEnabled = prefs.pushEnabled;
          this.smsWhatsAppReminderEnabled = prefs.smsWhatsAppReminderEnabled;

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
            smsWhatsAppReminderEnabled: this.smsWhatsAppReminderEnabled,
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
