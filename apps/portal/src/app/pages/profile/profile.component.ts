import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { KeyValuePipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { map } from "rxjs";
import type { BusinessProfile, Category, GalleryImage, PublicReviewItem } from "@adeni/shared";
import {
  formatTenantStatus,
  getCategoryLabel,
  MAX_GALLERY_PHOTOS,
  MAX_UPLOAD_BYTES,
  resolveBusinessCoverImage,
  VERIFICATION_DOCUMENT_LABELS,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  AdeniTabsComponent,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";

const PHONE_PATTERN = /^\+?[0-9\s-]{7,20}$/;
const ALLOWED_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const MAX_BYTES = MAX_UPLOAD_BYTES;

@Component({
  selector: "app-profile",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    KeyValuePipe,
    AdeniTabsComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    RouterLink,
  ],
  templateUrl: "./profile.component.html",
  styleUrl: "./profile.component.scss",
})
export class ProfileComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly route = inject(ActivatedRoute);
  readonly tab = toSignal(
    this.route.queryParamMap.pipe(
      map((params) => {
        const value = params.get("tab") ?? "overview";
        return value === "details" ? "overview" : value;
      }),
    ),
    {
      initialValue: (() => {
        const value = this.route.snapshot.queryParamMap.get("tab") ?? "overview";
        return value === "details" ? "overview" : value;
      })(),
    },
  );
  readonly tabs = [
    { id: "overview", label: "Overview" },
    { id: "business", label: "Business details" },
    { id: "reviews", label: "Reviews" },
    { id: "verification", label: "Verification" },
  ];

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly galleryUploading = signal(false);
  readonly coverPreview = signal<string>("");
  readonly galleryImages = signal<GalleryImage[]>([]);
  readonly maxGalleryPhotos = MAX_GALLERY_PHOTOS;

  businessName = "";
  categorySlug = "";
  additionalCategorySlugs: string[] = [];
  phone = "";
  description = "";
  readonly categories = signal<Category[]>([]);
  readonly categoryLabel = getCategoryLabel;
  readonly additionalOptions = computed(() =>
    this.categories().filter((c) => c.slug !== this.categorySlug),
  );
  readonly reviews = signal<PublicReviewItem[]>([]);
  readonly reviewSummary = signal<{ avg: number | null; count: number }>({ avg: null, count: 0 });
  readonly replyingId = signal<string | null>(null);
  replyDraft = "";
  readonly verificationMessage = signal<string | null>(null);
  readonly shareNotice = signal<string | null>(null);
  verificationDocType = 0;
  verificationReference = "";

  readonly formatTenantStatus = formatTenantStatus;
  readonly verificationLabels = VERIFICATION_DOCUMENT_LABELS;

  ngOnInit(): void {
    void this.load();
  }

  canEdit(): boolean {
    const p = this.profile();
    if (!p) return false;
    // Draft, verified, and rejected can edit; pending review and suspended cannot (matches API).
    return p.status === 0 || p.status === 2 || p.status === 3;
  }

  profileEditLockReason(): string | null {
    const p = this.profile();
    if (!p || this.canEdit()) return null;
    if (p.status === 1) {
      return "Your profile is with admin review — editing is paused until verification finishes.";
    }
    if (p.status === 4) {
      return "This business is suspended. Contact support to update profile details.";
    }
    return "Profile editing is not available for this status.";
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const p = await this.api.withAuthorizedClient((c) => c.getTenantProfile());
        this.profile.set(p);
        this.businessName = p.businessName;
        this.categorySlug = p.categorySlug;
        this.phone = p.phone;
        this.description = p.description ?? "";
        this.additionalCategorySlugs = [...(p.additionalCategorySlugs ?? [])];
        this.coverPreview.set(resolveBusinessCoverImage(p.categorySlug, p.coverImageUrl));
        this.galleryImages.set(p.galleryImages ?? []);
        await Promise.all([this.loadCategories(p), this.loadReviews(p)]);
      }, "Loading profile…", "Fetching business details");
    } catch {
      this.error.set("Could not load profile.");
      this.feedback.error("Could not load profile.");
      this.profile.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  async saveProfile(): Promise<void> {
    if (!this.canEdit()) return;
    if (!this.businessName.trim() || !this.categorySlug.trim() || !PHONE_PATTERN.test(this.phone.trim())) {
      this.error.set("Fix required fields before saving.");
      this.feedback.error("Fix required fields before saving.");
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.updateTenantProfile({
              businessName: this.businessName.trim(),
              categorySlug: this.categorySlug.trim(),
              additionalCategorySlugs: this.additionalCategorySlugs,
              phone: this.phone.trim(),
              description: this.description.trim(),
            }),
          ),
        "Saving profile…",
        "Updating business details",
      );
      this.profile.set(updated);
      this.feedback.success("Business details updated.", "Profile saved");
    } catch {
      this.error.set("Could not save profile.");
      this.feedback.error("Could not save profile.");
    } finally {
      this.saving.set(false);
    }
  }

  categoryLine(): string {
    const p = this.profile();
    if (!p) return "";
    return getCategoryLabel("en", p.categorySlug);
  }

  primaryLocationName(): string | null {
    const p = this.profile();
    if (!p?.locations.length) return null;
    const primary = p.locations.find((l) => l.isPrimary) ?? p.locations[0];
    return primary?.area ?? primary?.name ?? null;
  }

  onPrimaryCategoryChange(): void {
    this.additionalCategorySlugs = this.additionalCategorySlugs.filter((s) => s !== this.categorySlug);
  }

  isAdditionalSelected(slug: string): boolean {
    return this.additionalCategorySlugs.includes(slug);
  }

  toggleAdditional(slug: string): void {
    if (this.isAdditionalSelected(slug)) {
      this.additionalCategorySlugs = this.additionalCategorySlugs.filter((s) => s !== slug);
    } else {
      this.additionalCategorySlugs = [...this.additionalCategorySlugs, slug];
    }
  }

  async loadCategories(profile: BusinessProfile): Promise<void> {
    const marketId =
      profile.locations.find((l) => l.isPrimary)?.marketId ??
      profile.locations[0]?.marketId ??
      "lagos";
    try {
      const categories = await this.api.createPublicClient().getCategories({
        market: marketId,
        wellness: true,
      });
      this.categories.set(categories);
    } catch {
      this.categories.set([]);
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
      this.shareNotice.set("Booking link copied.");
      this.feedback.success("Booking link copied.");
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

  async sendReviewReply(reviewId: string): Promise<void> {
    const reply = this.replyDraft.trim();
    if (!reply) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) =>
        client.replyToReview(reviewId, reply),
      );
      this.reviews.update((items) => items.map((item) => (item.id === updated.id ? updated : item)));
      this.replyingId.set(null);
      this.replyDraft = "";
    } catch {
      this.error.set("Could not post that reply.");
    } finally {
      this.saving.set(false);
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

  async onGallerySelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.profile()) return;

    if (this.galleryImages().length >= MAX_GALLERY_PHOTOS) {
      this.error.set(`You can add up to ${MAX_GALLERY_PHOTOS} gallery photos.`);
      input.value = "";
      return;
    }
    if (!ALLOWED_TYPES.has(file.type)) {
      this.error.set("Use JPEG, PNG, or WebP.");
      return;
    }
    if (file.size > MAX_BYTES) {
      this.error.set("Photo must be 5 MB or smaller.");
      return;
    }

    this.galleryUploading.set(true);
    this.error.set(null);
    try {
      const items = await this.api.withAuthorizedClient(async (c) => {
        const slot = await c.createGalleryUploadUrl(file.type, file.size);
        const upload = await fetch(slot.uploadUrl, {
          method: "PUT",
          headers: { "Content-Type": file.type },
          body: file,
        });
        if (!upload.ok) throw new Error("upload failed");
        return c.addTenantGalleryImage({ galleryImageKey: slot.storageKey });
      });
      this.galleryImages.set(items);
    } catch {
      this.error.set("Gallery upload failed.");
    } finally {
      this.galleryUploading.set(false);
      input.value = "";
    }
  }

  async removeGalleryImage(storageKey: string): Promise<void> {
    this.error.set(null);
    try {
      const items = await this.api.withAuthorizedClient((c) =>
        c.removeTenantGalleryImage({ galleryImageKey: storageKey }),
      );
      this.galleryImages.set(items);
    } catch {
      this.error.set("Could not remove gallery photo.");
    }
  }
}
