import { Component, computed, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { Subscription } from "rxjs";
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
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
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
export class ProfileComponent implements OnInit, OnDestroy {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly route = inject(ActivatedRoute);
  private readonly activeLocation = inject(PortalActiveLocationService);
  private branchSub: Subscription | null = null;
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
  readonly tabs = computed(() => [
    { id: "overview", label: this.tr("portal.customers.overview") },
    { id: "business", label: this.tr("portal.profile.businessDetails") },
    { id: "reviews", label: this.tr("common.reviews") },
    { id: "verification", label: this.tr("portal.profile.verification") },
  ]);

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
    this.branchSub = this.activeLocation.selectionChanged$.subscribe(() => {
      const profile = this.profile();
      if (!profile) {
        return;
      }
      void this.activeLocation.runOnBranchSwitch(() => this.loadReviews(profile));
    });
  }

  ngOnDestroy(): void {
    this.branchSub?.unsubscribe();
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
      return this.tr("portal.profile.yourProfileWithAdminReview");
    }
    if (p.status === 4) {
      return this.tr("portal.profile.thisBusinessSuspendedContactSupport");
    }
    return this.tr("portal.profile.profileEditingNotAvailableStatus");
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
      }, this.tr("portal.profile.loadingProfile"), this.tr("portal.profile.fetchingBusinessDetails"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.profile.couldNotLoadProfile"));
      this.feedback.error(this.apiErr(caught, "portal.profile.couldNotLoadProfile"));
      this.profile.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  async saveProfile(): Promise<void> {
    if (!this.canEdit()) return;
    if (!this.businessName.trim() || !this.categorySlug.trim() || !PHONE_PATTERN.test(this.phone.trim())) {
      this.error.set(this.tr("portal.profile.fixRequiredFieldsBeforeSaving"));
      this.feedback.error(this.tr("portal.profile.fixRequiredFieldsBeforeSaving"));
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
        this.tr("portal.profile.savingProfile"),
        this.tr("portal.profile.updatingBusinessDetails"),
      );
      this.profile.set(updated);
      this.feedback.success(this.tr("portal.profile.businessDetailsUpdated"), this.tr("portal.profile.profileSaved"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.profile.couldNotSaveProfile"));
      this.feedback.error(this.apiErr(caught, "portal.profile.couldNotSaveProfile"));
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
      this.shareNotice.set(this.tr("portal.profile.bookingLinkCopied"));
      this.feedback.success(this.tr("portal.profile.bookingLinkCopied"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "common.couldNotCopyLink"));
    }
  }

  whatsAppShareUrl(): string {
    const p = this.profile();
    const text = encodeURIComponent(
      this.tr("portal.profile.whatsAppShareText", {
        name: p?.businessName ?? this.tr("portal.profile.whatsAppShareFallbackName"),
        url: this.publicBookingUrl(),
      }),
    );
    return `https://wa.me/?text=${text}`;
  }

  canSubmitVerification(): boolean {
    const p = this.profile();
    return p ? p.status === 0 || p.status === 3 : false;
  }

  async loadReviews(profile: BusinessProfile): Promise<void> {
    const branch =
      this.activeLocation.resolveContextLocation(profile)
      ?? profile.locations.find((l) => l.isPrimary)
      ?? profile.locations[0];
    const slug = branch?.slug;
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.profile.couldNotPostReply"));
    } finally {
      this.saving.set(false);
    }
  }

  async submitVerification(): Promise<void> {
    if (!this.canSubmitVerification()) return;
    if (this.verificationReference.trim().length < 4) {
      this.error.set(this.tr("portal.profile.referenceNumberMustBeAt"));
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
      this.verificationMessage.set(this.tr("portal.profile.verificationSubmittedAdminWillReview"));
      this.verificationReference = "";
      await this.load();
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.profile.couldNotSubmitVerification"));
    } finally {
      this.saving.set(false);
    }
  }

  async onCoverSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.profile()) return;

    if (!ALLOWED_TYPES.has(file.type)) {
      this.error.set(this.tr("common.useJpegPngWebp"));
      return;
    }
    if (file.size > MAX_BYTES) {
      this.error.set(this.tr("portal.profile.coverMustBe5Mb"));
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.profile.coverUploadFailed"));
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
      this.error.set(this.tr("portal.profile.youCanAddUpGallery", { MAX_GALLERY_PHOTOS: MAX_GALLERY_PHOTOS }));
      input.value = "";
      return;
    }
    if (!ALLOWED_TYPES.has(file.type)) {
      this.error.set(this.tr("common.useJpegPngWebp"));
      return;
    }
    if (file.size > MAX_BYTES) {
      this.error.set(this.tr("common.photoMustBe5Mb"));
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.profile.galleryUploadFailed"));
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.profile.couldNotRemoveGalleryPhoto"));
    }
  }
}
