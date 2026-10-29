import { Component, computed, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { merge, Subscription } from "rxjs";
import { toSignal } from "@angular/core/rxjs-interop";
import { DatePipe, KeyValuePipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { map } from "rxjs";
import type {
  BusinessProfile,
  Category,
  GalleryImage,
  PublicReviewItem,
  VerificationBadge,
  VerificationBadgeType,
} from "@adeni/shared";
import {
  formatTenantStatus,
  getCategoryLabel,
  getReviewCountLabel,
  MAX_GALLERY_PHOTOS,
  MAX_UPLOAD_BYTES,
  PROFILE_REVIEWS_PAGE_SIZE,
  resolveBusinessCoverImage,
  VERIFICATION_BADGE_LABELS,
  VERIFICATION_BADGE_TYPES,
  VERIFICATION_DOCUMENT_LABELS,
} from "@adeni/shared";
import {
  type AdeniCarbonIconName,
  type AdeniPillTone,
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniFeedbackService,
  AdeniLocaleService,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
  AdeniTabsComponent,
  TenantOperationsRealtimeService,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { BusinessActiveLocationService } from "../../core/services/business-active-location.service";
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
    DatePipe,
    AdeniTabsComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListPaginationComponent,
    AdeniListToolbarComponent,
    AdeniPillComponent,
    AdeniStaffButtonComponent,
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
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly route = inject(ActivatedRoute);
  protected readonly activeLocation = inject(BusinessActiveLocationService);
  private readonly uiLocale = inject(AdeniLocaleService).locale;
  private readonly operationsRealtime = inject(TenantOperationsRealtimeService);
  private branchSub: Subscription | null = null;
  private verificationSub: Subscription | null = null;
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
    { id: "overview", label: this.tr("businessApp.customers.overview") },
    { id: "business", label: this.tr("businessApp.profile.businessDetails") },
    { id: "reviews", label: this.tr("common.reviews") },
    { id: "verification", label: this.tr("businessApp.profile.verification") },
  ]);

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
  readonly reviewsLoading = signal(false);
  readonly reviewsPage = signal(1);
  readonly reviewsPageSize = signal(PROFILE_REVIEWS_PAGE_SIZE);
  readonly reviewsPageSizeOptions = [PROFILE_REVIEWS_PAGE_SIZE] as const;
  readonly reviewsTotalCount = signal(0);
  readonly reviewsRepliedCount = signal(0);
  readonly reviewSummary = signal<{ avg: number | null; count: number }>({ avg: null, count: 0 });
  readonly reviewStarSlots = [1, 2, 3, 4, 5] as const;
  readonly replyingId = signal<string | null>(null);
  replyDraft = "";
  readonly verificationMessage = signal<string | null>(null);
  readonly shareNotice = signal<string | null>(null);
  verificationDocType = 0;
  verificationReference = "";
  readonly verificationFileKey = signal<string | null>(null);
  readonly verificationFileUploading = signal(false);
  readonly trustBadges = signal<VerificationBadge[]>([]);
  readonly trustBadgeBusy = signal<VerificationBadgeType | null>(null);

  readonly formatTenantStatus = formatTenantStatus;
  readonly verificationLabels = VERIFICATION_DOCUMENT_LABELS;
  readonly verificationBadgeLabels = VERIFICATION_BADGE_LABELS;
  readonly verificationBadgeTypes = VERIFICATION_BADGE_TYPES;

  /** Optional registry/license number sent with CAC and license badge requests (admin sees it in the queue). */
  trustBadgeReferences: Partial<Record<VerificationBadgeType, string>> = {};

  readonly verificationBadgeMeta: Record<
    VerificationBadgeType,
    { icon: AdeniCarbonIconName; descKey: string; referenceKey?: string }
  > = {
    phone: { icon: "phone", descKey: "businessApp.profile.verificationBadgeDescPhone" },
    cac: {
      icon: "document",
      descKey: "businessApp.profile.verificationBadgeDescCac",
      referenceKey: "businessApp.profile.verificationBadgeReferenceCac",
    },
    address: { icon: "location", descKey: "businessApp.profile.verificationBadgeDescAddress" },
    license: {
      icon: "certificate",
      descKey: "businessApp.profile.verificationBadgeDescLicense",
      referenceKey: "businessApp.profile.verificationBadgeReferenceLicense",
    },
  };

  /** Status hero copy + visual tone (tenant status: 0 draft, 1 pending, 2 verified, 3 rejected, 4 suspended). */
  readonly verificationHero = computed(() => {
    const status = this.profile()?.status ?? 0;
    const map: Record<number, { key: string; icon: AdeniCarbonIconName; tone: AdeniPillTone }> = {
      0: { key: "Draft", icon: "document", tone: "brand" },
      1: { key: "Pending", icon: "time", tone: "warning" },
      2: { key: "Verified", icon: "security", tone: "success" },
      3: { key: "Rejected", icon: "warning--filled", tone: "danger" },
      4: { key: "Suspended", icon: "warning--filled", tone: "danger" },
    };
    const entry = map[status] ?? map[0];
    return {
      status,
      icon: entry.icon,
      tone: entry.tone,
      title: this.tr(`portal.profile.verificationHero${entry.key}Title`),
      body: this.tr(`portal.profile.verificationHero${entry.key}Body`),
    };
  });

  readonly trustBadgeProgress = computed(() => {
    const granted = VERIFICATION_BADGE_TYPES.filter((t) => this.trustBadgeGranted(t)).length;
    return this.tr("businessApp.profile.verificationTrustBadgesProgress", {
      granted,
      total: VERIFICATION_BADGE_TYPES.length,
    });
  });

  readonly reviewsReplyStats = computed(() => {
    const replied = this.reviewsRepliedCount();
    const total = this.reviewsTotalCount();
    const rate = total > 0 ? Math.round((replied / total) * 100) : 0;
    return { replied, total, rate };
  });

  readonly reviewsScopeHint = computed(() => {
    const display = this.activeLocation.switchDisplay();
    if (display.allLocationsScope) {
      return this.tr("businessApp.profile.reviewsAllLocations");
    }
    return this.tr("businessApp.profile.reviewsBranchScope", { branch: display.title });
  });

  readonly reviewsHero = computed(() => {
    const count = this.reviewSummary().count;
    if (count <= 0) {
      return {
        status: "empty" as const,
        icon: "user--follow" as AdeniCarbonIconName,
        tone: "brand" as AdeniPillTone,
        title: this.tr("businessApp.profile.reviewsHeroEmptyTitle"),
        body: this.tr("businessApp.profile.reviewsHeroEmptyBody"),
        badge: this.tr("businessApp.profile.reviewsHeroEmptyBadge"),
      };
    }
    return {
      status: "active" as const,
      icon: "checkmark--filled" as AdeniCarbonIconName,
      tone: "success" as AdeniPillTone,
      title: this.tr("businessApp.profile.reviewsHeroActiveTitle"),
      body: this.tr("businessApp.profile.reviewsHeroActiveBody"),
      badge: this.tr("businessApp.profile.reviewsHeroActiveBadge"),
    };
  });

  readonly reviewsCountLabel = computed(() =>
    getReviewCountLabel(this.uiLocale(), this.reviewSummary().count),
  );

  readonly reviewsRatingDistribution = computed(() => {
    const items = this.reviews();
    if (items.length === 0) {
      return null;
    }
    const counts = [0, 0, 0, 0, 0];
    for (const row of items) {
      const bucket = Math.min(5, Math.max(1, Math.round(row.rating))) - 1;
      counts[bucket]++;
    }
    const peak = Math.max(...counts, 1);
    return ([5, 4, 3, 2, 1] as const).map((stars) => ({
      stars,
      count: counts[stars - 1],
      percent: (counts[stars - 1] / peak) * 100,
    }));
  });

  readonly verificationSteps = computed(() => {
    const status = this.profile()?.status ?? 0;
    const labels = [
      this.tr("businessApp.profile.verificationStepDraft"),
      this.tr("businessApp.profile.verificationStepInReview"),
      this.tr("businessApp.profile.verificationStepVerified"),
    ];
    let activeIndex = 0;
    if (status === 1) {
      activeIndex = 1;
    } else if (status === 2) {
      activeIndex = 2;
    } else if (status === 3) {
      return {
        labels: [
          labels[0],
          this.tr("businessApp.profile.verificationStepRejected"),
          labels[2],
        ],
        activeIndex: 1,
      };
    } else if (status === 4) {
      return {
        labels: [
          labels[0],
          this.tr("businessApp.profile.verificationStepSuspended"),
          labels[2],
        ],
        activeIndex: 1,
      };
    }
    return { labels, activeIndex };
  });

  readonly prerequisitesMet = computed(() => {
    const p = this.profile();
    if (!p) return false;
    const hasLocation = p.locations.some((l) => l.isActive && l.slug.trim().length > 0);
    const profileReady =
      p.businessName.trim().length >= 2 && p.categorySlug.trim().length > 0 && p.phone.trim().length >= 10;
    return hasLocation && profileReady;
  });

  ngOnInit(): void {
    void this.load();
    this.branchSub = this.activeLocation.selectionChanged$.subscribe(() => {
      const profile = this.profile();
      if (!profile) {
        return;
      }
      this.reviewsPage.set(1);
      void this.activeLocation.runOnBranchSwitch(() => this.loadReviews(profile));
    });
    this.verificationSub = merge(
      this.operationsRealtime.tenantVerificationChanged$,
      this.operationsRealtime.tenantBadgesChanged$,
    ).subscribe((event) => {
      if (event.tenantId === this.profile()?.tenantId) {
        void this.refreshVerificationState();
      }
    });
  }

  ngOnDestroy(): void {
    this.branchSub?.unsubscribe();
    this.verificationSub?.unsubscribe();
  }

  /** Realtime: re-fetch profile status + badge rows without the loading overlay or touching form drafts. */
  private async refreshVerificationState(): Promise<void> {
    try {
      const p = await this.api.withAuthorizedClient((c) => c.getTenantProfile());
      this.profile.set(p);
      await this.loadTrustBadges(p);
    } catch {
      // Keep current UI; user can reload.
    }
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
      return this.tr("businessApp.profile.yourProfileWithAdminReview");
    }
    if (p.status === 4) {
      return this.tr("businessApp.profile.thisBusinessSuspendedContactSupport");
    }
    return this.tr("businessApp.profile.profileEditingNotAvailableStatus");
  }

  async load(): Promise<void> {
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
        await Promise.all([this.loadCategories(p), this.loadReviews(p), this.loadTrustBadges(p)]);
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.profile.couldNotLoadProfile"));
      this.feedback.error(this.apiErr(caught, "businessApp.profile.couldNotLoadProfile"));
      this.profile.set(null);
    }
  }

  async saveProfile(): Promise<void> {
    if (!this.canEdit()) return;
    if (!this.businessName.trim() || !this.categorySlug.trim() || !PHONE_PATTERN.test(this.phone.trim())) {
      this.error.set(this.tr("businessApp.profile.fixRequiredFieldsBeforeSaving"));
      this.feedback.error(this.tr("businessApp.profile.fixRequiredFieldsBeforeSaving"));
      return;
    }
    if (
      !(await this.confirmDialog.confirmKind("save", {
        subject: this.tr("common.confirm.subjects.profile"),
      }))
    ) {
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
        this.tr("businessApp.profile.savingProfile"),
        this.tr("businessApp.profile.updatingBusinessDetails"),
      );
      this.profile.set(updated);
      this.feedback.success(this.tr("businessApp.profile.businessDetailsUpdated"), this.tr("businessApp.profile.profileSaved"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.profile.couldNotSaveProfile"));
      this.feedback.error(this.apiErr(caught, "businessApp.profile.couldNotSaveProfile"));
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
      this.shareNotice.set(this.tr("businessApp.profile.bookingLinkCopied"));
      this.feedback.success(this.tr("businessApp.profile.bookingLinkCopied"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "common.couldNotCopyLink"));
    }
  }

  whatsAppShareUrl(): string {
    const p = this.profile();
    const text = encodeURIComponent(
      this.tr("businessApp.profile.whatsAppShareText", {
        name: p?.businessName ?? this.tr("businessApp.profile.whatsAppShareFallbackName"),
        url: this.publicBookingUrl(),
      }),
    );
    return `https://wa.me/?text=${text}`;
  }

  canSubmitVerification(): boolean {
    const p = this.profile();
    return p ? p.status === 0 || p.status === 3 : false;
  }

  isSuspended(): boolean {
    return this.profile()?.status === 4;
  }

  async loadTrustBadges(profile: BusinessProfile): Promise<void> {
    if (profile.status !== 2) {
      this.trustBadges.set([]);
      return;
    }
    try {
      const items = await this.api.withAuthorizedClient((c) => c.listTenantVerificationBadges());
      this.trustBadges.set(items);
    } catch {
      this.trustBadges.set([]);
    }
  }

  async requestTrustBadge(badgeType: VerificationBadgeType): Promise<void> {
    const reference = this.verificationBadgeMeta[badgeType].referenceKey
      ? this.trustBadgeReferences[badgeType]?.trim() || undefined
      : undefined;
    if (reference && reference.length < 5) {
      this.feedback.error(this.tr("businessApp.profile.referenceNumberMustBeAt"));
      return;
    }

    if (
      !(await this.confirmDialog.confirmKind("submit", {
        subject: this.tr("common.confirm.subjects.badgeRequest", {
          badge: VERIFICATION_BADGE_LABELS[badgeType],
        }),
      }))
    ) {
      return;
    }

    this.trustBadgeBusy.set(badgeType);
    try {
      const requested = await this.api.withAuthorizedClient((c) =>
        c.requestVerificationBadge({ badgeType, referenceNumber: reference }),
      );
      // Show Pending immediately; the list reload below keeps it after refresh.
      this.trustBadges.update((items) => [
        ...items.filter((b) => b.badgeType !== badgeType),
        requested,
      ]);
      this.trustBadgeReferences[badgeType] = "";
      this.feedback.success(this.tr("businessApp.profile.verificationRequestBadge"));
      const p = this.profile();
      if (p) {
        await this.loadTrustBadges(p);
      }
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.profile.couldNotSubmitVerification"));
    } finally {
      this.trustBadgeBusy.set(null);
    }
  }

  docTypeLabel(documentType: number): string {
    return this.verificationLabels[documentType] ?? String(documentType);
  }

  trustBadgeGranted(badgeType: string): boolean {
    return this.trustBadges().some((b) => b.badgeType === badgeType && b.status === "granted");
  }

  trustBadgePending(badgeType: string): boolean {
    return this.trustBadges().some((b) => b.badgeType === badgeType && b.status === "pending");
  }

  async loadReviews(profile: BusinessProfile): Promise<void> {
    this.reviewsLoading.set(true);
    const branch =
      this.activeLocation.resolveContextLocation(profile)
      ?? profile.locations.find((l) => l.isPrimary)
      ?? profile.locations[0];
    const slug = branch?.slug?.trim();
    const locationId = this.activeLocation.tenantApiLocationId(profile.entitlements);
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const pageSize = this.reviewsPageSize();
        const payload = await c.listTenantReviews({
          page: this.reviewsPage(),
          pageSize,
          locationId,
        });
        this.reviews.set(payload.items.slice(0, pageSize));
        this.reviewsTotalCount.set(payload.totalCount);
        this.reviewsRepliedCount.set(payload.repliedCount ?? 0);

        let avg: number | null = null;
        let count = payload.totalCount;
        if (slug) {
          const publicProfile = await c.getBusinessProfile(slug);
          avg = publicProfile.ratingAvg ?? null;
          count = publicProfile.reviewCount ?? payload.totalCount;
        }
        this.reviewSummary.set({ avg, count });
      });
    } catch {
      this.reviews.set([]);
      this.reviewsTotalCount.set(0);
      this.reviewsRepliedCount.set(0);
      this.reviewSummary.set({ avg: null, count: 0 });
    } finally {
      this.reviewsLoading.set(false);
    }
  }

  reviewRatingTone(rating: number): AdeniPillTone {
    if (rating >= 5) {
      return "success";
    }
    if (rating >= 4) {
      return "brand";
    }
    if (rating >= 3) {
      return "warning";
    }
    return "danger";
  }

  reviewAverageDisplay(): string {
    const avg = this.reviewSummary().avg;
    if (avg == null) {
      return "—";
    }
    return avg.toFixed(1);
  }

  reviewAverageStars(): number {
    const avg = this.reviewSummary().avg;
    if (avg == null) {
      return 0;
    }
    return Math.min(5, Math.max(0, Math.round(avg)));
  }

  isReviewStarFilled(starIndex: number, rating: number): boolean {
    return starIndex <= rating;
  }

  goToReviewsPage(next: number): void {
    if (next === this.reviewsPage()) {
      return;
    }
    this.reviewsPage.set(next);
    const profile = this.profile();
    if (profile) {
      void this.loadReviews(profile);
    }
  }

  async sendReviewReply(reviewId: string): Promise<void> {
    const reply = this.replyDraft.trim();
    if (!reply) return;
    if (
      !(await this.confirmDialog.confirmKind("send", {
        subject: this.tr("common.confirm.subjects.reviewReply"),
      }))
    ) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const hadReply = (this.reviews().find((item) => item.id === reviewId)?.ownerReply?.trim()?.length ?? 0) > 0;
    try {
      const updated = await this.api.withAuthorizedClient((client) =>
        client.replyToReview(reviewId, reply),
      );
      this.reviews.update((items) => items.map((item) => (item.id === updated.id ? updated : item)));
      if (!hadReply) {
        this.reviewsRepliedCount.update((count) => count + 1);
      }
      this.replyingId.set(null);
      this.replyDraft = "";
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.profile.couldNotPostReply"));
    } finally {
      this.saving.set(false);
    }
  }

  async submitVerification(): Promise<void> {
    if (!this.canSubmitVerification()) return;
    if (this.verificationReference.trim().length < 5) {
      this.error.set(this.tr("businessApp.profile.referenceNumberMustBeAt"));
      return;
    }
    if (!this.prerequisitesMet()) {
      this.error.set(this.tr("businessApp.profile.fixRequiredFieldsBeforeSaving"));
      return;
    }
    if (
      !(await this.confirmDialog.confirmKind("submit", {
        subject: this.tr("common.confirm.subjects.verification"),
      }))
    ) {
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
              storageFileKey: this.verificationFileKey() ?? undefined,
            },
          ],
        }),
      );
      this.verificationMessage.set(this.tr("businessApp.profile.verificationSubmittedAdminWillReview"));
      this.verificationReference = "";
      this.verificationFileKey.set(null);
      await this.load();
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.profile.couldNotSubmitVerification"));
    } finally {
      this.saving.set(false);
    }
  }

  async onVerificationFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const allowed = new Set(["image/jpeg", "image/png", "application/pdf"]);
    if (!allowed.has(file.type)) {
      this.error.set(this.tr("common.useJpegPngWebp"));
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.error.set(this.tr("common.photoMustBe5Mb"));
      return;
    }

    this.verificationFileUploading.set(true);
    this.error.set(null);
    try {
      const key = await this.api.withAuthorizedClient(async (c) => {
        const slot = await c.createVerificationDocumentUploadUrl(file.type, file.size);
        const upload = await fetch(slot.uploadUrl, {
          method: "PUT",
          headers: { "Content-Type": file.type },
          body: file,
        });
        if (!upload.ok) throw new Error("upload failed");
        return slot.storageKey;
      });
      this.verificationFileKey.set(key);
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.profile.couldNotSubmitVerification"));
    } finally {
      this.verificationFileUploading.set(false);
      input.value = "";
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
      this.error.set(this.tr("businessApp.profile.coverMustBe5Mb"));
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
      this.error.set(this.apiErr(caught, "businessApp.profile.coverUploadFailed"));
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
      this.error.set(this.tr("businessApp.profile.youCanAddUpGallery", { MAX_GALLERY_PHOTOS: MAX_GALLERY_PHOTOS }));
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
      this.error.set(this.apiErr(caught, "businessApp.profile.galleryUploadFailed"));
    } finally {
      this.galleryUploading.set(false);
      input.value = "";
    }
  }

  async removeGalleryImage(storageKey: string): Promise<void> {
    if (
      !(await this.confirmDialog.confirmKind("remove", {
        subject: this.tr("common.confirm.subjects.galleryPhoto"),
      }))
    ) {
      return;
    }
    this.error.set(null);
    try {
      const items = await this.api.withAuthorizedClient((c) =>
        c.removeTenantGalleryImage({ galleryImageKey: storageKey }),
      );
      this.galleryImages.set(items);
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.profile.couldNotRemoveGalleryPhoto"));
    }
  }
}
