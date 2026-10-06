import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { BusinessProfile, PublicPageTemplateId, UpdatePublicPageRequest } from "@adeni/shared";
import {
  DEFAULT_PUBLIC_PAGE_CONFIG,
  PUBLIC_PAGE_TEMPLATES,
  resolvePublicPageTemplateId,
} from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-public-page",
  standalone: true,
  imports: [PortalPageComponent, FormsModule],
  templateUrl: "./public-page.component.html",
  styleUrl: "./public-page.component.scss",
})
export class PublicPageSettingsComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);

  readonly templates = PUBLIC_PAGE_TEMPLATES;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly saved = signal(false);
  readonly profile = signal<BusinessProfile | null>(null);

  templateId: PublicPageTemplateId = "studio";
  accentColor = "#0F766E";
  useAccent = false;
  showAbout = true;
  showServices = true;
  showReviews = true;
  showVisit = true;

  readonly previewSlug = computed(() => {
    const locations = this.profile()?.locations ?? [];
    const primary = locations.find((l) => l.isPrimary) ?? locations[0];
    return primary?.slug ?? null;
  });

  readonly liveUrl = computed(() => {
    const slug = this.previewSlug();
    return slug ? `${this.config.discoverWebUrl}/businesses/${slug}` : null;
  });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const p = await this.api.withAuthorizedClient((c) => c.getTenantProfile());
      this.profile.set(p);
      const page = p.publicPage ?? DEFAULT_PUBLIC_PAGE_CONFIG;
      this.templateId = resolvePublicPageTemplateId(page.templateId);
      this.useAccent = Boolean(page.accentColor);
      this.accentColor = page.accentColor ?? "#0F766E";
      this.showAbout = page.sections.about;
      this.showServices = page.sections.services;
      this.showReviews = page.sections.reviews;
      this.showVisit = page.sections.visit;
    } catch {
      this.error.set("Could not load public page settings.");
      this.profile.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  selectTemplate(id: PublicPageTemplateId): void {
    this.templateId = id;
  }

  async save(): Promise<void> {
    if (!this.showAbout && !this.showServices && !this.showVisit) {
      this.error.set("Keep at least one of About, Services, or Visit visible.");
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.saved.set(false);

    const request: UpdatePublicPageRequest = {
      templateId: this.templateId,
      accentColor: this.useAccent ? this.accentColor : null,
      sections: {
        about: this.showAbout,
        services: this.showServices,
        reviews: this.showReviews,
        visit: this.showVisit,
        book: true,
      },
    };

    try {
      const updated = await this.api.withAuthorizedClient((c) =>
        c.updateTenantPublicPage(request),
      );
      this.profile.set(updated);
      this.saved.set(true);
    } catch {
      this.error.set("Could not save public page settings.");
    } finally {
      this.saving.set(false);
    }
  }
}
