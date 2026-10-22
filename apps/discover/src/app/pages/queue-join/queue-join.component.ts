import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { AdeniApiClient } from "@adeni/api-client";
import {
  combineGuestName,
  getLocaleOption,
  optionalEmail,
  t,
  type LocaleId,
  type PublicBusinessProfile,
  type QueueTicketResponse,
  type ServiceOffering,
} from "@adeni/shared";
import { AdeniLocaleService, injectAdeniTranslate } from "@adeni/ui";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";

@Component({
  selector: "app-queue-join",
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: "./queue-join.component.html",
  styleUrl: "./queue-join.component.scss",
})
export class QueueJoinComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly route = inject(ActivatedRoute);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly localeService = inject(AdeniLocaleService);

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly business = signal<PublicBusinessProfile | null>(null);
  readonly services = signal<ServiceOffering[]>([]);
  readonly ticket = signal<QueueTicketResponse | null>(null);
  readonly kioskMode = signal(false);
  readonly locale = this.localeService.locale;

  logoUrl(): string | null {
    return this.business()?.publicPage?.logoImageUrl ?? null;
  }

  initials(name: string): string {
    const parts = name.split(/\s+/).filter(Boolean).slice(0, 2);
    if (parts.length === 0) {
      return "?";
    }
    return parts.map((part) => part[0]?.toUpperCase() ?? "").join("");
  }

  label(key: string, vars?: Record<string, string | number>): string {
    return t(this.locale(), key, vars);
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

  firstName = "";
  lastName = "";
  phone = "";
  email = "";
  serviceOfferingId = "";

  ngOnInit(): void {
    const kiosk = this.route.snapshot.queryParamMap.get("kiosk");
    this.kioskMode.set(kiosk === "1" || kiosk === "true");
    void this.load();
  }

  joinAnother(): void {
    this.ticket.set(null);
    this.firstName = "";
    this.lastName = "";
    this.phone = "";
    this.email = "";
    this.serviceOfferingId = "";
    this.error.set(null);
  }

  async load(): Promise<void> {
    const slug = this.route.snapshot.paramMap.get("slug");
    if (!slug) {
      this.error.set(this.label("queueJoin.notFound"));
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    try {
      const client = new AdeniApiClient({ baseUrl: this.config.apiBaseUrl, locale: () => this.localeService.locale() });
      const [profile, services] = await Promise.all([
        client.getBusinessProfile(slug),
        client.getBusinessServices(slug),
      ]);
      this.business.set(profile);
      this.services.set(services.filter((s) => s.isActive !== false));
    } catch {
      this.error.set(this.label("queueJoin.errorLoad"));
    } finally {
      this.loading.set(false);
    }
  }

  async join(): Promise<void> {
    const slug = this.route.snapshot.paramMap.get("slug");
    const displayName = combineGuestName(this.firstName, this.lastName);
    if (!slug || !displayName || !this.phone.trim()) {
      this.error.set(this.label("queueJoin.errorRequired"));
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      const client = new AdeniApiClient({ baseUrl: this.config.apiBaseUrl, locale: () => this.localeService.locale() });
      const ticket = await client.joinPublicQueue(slug, {
        displayName,
        phone: this.phone.trim(),
        email: optionalEmail(this.email),
        serviceOfferingId: this.serviceOfferingId || undefined,
        partySize: 1,
      });
      this.ticket.set(ticket);
    } catch {
      this.error.set(this.label("queueJoin.errorJoin"));
    } finally {
      this.submitting.set(false);
    }
  }
}
