import {
  Component,
  computed,
  inject,
  makeStateKey,
  OnInit,
  PendingTasks,
  signal,
  TransferState,
} from "@angular/core";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import type { Category, DiscoveryBusinessItem, SeoNeighborhood } from "@adeni/shared";
import {
  FEATURED_PAGE_SIZE,
  getCategoryLabel,
  getMarketById,
  getMarketFeaturedCategorySlugs,
  getSeoNeighborhood,
  isSeoCategorySlug,
  listSeoNeighborhoods,
  seoCategoryPath,
  seoMarketPath,
  seoNeighborhoodCategoryPath,
  seoNeighborhoodPath,
  t,
} from "@adeni/shared";
import { AdeniLocaleService, injectAdeniTranslate } from "@adeni/ui";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";
import { CustomerApiService } from "../../core/services/customer-api.service";
import { MarketContextService } from "../../core/services/market-context.service";
import { SeoService } from "../../core/services/seo.service";
import { DiscoveryBusinessCardComponent } from "../../shared/discovery-business-card.component";

const SEO_MARKET_STATE_KEY = makeStateKey<{
  marketId: string;
  categorySlug: string | null;
  areaSlug: string | null;
  categories: Category[];
  items: DiscoveryBusinessItem[];
}>("seo-market-landing");

