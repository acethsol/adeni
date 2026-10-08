import { DecimalPipe } from "@angular/common";
import {
  afterNextRender,
  Component,
  computed,
  inject,
  Injector,
  makeStateKey,
  OnInit,
  PendingTasks,
  signal,
  TransferState,
  viewChild,
} from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import type {
  PublicBusinessProfile,
  PublicReviewItem,
  ServiceMenuGroup,
  ServiceOffering,
} from "@adeni/shared";
import {
  DEFAULT_PUBLIC_PAGE_CONFIG,
  discoveryCtaLabel,
  formatPrice,
  getCategoryLabel,
  getCategoryVisual,
  resolveBusinessCoverImage,
  resolveBusinessImageUrls,
  resolvePublicPageTemplateId,
  shouldShowQuoteFlow,
  t,
} from "@adeni/shared";
import { AdeniCarbonIconComponent, AdeniLocaleService } from "@adeni/ui";
import {
  ADENI_DISCOVER_CONFIG,
  isDiscoverCustomerDevMode,
} from "../../core/adeni-config";
import { buildLocalBusinessJsonLd } from "../../core/seo-jsonld";
import { CustomerApiService } from "../../core/services/customer-api.service";
import { DiscoverLoadingService } from "../../core/services/discover-loading.service";
import { SeoService } from "../../core/services/seo.service";
import { MarketContextService } from "../../core/services/market-context.service";
import { BookingPanelComponent } from "../../shared/booking-panel.component";
import { QuoteRequestPanelComponent } from "../../shared/quote-request-panel.component";

const BUSINESS_STATE_KEY = makeStateKey<{
  slug: string;
  profile: PublicBusinessProfile;
  services: ServiceOffering[];
  menuGroups: ServiceMenuGroup[];
  reviews: PublicReviewItem[];
}>("discover-business-profile");

type ServiceCollection = {
  id: string;
  name: string;
  services: ServiceOffering[];
};

