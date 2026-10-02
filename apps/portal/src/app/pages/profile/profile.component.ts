import { Component, inject, OnInit, signal } from "@angular/core";
import { KeyValuePipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import type { BusinessProfile, PublicReviewItem } from "@adeni/shared";
import {
  formatTenantStatus,
  resolveBusinessCoverImage,
  VERIFICATION_DOCUMENT_LABELS,
} from "@adeni/shared";
import { PortalPageComponent } from "../../shared/portal-page.component";
import { BusinessApiService } from "../../core/services/business-api.service";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";

const PHONE_PATTERN = /^\+?[0-9\s-]{7,20}$/;
const ALLOWED_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const MAX_BYTES = 5 * 1024 * 1024;

@Component({
  selector: "app-profile",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, KeyValuePipe],
  templateUrl: "./profile.component.html",
  styleUrl: "./profile.component.scss",
})
export class ProfileComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly coverPreview = signal<string>("");

  businessName = "";
  categorySlug = "";
  phone = "";
  description = "";
  autoConfirm = false;
  depositPercent = 0;

  readonly reviews = signal<PublicReviewItem[]>([]);
  readonly reviewSummary = signal<{ avg: number | null; count: number }>({ avg: null, count: 0 });
  readonly verificationMessage = signal<string | null>(null);
  verificationDocType = 0;
  verificationReference = "";

  readonly formatTenantStatus = formatTenantStatus;
  readonly verificationLabels = VERIFICATION_DOCUMENT_LABELS;

  ngOnInit(): void {
    void this.load();
  }

  canEdit(): boolean {
    const p = this.profile();
    return p ? p.status === 0 || p.status === 3 : false;
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const p = await this.api.withAuthorizedClient((c) => c.getTenantProfile());
      this.profile.set(p);
      this.businessName = p.businessName;
      this.categorySlug = p.categorySlug;
      this.phone = p.phone;
      this.description = p.description ?? "";
      this.autoConfirm = p.autoConfirmBookings ?? false;
      this.depositPercent = p.depositPercent ?? 0;
      this.coverPreview.set(resolveBusinessCoverImage(p.categorySlug, p.coverImageUrl));
      await this.loadReviews(p);
    } catch {
      this.error.set("Could not load profile.");
      this.profile.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  async saveProfile(): Promise<void> {
    if (!this.canEdit()) return;
    if (!this.businessName.trim() || !this.categorySlug.trim() || !PHONE_PATTERN.test(this.phone.trim())) {
      this.error.set("Fix required fields before saving.");
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((c) =>
        c.updateTenantProfile({
          businessName: this.businessName.trim(),
          categorySlug: this.categorySlug.trim(),
          phone: this.phone.trim(),
          description: this.description.trim(),
        }),
      );
      this.profile.set(updated);
    } catch {
      this.error.set("Could not save profile.");
    } finally {
      this.saving.set(false);
    }
  }

  async saveSettings(): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((c) =>
        c.updateTenantSettings({
          autoConfirmBookings: this.autoConfirm,
          depositPercent: this.depositPercent,
        }),
      );
      this.profile.set(updated);
    } catch {
      this.error.set("Could not save booking settings.");
    } finally {
      this.saving.set(false);
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

  async copyPublicLink(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.publicBookingUrl());
      this.verificationMessage.set("Booking link copied.");
    } catch {
      this.error.set("Could not copy link.");
    }
  }

  whatsAppShareUrl(): string {
    const p = this.profile();
    const text = encodeURIComponent(
      `Book ${p?.businessName ?? "us"} on Adeni:\n${this.publicBookingUrl()}`,
    );
    return `https://wa.me/?text=${text}`;
  }

  canSubmitVerification(): boolean {
    const p = this.profile();
    return p ? p.status === 0 || p.status === 3 : false;
  }

  async loadReviews(profile: BusinessProfile): Promise<void> {
    const slug = profile.locations.find((l) => l.isPrimary)?.slug ?? profile.locations[0]?.slug;
    if (!slug) {
      this.reviews.set([]);
      return;
    }
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const [publicProfile, payload] = await Promise.all([
          c.getBusinessProfile(slug),
          c.getBusinessReviews(slug, 1, 10),
        ]);
        this.reviews.set(payload.items);
        this.reviewSummary.set({
          avg: publicProfile.ratingAvg ?? null,
          count: publicProfile.reviewCount ?? 0,
        });
      });
    } catch {
      this.reviews.set([]);
    }
  }

  async submitVerification(): Promise<void> {
    if (!this.canSubmitVerification()) return;
    if (this.verificationReference.trim().length < 4) {
      this.error.set("Reference number must be at least 4 characters.");
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient((c) =>
        c.submitTenantVerification({
          documents: [
            {
              documentType: this.verificationDocType,
              referenceNumber: this.verificationReference.trim(),
            },
          ],
        }),
      );
      this.verificationMessage.set("Verification submitted. An admin will review your business.");
      this.verificationReference = "";
      await this.load();
    } catch {
      this.error.set("Could not submit verification.");
    } finally {
      this.saving.set(false);
    }
  }

  async onCoverSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.profile()) return;

    if (!ALLOWED_TYPES.has(file.type)) {
      this.error.set("Use JPEG, PNG, or WebP.");
      return;
    }
    if (file.size > MAX_BYTES) {
      this.error.set("Cover must be 5 MB or smaller.");
      return;
    }

    this.uploading.set(true);
    this.error.set(null);
    try {
      const url = await this.api.withAuthorizedClient(async (c) => {
        const slot = await c.createCoverUploadUrl(file.type, file.size);
        const upload = await fetch(slot.uploadUrl, {
          method: "PUT",
          headers: { "Content-Type": file.type },
          body: file,
        });
        if (!upload.ok) throw new Error("upload failed");
        return c.updateTenantCoverImage({ coverImageKey: slot.storageKey });
      });
      this.coverPreview.set(url);
      await this.load();
    } catch {
      this.error.set("Cover upload failed.");
    } finally {
      this.uploading.set(false);
      input.value = "";
    }
  }
}