@Component({
  selector: "app-seo-market",
  standalone: true,
  imports: [RouterLink, DiscoveryBusinessCardComponent],
  templateUrl: "./seo-market.component.html",
  styleUrl: "./seo-market.component.scss",
})
export class SeoMarketPageComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(CustomerApiService);
  private readonly market = inject(MarketContextService);
  private readonly seo = inject(SeoService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly localeService = inject(AdeniLocaleService);
  private readonly pendingTasks = inject(PendingTasks);
  private readonly transferState = inject(TransferState);

  readonly locale = this.localeService.locale;
  readonly loading = signal(true);
  readonly marketId = signal("lagos");
  readonly categorySlug = signal<string | null>(null);
  readonly areaSlug = signal<string | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly items = signal<DiscoveryBusinessItem[]>([]);

  readonly marketConfig = computed(() => getMarketById(this.marketId()));
  readonly marketName = computed(() => this.marketConfig()?.name ?? "Adeni");
  readonly neighborhood = computed((): SeoNeighborhood | null =>
    getSeoNeighborhood(this.marketId(), this.areaSlug()),
  );
  readonly areaLabel = computed(() => this.neighborhood()?.label ?? null);
  readonly categoryLabel = computed(() => {
    const slug = this.categorySlug();
    if (!slug) {
      return null;
    }
    return getCategoryLabel(this.locale(), slug);
  });
  readonly heading = computed(() => {
    const category = this.categoryLabel();
    const area = this.areaLabel();
    const city = this.marketName();
    if (category && area) {
      return `${category} in ${area}`;
    }
    if (category) {
      return `${category} in ${city}`;
    }
    if (area) {
      return this.tr("discover.seoMarket.beautyWellness", { area: area });
    }
    return this.tr("discover.seoMarket.beautyWellness2", { city: city });
  });
  readonly lede = computed(() => {
    const category = this.categoryLabel();
    const area = this.areaLabel();
    const city = this.marketName();
    const place = area ?? city;
    if (category) {
      return this.tr("discover.seoMarket.verifiedProfessionalsCompareServicesSee", { toLowerCase: category.toLowerCase(), place: place });
    }
    if (area) {
      return this.tr("discover.seoMarket.discoverVerifiedSalonsSpasWellness", { area: area, city: city });
    }
    return this.tr("discover.seoMarket.discoverVerifiedSalonsSpasWellness2", { city: city });
  });
  readonly categoryLinks = computed(() => {
    const marketId = this.marketId();
    const area = this.areaSlug();
    const featured = new Set(getMarketFeaturedCategorySlugs(marketId, 12));
    const fromApi = this.categories().filter((c) => featured.has(c.slug) || featured.size === 0);
    const slugs =
      fromApi.length > 0
        ? fromApi.map((c) => c.slug)
        : getMarketFeaturedCategorySlugs(marketId, 8);
    return slugs.map((slug) => ({
      slug,
      label: getCategoryLabel(this.locale(), slug),
      path: area
        ? seoNeighborhoodCategoryPath(marketId, area, slug)
        : seoCategoryPath(marketId, slug),
    }));
  });
  readonly neighborhoodLinks = computed(() =>
    listSeoNeighborhoods(this.marketId()).map((n) => ({
      ...n,
      path: seoNeighborhoodPath(n.marketId, n.slug),
    })),
  );

  async ngOnInit(): Promise<void> {
    const marketId = (this.route.snapshot.paramMap.get("marketId") ?? "").toLowerCase();
    if (!getMarketById(marketId)) {
      void this.router.navigateByUrl("/");
      return;
    }

    const areaParam = this.route.snapshot.paramMap.get("areaSlug")?.toLowerCase() ?? null;
    const categoryParam =
      this.route.snapshot.paramMap.get("categorySlug")?.toLowerCase() ?? null;
    const segment = this.route.snapshot.paramMap.get("segment")?.toLowerCase() ?? null;

    let areaSlug: string | null = areaParam;
    let categorySlug: string | null = categoryParam;

    if (segment) {
      if (getSeoNeighborhood(marketId, segment)) {
        areaSlug = segment;
      } else if (isSeoCategorySlug(segment)) {
        categorySlug = segment;
      } else {
        void this.router.navigateByUrl("/");
        return;
      }
    }

    if (areaSlug && !getSeoNeighborhood(marketId, areaSlug)) {
      void this.router.navigateByUrl("/");
      return;
    }
    if (categorySlug && !isSeoCategorySlug(categorySlug)) {
      void this.router.navigateByUrl("/");
      return;
    }

    this.marketId.set(marketId);
    this.areaSlug.set(areaSlug);
    this.categorySlug.set(categorySlug);
    this.market.applyMarketQueryParam(marketId);

    const cached = this.transferState.get(SEO_MARKET_STATE_KEY, null);
    if (
      cached
      && cached.marketId === marketId
      && cached.categorySlug === categorySlug
      && cached.areaSlug === areaSlug
    ) {
      this.categories.set(cached.categories);
      this.items.set(cached.items);
      this.loading.set(false);
      this.transferState.remove(SEO_MARKET_STATE_KEY);
      this.applySeo();
      return;
    }

    await this.pendingTasks.run(() => this.load());
  }

  exploreQuery(): { market: string; category?: string; q?: string } {
    const market = this.marketId();
    const category = this.categorySlug();
    const q = this.neighborhood()?.query;
    const params: { market: string; category?: string; q?: string } = { market };
    if (category) {
      params.category = category;
    }
    if (q) {
      params.q = q;
    }
    return params;
  }

  marketHubHref(): string {
    return seoMarketPath(this.marketId());
  }

  areaHubHref(): string | null {
    const area = this.areaSlug();
    return area ? seoNeighborhoodPath(this.marketId(), area) : null;
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    const client = this.api.createPublicClient();
    const marketId = this.marketId();
    const category = this.categorySlug();
    const areaQuery = this.neighborhood()?.query ?? null;
    const market = getMarketById(marketId);
    const loc = market?.defaultLocation ?? this.config.defaultLocation;

    const [categoriesResult, searchResult] = await Promise.allSettled([
      client.getCategories({ market: marketId, wellness: true }),
      client.searchDiscovery({
        lat: loc.lat,
        lng: loc.lng,
        market: marketId,
        category,
        q: areaQuery ?? undefined,
        page: 1,
        pageSize: FEATURED_PAGE_SIZE,
        sort: "featured",
      }),
    ]);

    const categories =
      categoriesResult.status === "fulfilled" ? categoriesResult.value : [];
    const items =
      searchResult.status === "fulfilled" ? searchResult.value.items : [];

    this.categories.set(categories);
    this.items.set(items);
    this.transferState.set(SEO_MARKET_STATE_KEY, {
      marketId,
      categorySlug: category,
      areaSlug: this.areaSlug(),
      categories,
      items,
    });
    this.loading.set(false);
    this.applySeo();
  }

  private applySeo(): void {
    const city = this.marketName();
    const category = this.categoryLabel();
    const area = this.areaLabel();
    const areaSlug = this.areaSlug();
    const categorySlug = this.categorySlug();

    let path = seoMarketPath(this.marketId());
    if (areaSlug && categorySlug) {
      path = seoNeighborhoodCategoryPath(this.marketId(), areaSlug, categorySlug);
    } else if (areaSlug) {
      path = seoNeighborhoodPath(this.marketId(), areaSlug);
    } else if (categorySlug) {
      path = seoCategoryPath(this.marketId(), categorySlug);
    }

    const place = area ?? city;
    const title = category
      ? `${category} in ${place} — Adeni`
      : area
        ? this.tr("discover.seoMarket.beautyWellnessAdeni", { area: area })
        : this.tr("discover.seoMarket.beautyWellnessAdeni2", { city: city });
    const description = category
      ? this.tr("discover.seoMarket.bookVerifiedRealAvailabilityReviews", { toLowerCase: category.toLowerCase(), place: place })
      : area
        ? this.tr("discover.seoMarket.bookVerifiedBeautyWellnessReal", { area: area, city: city })
        : t(this.locale(), "discover.metaDescription", { market: city });

    this.seo.update(
      {
        title,
        description,
        canonicalPath: path,
        imageUrl: this.items()[0]?.coverImageUrl ?? null,
      },
      this.config.publicAppUrl,
    );
  }
}
