import { Component, HostListener, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";
import type { BookingResponse, KioskStatusResponse, ServiceOffering } from "@adeni/shared";
import {
  combineGuestName,
  formatSlotTime,
  getLocaleOption,
  hasCapability,
  optionalEmail,
  t,
  type LocaleId,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniDocumentTitleStrategy,
  AdeniLocaleService,
  AdeniPortalEmptyStateComponent,
  type AdeniCarbonIconName,
  injectAdeniTranslate,
} from "@adeni/ui";
import { PortalThemeService } from "../../core/portal-theme.service";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalIconTooltipDirective } from "../../shared/portal-icon-tooltip.directive";
import { PortalNavIconComponent } from "../../layout/portal-nav-icon.component";

const CONFIRMED = 1;
const CHECKED_IN = 4;

@Component({
  selector: "app-kiosk",
  standalone: true,
  imports: [
    FormsModule,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    PortalNavIconComponent,
    PortalIconTooltipDirective,
  ],
  templateUrl: "./kiosk.component.html",
  styleUrl: "./kiosk.component.scss",
})
export class KioskComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly api = inject(BusinessApiService);
  private readonly router = inject(Router);
  private readonly documentTitle = inject(AdeniDocumentTitleStrategy);
  private readonly localeService = inject(AdeniLocaleService);
  readonly portalTheme = inject(PortalThemeService);
  readonly locale = this.localeService.locale;

  readonly loading = signal(true);
  readonly checkingIn = signal<string | null>(null);
  readonly exiting = signal(false);
  readonly error = signal<string | null>(null);
  readonly status = signal<KioskStatusResponse | null>(null);
  readonly bookings = signal<BookingResponse[]>([]);
  readonly checkedIn = signal<BookingResponse | null>(null);
  readonly exitOpen = signal(false);
  readonly walkInOpen = signal(false);
  readonly businessName = signal(this.tr("portal.kiosk.checkInTitle"));
  readonly businessLogoUrl = signal<string | null>(null);
  readonly queueEnabled = signal(false);
  readonly services = signal<ServiceOffering[]>([]);

  search = "";
  exitPin = "";
  walkInFirstName = "";
  walkInLastName = "";
  walkInPhone = "";
  walkInEmail = "";
  walkInServiceOfferingId = "";

  label(key: string, vars?: Record<string, string | number>): string {
    return t(this.locale(), key, vars);
  }

  slotTime(iso: string): string {
    return formatSlotTime(iso, this.locale());
  }

  localeToggleLabel(): string {
    return this.label("kiosk.language", {
      language: getLocaleOption(this.locale()).nativeLabel,
    });
  }

  cycleLocale(): void {
    const options = this.localeService.locales;
    const index = options.findIndex((option) => option.id === this.locale());
    const next = options[(index + 1) % options.length]?.id ?? ("en" satisfies LocaleId);
    this.localeService.setLocale(next);
  }

  themeAriaLabel(): string {
    return this.portalTheme.theme() === "dark"
      ? this.label("kiosk.themeLight")
      : this.label("kiosk.themeDark");
  }

  ngOnInit(): void {
    void this.bootstrap();
  }

  @HostListener("document:keydown.escape")
  onEscape(): void {
    this.openExit();
  }

  async bootstrap(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [status, profile, bookings, services] = await Promise.all([
          client.getTenantKioskStatus(),
          client.getTenantProfile(),
          client.getTenantBookings(),
          client.getTenantServices(),
        ]);
        this.status.set(status);
        const name = profile.businessName?.trim() || this.tr("portal.queue.check");
        this.businessName.set(name);
        this.documentTitle.setAppTitle(name);
        this.businessLogoUrl.set(profile.publicPage?.logoImageUrl ?? null);
        this.queueEnabled.set(
          status.queueEnabled || hasCapability(profile.capabilities, "walk_in_queue"),
        );
        this.bookings.set(bookings);
        this.services.set(services.filter((service) => service.isActive !== false));
      });
    } catch {
      this.error.set(this.label("kiosk.errorOpen"));
    } finally {
      this.loading.set(false);
    }
  }

  arrivals(): BookingResponse[] {
    const q = this.search.trim().toLowerCase();
    const today = startOfLocalDay();
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);

    return this.bookings()
      .filter((b) => b.status === CONFIRMED || b.status === CHECKED_IN)
      .filter((b) => {
        const start = new Date(b.startAt);
        return start >= today && start < tomorrow;
      })
      .filter((b) => {
        if (!q) return true;
        const hay = [
          b.customerName,
          b.customerPhoneMasked,
          b.serviceName,
          b.staffDisplayName,
          ...(b.guests?.map((g) => g.displayName) ?? []),
        ]
          .filter(Boolean)
          .join(" ")
          .toLowerCase();
        return hay.includes(q);
      })
      .sort((a, b) => +new Date(a.startAt) - +new Date(b.startAt));
  }

  clientLabel(booking: BookingResponse): string {
    return (
      booking.customerName?.trim() ||
      booking.guests?.find((g) => g.displayName?.trim())?.displayName?.trim() ||
      this.label("kiosk.guestFallback")
    );
  }

  businessInitials(): string {
    const parts = this.businessName()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2);
    if (parts.length === 0) {
      return "?";
    }
    return parts.map((part) => part[0]?.toUpperCase() ?? "").join("");
  }

  arrivalStatusView(status: number): {
    label: string;
    icon: AdeniCarbonIconName;
    tone: "scheduled" | "floor";
  } {
    if (status === CHECKED_IN) {
      return {
        label: this.label("kiosk.statusFloor"),
        icon: "user--filled",
        tone: "floor",
      };
    }
    return {
      label: this.label("kiosk.statusScheduled"),
      icon: "calendar",
      tone: "scheduled",
    };
  }

  async checkIn(booking: BookingResponse): Promise<void> {
    if (booking.status === CHECKED_IN) {
      this.checkedIn.set(booking);
      return;
    }
    this.checkingIn.set(booking.id);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) =>
        client.checkInTenantBooking(booking.id),
      );
      this.bookings.update((list) =>
        list.map((item) => (item.id === booking.id ? updated : item)),
      );
      this.checkedIn.set(updated);
    } catch {
      this.error.set(this.label("kiosk.errorCheckIn"));
    } finally {
      this.checkingIn.set(null);
    }
  }

  nextGuest(): void {
    this.checkedIn.set(null);
    this.search = "";
    this.error.set(null);
  }

  openExit(): void {
    this.exitOpen.set(true);
    this.exitPin = "";
    this.error.set(null);
  }

  cancelExit(): void {
    this.exitOpen.set(false);
    this.exitPin = "";
  }

  async confirmExit(): Promise<void> {
    const hasPin = this.status()?.hasPin === true;
    if (!hasPin) {
      if (!window.confirm(this.label("kiosk.confirmLeave"))) {
        return;
      }
      await this.router.navigateByUrl("/bookings?tab=upcoming");
      return;
    }

    if (!/^\d{4,8}$/.test(this.exitPin.trim())) {
      this.error.set(this.label("kiosk.errorPin"));
      return;
    }

    this.exiting.set(true);
    this.error.set(null);
    try {
      const result = await this.api.withAuthorizedClient((client) =>
        client.verifyTenantKioskPin(this.exitPin.trim()),
      );
      if (!result.ok) {
        this.error.set(this.label("kiosk.errorPinWrong"));
        return;
      }
      await this.router.navigateByUrl("/bookings?tab=upcoming");
    } catch {
      this.error.set(this.label("kiosk.errorPinVerify"));
    } finally {
      this.exiting.set(false);
    }
  }

  async addWalkIn(): Promise<void> {
    const displayName = combineGuestName(this.walkInFirstName, this.walkInLastName);
    if (!displayName || !this.walkInPhone.trim()) {
      this.error.set(this.label("kiosk.errorWalkInRequired"));
      return;
    }
    this.checkingIn.set("walk-in");
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient((client) =>
        client.addTenantQueueTicket({
          displayName,
          phone: this.walkInPhone.trim(),
          email: optionalEmail(this.walkInEmail),
          serviceOfferingId: this.walkInServiceOfferingId || undefined,
          partySize: 1,
        }),
      );
      this.walkInFirstName = "";
      this.walkInLastName = "";
      this.walkInPhone = "";
      this.walkInEmail = "";
      this.walkInServiceOfferingId = "";
      this.walkInOpen.set(false);
    } catch {
      this.error.set(this.label("kiosk.errorWalkIn"));
    } finally {
      this.checkingIn.set(null);
    }
  }
}

function startOfLocalDay(): Date {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  return d;
}