@Component({
  selector: "app-business-profile",
  standalone: true,
  imports: [
    RouterLink,
    DecimalPipe,
    BookingPanelComponent,
    QuoteRequestPanelComponent,
    AdeniCarbonIconComponent,
  ],
  templateUrl: "./business.component.html",
  styleUrl: "./business.component.scss",
})
export class BusinessProfileComponent implements OnInit {
  private readonly api = inject(CustomerApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly market = inject(MarketContextService);
  private readonly localeService = inject(AdeniLocaleService);
  private readonly pendingTasks = inject(PendingTasks);
  private readonly transferState = inject(TransferState);
  private readonly pageLoading = inject(DiscoverLoadingService);
  private readonly injector = inject(Injector);
  private readonly bookingPanel = viewChild(BookingPanelComponent);
  private bookRailPulseTimer: ReturnType<typeof setTimeout> | null = null;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<PublicBusinessProfile | null>(null);
  readonly services = signal<ServiceOffering[]>([]);
  readonly menuGroups = signal<ServiceMenuGroup[]>([]);
  readonly reviews = signal<PublicReviewItem[]>([]);
  readonly bookingEnabled = signal(false);
  readonly linkCopied = signal(false);
  readonly whatsappUrl = signal<string | null>(null);
  readonly galleryIndex = signal(0);
  readonly servicesExpanded = signal(false);
  readonly serviceSearch = signal("");
  readonly bookRailPulse = signal(false);

  private static readonly SERVICES_PREVIEW = 6;

  readonly locale = this.localeService.locale;
  readonly coverFor = resolveBusinessCoverImage;
  readonly showQuoteFlow = shouldShowQuoteFlow;
  readonly priceFor = formatPrice;

  readonly visual = computed(() => {
    const p = this.profile();
    return p ? getCategoryVisual(p.categorySlug) : null;
  });

  readonly gallery = computed(() => {
    const p = this.profile();
    if (!p) {
      return [] as string[];
    }
    return resolveBusinessImageUrls(p.categorySlug, p.coverImageUrl, p.imageUrls);
  });

  readonly coverUrl = computed(() => {
    const images = this.gallery();
    if (images.length > 0) {
      const idx = Math.min(this.galleryIndex(), images.length - 1);
      return images[idx] ?? images[0] ?? "";
    }
    const p = this.profile();
    return p ? this.coverFor(p.categorySlug, p.coverImageUrl) : "";
  });

  readonly categoryName = computed(() => {
    const p = this.profile();
    return p ? getCategoryLabel(this.locale(), p.categorySlug) : "";
  });

  readonly ctaLabel = computed(() =>
    discoveryCtaLabel(this.profile()?.discoveryCta, this.locale()),
  );

  label(key: string, vars?: Record<string, string | number>): string {
    return t(this.locale(), key, vars);
  }

  readonly isNew = computed(() => {
    const p = this.profile();
    return Boolean(p && (p.reviewCount ?? 0) === 0);
  });

  readonly directionsUrl = computed(() => {
    const p = this.profile();
    if (!p || p.latitude == null || p.longitude == null) {
      return null;
    }
    const q = encodeURIComponent(`${p.latitude},${p.longitude}`);
    return `https://www.google.com/maps/dir/?api=1&destination=${q}`;
  });

  readonly mapsUrl = computed(() => {
    const p = this.profile();
    if (!p) {
      return null;
    }
    if (p.latitude != null && p.longitude != null) {
      return `https://www.google.com/maps/search/?api=1&query=${p.latitude},${p.longitude}`;
    }
    const q = encodeURIComponent(`${p.addressLine}, ${p.area}`);
    return `https://www.google.com/maps/search/?api=1&query=${q}`;
  });

  readonly menuServices = computed(() => {
    const items = this.services();
    const active = items.filter((s) => s.isActive !== false);
    const base = active.length > 0 ? active : items;
    const q = this.serviceSearch().trim().toLowerCase();
    if (!q) {
      return base;
    }
    return base.filter(
      (s) =>
        s.name.toLowerCase().includes(q) ||
        (s.description?.toLowerCase().includes(q) ?? false),
    );
  });

  readonly serviceCollections = computed((): ServiceCollection[] => {
    const items = this.menuServices();
    const groups = [...this.menuGroups()].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
    const collections: ServiceCollection[] = [];
    const assigned = new Set<string>();

    for (const group of groups) {
      const services = items.filter((s) => s.menuGroupId === group.id);
      if (services.length === 0) {
        continue;
      }
      for (const s of services) {
        assigned.add(s.id);
      }
      collections.push({ id: group.id, name: group.name, services });
    }

    const ungrouped = items.filter((s) => !assigned.has(s.id));
    if (ungrouped.length > 0) {
      collections.push({
        id: "__other",
        name: groups.length > 0 ? "Other services" : "Services",
        services: ungrouped,
      });
    }

    return collections;
  });

  readonly visibleCollections = computed((): ServiceCollection[] => {
    const collections = this.serviceCollections();
    if (this.servicesExpanded()) {
      return collections;
    }

    let remaining = BusinessProfileComponent.SERVICES_PREVIEW;
    const visible: ServiceCollection[] = [];
    for (const collection of collections) {
      if (remaining <= 0) {
        break;
      }
      const slice = collection.services.slice(0, remaining);
      remaining -= slice.length;
      visible.push({ ...collection, services: slice });
    }
    return visible;
  });

  readonly hiddenServiceCount = computed(() =>
    Math.max(
      0,
      this.menuServices().length -
        this.visibleCollections().reduce((n, c) => n + c.services.length, 0),
    ),
  );

  readonly tagline = computed(() => {
    const p = this.profile();
    if (!p) {
      return "";
    }
    const desc = p.description?.trim();
    if (desc && desc.length <= 140) {
      return desc;
    }
    if (desc) {
      return `${desc.slice(0, 130).trimEnd()}…`;
    }
    return `Book ${this.categoryName().toLowerCase()} in ${p.area}.`;
  });

  readonly publicPage = computed(() => {
    const page = this.profile()?.publicPage;
    if (!page) {
      return DEFAULT_PUBLIC_PAGE_CONFIG;
    }
    return {
      ...DEFAULT_PUBLIC_PAGE_CONFIG,
      ...page,
      templateId: resolvePublicPageTemplateId(page.templateId),
      sections: {
        ...DEFAULT_PUBLIC_PAGE_CONFIG.sections,
        ...page.sections,
      },
    };
  });

  readonly bookingOpen = computed(() => this.sections().book !== false);

  readonly templateId = computed(() => this.publicPage().templateId);

  readonly accentColor = computed(
    () => this.publicPage().accentColor ?? this.visual()?.gradient[1] ?? "#13c2a3",
  );

  readonly logoUrl = computed(() => this.publicPage().logoImageUrl ?? null);

  readonly sections = computed(() => this.publicPage().sections);

  readonly showPolicies = computed(() => {
    if (!this.sections().policies) {
      return false;
    }
    const policies = this.profile()?.policies;
    if (!policies) {
      return false;
    }
    return Boolean(
      policies.booking?.trim() ||
        policies.payment?.trim() ||
        policies.cancellation?.trim() ||
        policies.terms?.trim(),
    );
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get("slug");
      if (slug) {
        void this.load(slug);
      }
    });
  }

  supportsDeposits(profile: PublicBusinessProfile): boolean {
    return profile.capabilities?.includes("deposits") ?? false;
  }

  scrollToSection(sectionId: string, event?: Event): void {
    event?.preventDefault();
    const el = document.getElementById(sectionId);
    if (!el) {
      return;
    }
    el.scrollIntoView({ behavior: "smooth", block: "start" });
    history.replaceState(null, "", `#${sectionId}`);
    if (sectionId === "book") {
      this.pulseBookRail();
    }
  }

  bookService(service: ServiceOffering, event?: Event): void {
    event?.preventDefault();
    if (!this.bookingOpen()) {
      this.scrollToSection("visit", event);
      return;
    }
    this.scrollToSection("book", event);
    const panel = this.bookingPanel();
    if (panel) {
      void panel.selectService(service);
      return;
    }
    // Panel may not be ready yet (quote flow / first paint) — retry once.
    afterNextRender(
      () => {
        void this.bookingPanel()?.selectService(service);
      },
      { injector: this.injector },
    );
  }

