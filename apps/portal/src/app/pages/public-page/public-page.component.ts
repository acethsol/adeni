import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { BusinessProfile, PublicPageTemplateId } from "@adeni/shared";
import {
  DEFAULT_PUBLIC_PAGE_CONFIG,
  PUBLIC_PAGE_TEMPLATES,
  resolvePublicPageTemplateId,
} from "@adeni/shared";
import { AdeniFeedbackService, PortalPageComponent } from "@adeni/ui";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";

type PublicPageDraft = {
  templateId: PublicPageTemplateId;
  accentColor: string;
  useAccent: boolean;
  showAbout: boolean;
  showServices: boolean;
  showReviews: boolean;
  showVisit: boolean;
  showBook: boolean;
  showPolicies: boolean;
  policyBooking: string;
  policyPayment: string;
  policyCancellation: string;
  policyTerms: string;
  requirePolicyAcceptance: boolean;
};

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
  private readonly feedback = inject(AdeniFeedbackService);

  readonly templates = PUBLIC_PAGE_TEMPLATES;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly profile = signal<BusinessProfile | null>(null);
  private readonly savedSnapshot = signal("");
  private readonly formTick = signal(0);

  templateId: PublicPageTemplateId = "studio";
  accentColor = "#0F766E";
  useAccent = false;
  showAbout = true;
  showServices = true;
  showReviews = true;
  showVisit = true;
  showBook = true;
  showPolicies = false;

  policyBooking = "";
  policyPayment = "";
  policyCancellation = "";
  policyTerms = "";
  requirePolicyAcceptance = false;

  readonly dirty = computed(() => {
    this.formTick();
    return this.serializeDraft() !== this.savedSnapshot();
  });

  readonly canSave = computed(() => this.dirty() && !this.saving() && !this.loading());

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

  markDirty(): void {
    this.formTick.update((n) => n + 1);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      await this.feedback.runLoading(async () => {
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
        this.showBook = page.sections.book ?? true;
        this.showPolicies = page.sections.policies ?? false;
        const policies = p.policies;
        this.policyBooking = policies?.booking ?? "";
        this.policyPayment = policies?.payment ?? "";
        this.policyCancellation = policies?.cancellation ?? "";
        this.policyTerms = policies?.terms ?? "";
        this.requirePolicyAcceptance = policies?.requireAcceptance ?? false;
        this.captureSnapshot();
      }, "Loading public page…", "Fetching template and policies");
    } catch {
      this.profile.set(null);
      this.feedback.error("Could not load public page settings.");
    } finally {
      this.loading.set(false);
    }
  }

  selectTemplate(id: PublicPageTemplateId): void {
    if (this.templateId === id) {
      return;
    }
    this.templateId = id;
    this.markDirty();
  }

  async save(): Promise<void> {
    if (!this.dirty()) {
      this.feedback.info("Nothing to save — no changes yet.");
      return;
    }

    if (!this.showAbout && !this.showServices && !this.showVisit) {
      this.feedback.error("Keep at least one of About, Services, or Visit visible.");
      return;
    }

    const hasPolicyContent = Boolean(
      this.policyBooking.trim() ||
        this.policyPayment.trim() ||
        this.policyCancellation.trim() ||
        this.policyTerms.trim(),
    );
    if (hasPolicyContent) {
      this.showPolicies = true;
    }

    this.saving.set(true);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          await c.updateTenantPolicies({
            booking: this.policyBooking.trim() || null,
            payment: this.policyPayment.trim() || null,
            cancellation: this.policyCancellation.trim() || null,
            terms: this.policyTerms.trim() || null,
            requireAcceptance: this.requirePolicyAcceptance,
          });
          const updated = await c.updateTenantPublicPage({
            templateId: this.templateId,
            accentColor: this.useAccent ? this.accentColor : null,
            sections: {
              about: this.showAbout,
              services: this.showServices,
              reviews: this.showReviews,
              visit: this.showVisit,
              book: this.showBook,
              policies: this.showPolicies,
            },
          });
          this.profile.set(updated);
        });
      }, "Saving…", "Updating template, sections, and policies");

      this.captureSnapshot();
      this.feedback.success(
        this.showBook
          ? "Template, sections, and policies are live on your booking page."
          : "Saved. Online booking is off — customers can still view your page.",
        "Public page saved",
      );
    } catch (err) {
      this.feedback.error(err instanceof Error ? err.message : "Could not save public page settings.");
    } finally {
      this.saving.set(false);
    }
  }

  private captureSnapshot(): void {
    this.savedSnapshot.set(this.serializeDraft());
    this.formTick.update((n) => n + 1);
  }

  private serializeDraft(): string {
    const draft: PublicPageDraft = {
      templateId: this.templateId,
      accentColor: this.useAccent ? this.accentColor.trim().toUpperCase() : "",
      useAccent: this.useAccent,
      showAbout: this.showAbout,
      showServices: this.showServices,
      showReviews: this.showReviews,
      showVisit: this.showVisit,
      showBook: this.showBook,
      showPolicies: this.showPolicies,
      policyBooking: this.policyBooking.trim(),
      policyPayment: this.policyPayment.trim(),
      policyCancellation: this.policyCancellation.trim(),
      policyTerms: this.policyTerms.trim(),
      requirePolicyAcceptance: this.requirePolicyAcceptance,
    };
    return JSON.stringify(draft);
  }
}
