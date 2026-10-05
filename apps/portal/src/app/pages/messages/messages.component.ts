import { Component, inject, OnInit, signal } from "@angular/core";
import type { BusinessProfile } from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";

@Component({
  selector: "app-messages",
  standalone: true,
  imports: [PortalPageComponent],
  templateUrl: "./messages.component.html",
  styleUrl: "./messages.component.scss",
})
export class MessagesComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly notice = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const p = await this.api.withAuthorizedClient((c) => c.getTenantProfile());
      this.profile.set(p);
    } catch {
      this.error.set("Could not load business profile.");
      this.profile.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  primarySlug(): string | null {
    const p = this.profile();
    if (!p) return null;
    const primary = p.locations.find((l) => l.isPrimary) ?? p.locations[0];
    return primary?.slug ?? null;
  }

  publicBookingUrl(): string {
    const slug = this.primarySlug();
    if (!slug) return this.config.discoverWebUrl;
    return `${this.config.discoverWebUrl}/businesses/${slug}`;
  }

  whatsAppUrl(message: string): string {
    return `https://wa.me/?text=${encodeURIComponent(message)}`;
  }

  templateConfirm(): string {
    const name = this.profile()?.businessName ?? "your business";
    return `Hi! Your booking with ${name} is confirmed. Details: ${this.publicBookingUrl()}`;
  }

  templateReminder(): string {
    const name = this.profile()?.businessName ?? "your business";
    return `Reminder from ${name}: we look forward to seeing you. Manage booking: ${this.publicBookingUrl()}`;
  }

  templateFollowUp(): string {
    const name = this.profile()?.businessName ?? "your business";
    return `Thanks for visiting ${name}! Book again anytime: ${this.publicBookingUrl()}`;
  }

  async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.notice.set("Copied to clipboard.");
    } catch {
      this.error.set("Could not copy.");
    }
  }
}