  private pulseBookRail(): void {
    if (this.bookRailPulseTimer) {
      clearTimeout(this.bookRailPulseTimer);
    }
    this.bookRailPulse.set(false);
    this.bookRailPulseTimer = setTimeout(() => {
      this.bookRailPulse.set(true);
      this.bookRailPulseTimer = setTimeout(() => {
        this.bookRailPulse.set(false);
        this.bookRailPulseTimer = null;
      }, 1100);
    }, 16);
  }

  prevGalleryImage(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const count = this.gallery().length;
    if (count < 2) {
      return;
    }
    this.galleryIndex.update((i) => (i - 1 + count) % count);
  }

  nextGalleryImage(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const count = this.gallery().length;
    if (count < 2) {
      return;
    }
    this.galleryIndex.update((i) => (i + 1) % count);
  }

  async copyBookingLink(): Promise<void> {
    const p = this.profile();
    if (!p || typeof navigator === "undefined" || !navigator.clipboard) {
      return;
    }
    const url = `${this.config.publicAppUrl}/businesses/${p.slug}`;
    try {
      await navigator.clipboard.writeText(url);
      this.linkCopied.set(true);
      window.setTimeout(() => this.linkCopied.set(false), 2000);
    } catch {
      /* ignore */
    }
  }

  private async load(slug: string): Promise<void> {
    this.galleryIndex.set(0);
    const cached = this.transferState.get(BUSINESS_STATE_KEY, null);
    if (cached?.slug === slug) {
      this.profile.set(cached.profile);
      this.services.set(cached.services);
      this.menuGroups.set(cached.menuGroups ?? []);
      this.reviews.set(cached.reviews);
      this.loading.set(false);
      this.transferState.remove(BUSINESS_STATE_KEY);
      this.applySeo(cached.profile, slug);
      void this.hydrateExtras(slug);
      this.scrollToRouteFragment();
      return;
    }

    await this.pendingTasks.run(() => this.fetch(slug));
  }

  private async fetch(slug: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.linkCopied.set(false);
    this.whatsappUrl.set(null);
    this.pageLoading.show("Loading business…", "Fetching profile, services, and reviews");

    const client = this.api.createPublicClient();

    try {
      const [profile, catalog, reviews] = await Promise.all([
        client.getBusinessProfile(slug),
        client.getBusinessServiceCatalog(slug).catch(() => ({ items: [] as ServiceOffering[], groups: [] as ServiceMenuGroup[] })),
        client
          .getBusinessReviews(slug, 1, 6)
          .then((r) => r.items)
          .catch(() => [] as PublicReviewItem[]),
      ]);
      this.profile.set(profile);
      this.services.set(catalog.items);
      this.menuGroups.set(catalog.groups);
      this.reviews.set(reviews);
      this.transferState.set(BUSINESS_STATE_KEY, {
        slug,
        profile,
        services: catalog.items,
        menuGroups: catalog.groups,
        reviews,
      });
      this.applySeo(profile, slug);
      void this.hydrateExtras(slug);
      this.scrollToRouteFragment();
    } catch {
      this.error.set("Business not found or API unavailable.");
      this.profile.set(null);
      this.services.set([]);
      this.menuGroups.set([]);
      this.reviews.set([]);
    } finally {
      this.loading.set(false);
      this.pageLoading.hide();
    }
  }

  private scrollToRouteFragment(): void {
    const fragment = this.route.snapshot.fragment;
    if (!fragment || typeof document === "undefined") {
      return;
    }
    afterNextRender(
      () => {
        window.setTimeout(() => this.scrollToSection(fragment), 50);
      },
      { injector: this.injector },
    );
  }

  private async hydrateExtras(slug: string): Promise<void> {
    const devEnabled = isDiscoverCustomerDevMode(this.config);
    const loggedIn = devEnabled || (await this.api.isLoggedIn());
    this.bookingEnabled.set(loggedIn);

    const client = this.api.createPublicClient();
    void client
      .getBusinessWhatsAppLink(slug)
      .then((link) => this.whatsappUrl.set(link.url))
      .catch(() => this.whatsappUrl.set(null));
  }

  private applySeo(profile: PublicBusinessProfile, slug: string): void {
    const marketName = this.market.market()?.name ?? "Adeni";
    const path = `/businesses/${slug}`;
    const cover = this.coverFor(profile.categorySlug, profile.coverImageUrl);
    const description =
      profile.description ||
      `Verified ${getCategoryLabel(this.locale(), profile.categorySlug)} in ${profile.area}.`;

    this.seo.update(
      {
        title: `${profile.name} — Adeni ${marketName}`,
        description,
        canonicalPath: path,
        imageUrl: cover,
        jsonLd: buildLocalBusinessJsonLd(
          profile,
          `${this.config.publicAppUrl}${path}`,
          cover,
        ),
      },
      this.config.publicAppUrl,
    );
  }
}
