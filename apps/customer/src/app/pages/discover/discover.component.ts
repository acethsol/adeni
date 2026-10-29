import {
  afterNextRender,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  Injector,
  runInInjectionContext,
  signal,
  viewChild,
} from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import type { Category, DiscoveryBusinessItem } from "@adeni/shared";
import {
  DISCOVERY_PAGE_SIZE,
  getCategoryLabel,
  getCategoryVisual,
  t,
} from "@adeni/shared";
import { AdeniCarbonIconComponent, AdeniLocaleService } from "@adeni/ui";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";
import { CustomerApiService } from "../../core/services/customer-api.service";
import { DiscoverLoadingService } from "../../core/services/discover-loading.service";
import { MarketContextService } from "../../core/services/market-context.service";
import { SeoService } from "../../core/services/seo.service";
import { DiscoveryBusinessCardComponent } from "../../shared/discovery-business-card.component";
import { DiscoveryMapComponent } from "../../shared/discovery-map.component";

const MARKET_TONES: Record<string, { h1: string; h2: string; h3: string }> = {
  lagos: { h1: "#7f56ff", h2: "#13c2a3", h3: "#6f42ff" },
  ottawa: { h1: "#6f42ff", h2: "#13c2a3", h3: "#7f56ff" },
};

const LOAD_MORE_PANEL_DELAY_MS = 450;

@Component({
  selector: "app-discover-page",
  standalone: true,
  imports: [
    RouterLink,
    DiscoveryBusinessCardComponent,
    DiscoveryMapComponent,
    AdeniCarbonIconComponent,
  ],
  templateUrl: "./discover.component.html",
  styleUrl: "./discover.component.scss",
})
export class DiscoverComponent {
  private readonly api = inject(CustomerApiService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly route = inject(ActivatedRoute);
  private readonly localeService = inject(AdeniLocaleService);
  readonly market = inject(MarketContextService);
  private readonly seo = inject(SeoService);
  private readonly pageLoading = inject(DiscoverLoadingService);
  private readonly injector = inject(Injector);
  private readonly loadMoreSentinel = viewChild<ElementRef<HTMLElement>>("loadMoreSentinel");

  readonly loading = signal(true);
  readonly loadingMore = signal(false);
  readonly loadFailed = signal(false);
  readonly loadMoreError = signal<string | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly items = signal<DiscoveryBusinessItem[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly activeLocationId = signal<string | null>(null);
  readonly mobileView = signal<"list" | "map">("list");

  readonly searchQuery = signal("");
  readonly selectedCategory = signal("");
  readonly sort = signal<"distance" | "featured">("distance");
  readonly minRating = signal<number | null>(null);

  readonly locale = this.localeService.locale;
  readonly pageTone = computed(() => {
    const id = this.market.market()?.id ?? "lagos";
    return MARKET_TONES[id] ?? MARKET_TONES["lagos"];
  });
  readonly marketName = computed(() => this.market.market()?.name ?? "Adeni");
  readonly selectedCategoryLabel = computed(() => {
    const slug = this.selectedCategory();
    if (!slug) {
      return null;
    }
    const match = this.categories().find((category) => category.slug === slug);
    return getCategoryLabel(this.locale(), slug, match?.name);
  });
  readonly resultLabel = computed(() => {
    const count = this.totalCount();
    const category = this.selectedCategoryLabel();
    const market = this.marketName();
    if (category) {
      return count === 1
        ? t(this.locale(), "discover.placesCountCategoryOne", { category })
        : t(this.locale(), "discover.placesCountCategory", { count, category });
    }
    return count === 1
      ? t(this.locale(), "discover.placesCountOne", { market })
      : t(this.locale(), "discover.placesCount", { count, market });
  });
  readonly headline = computed(() => this.resultLabel());
  readonly kicker = computed(() => {
    const category = this.selectedCategoryLabel();
    return category ?? this.marketName();
  });
  readonly hasMore = computed(
    () => this.page() * DISCOVERY_PAGE_SIZE < this.totalCount(),
  );
  readonly filterSummary = computed(() => {
    const parts: { key: string; label: string; clear: Record<string, string | null> }[] = [];
    const category = this.selectedCategoryLabel();
    if (category) {
      parts.push({
        key: "category",
        label: category,
        clear: this.filterQuery({ category: null }),
      });
    }
    if (this.searchQuery().trim()) {
      parts.push({
        key: "q",
        label: `“${this.searchQuery().trim()}”`,
        clear: { ...this.filterQuery(), q: null },
      });
    }
    if (this.sort() === "featured") {
      parts.push({
        key: "sort",
        label: t(this.locale(), "discover.sortFeatured"),
        clear: this.filterQuery({ sort: "distance" }),
      });
    }
    if (this.minRating() != null) {
      parts.push({
        key: "rating",
        label: t(this.locale(), "discover.minRatingValue", { value: this.minRating()! }),
        clear: this.filterQuery({ minRating: null }),
      });
    }
    return parts;
  });
  readonly hasActiveFilters = computed(() => this.filterSummary().length > 0);
  readonly showMap = computed(() => Boolean(this.config.mapboxAccessToken.trim()));
  readonly mapCenter = computed(() => {
    const loc = this.market.searchLocation();
    return { lat: loc.lat, lng: loc.lng };
  });

  private lastLoadedMarketId: string | null = null;
  private allowMarketReload = false;
  private loadGeneration = 0;
  private loadMorePanelTimer: ReturnType<typeof setTimeout> | null = null;
  private loadMorePanelShown = false;

  constructor() {
    this.route.queryParamMap.subscribe(() => {
      const params = this.route.snapshot.queryParamMap;
      this.selectedCategory.set(params.get("category") ?? "");
      this.searchQuery.set(params.get("q") ?? "");
      const sortParam = params.get("sort");
      this.sort.set(sortParam === "featured" ? "featured" : "distance");
      const rating = Number(params.get("minRating"));
      this.minRating.set(
        Number.isFinite(rating) && rating >= 1 && rating <= 5 ? Math.round(rating) : null,
      );
      void this.load();
    });

    effect(() => {
      const marketId = this.market.market()?.id ?? null;
      if (!this.allowMarketReload || !marketId || marketId === this.lastLoadedMarketId) {
        return;
      }
      void this.load();
    });

    afterNextRender(() => {
      runInInjectionContext(this.injector, () => {
        effect((onCleanup) => {
          const node = this.loadMoreSentinel()?.nativeElement ?? null;
          if (!node || typeof IntersectionObserver === "undefined") {
            return;
          }

          const observer = new IntersectionObserver(
            (entries) => {
              if (entries.some((entry) => entry.isIntersecting)) {
                void this.loadMore();
              }
            },
            { root: null, rootMargin: "480px 0px", threshold: 0 },
          );
          observer.observe(node);
          onCleanup(() => observer.disconnect());
        });
      });
    });
  }

  retryLoadMore(): void {
    this.loadMoreError.set(null);
    void this.loadMore();
  }

  onMarkerSelect(locationId: string): void {
    this.activeLocationId.set(locationId);
    this.mobileView.set("list");
    setTimeout(() => {
      const node = document.querySelector(".grid > li.active") as HTMLElement | null;
      node?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }, 0);
  }

  categoryVisual(slug: string, name?: string) {
    return getCategoryVisual(slug, getCategoryLabel(this.locale(), slug, name));
  }

  categoryLabel(slug: string, name?: string) {
    return getCategoryLabel(this.locale(), slug, name);
  }

  label(key: string, vars?: Record<string, string | number>) {
    return t(this.locale(), key, vars);
  }

  filterQuery(extra: Record<string, string | number | null | undefined> = {}): Record<string, string | null> {
    const category =
      "category" in extra ? (extra["category"] as string | null) : this.selectedCategory() || null;
    const sortValue =
      "sort" in extra
        ? (extra["sort"] as string | null)
        : this.sort() === "featured"
          ? "featured"
          : null;
    const minRatingValue =
      "minRating" in extra
        ? extra["minRating"] == null
          ? null
          : String(extra["minRating"])
        : this.minRating() != null
          ? String(this.minRating())
          : null;
    const q =
      "q" in extra ? (extra["q"] as string | null) : this.searchQuery().trim() || null;

    return {
      category: category || null,
      q: q?.trim() || null,
      sort: sortValue === "featured" ? "featured" : null,
      minRating: minRatingValue,
    };
  }

  async load(): Promise<void> {
    const generation = ++this.loadGeneration;
    this.clearLoadMorePanel();
    await this.market.bootstrap();
    if (generation !== this.loadGeneration) {
      return;
    }

    this.loading.set(true);
    this.loadingMore.set(false);
    this.loadFailed.set(false);
    this.loadMoreError.set(null);
    this.page.set(1);
    this.activeLocationId.set(null);
    this.pageLoading.show(
      t(this.locale(), "discover.loadingMore"),
      t(this.locale(), "discover.loadingMorePanel"),
    );

    const client = this.api.createPublicClient();
    const loc = this.market.searchLocation();
    const marketId = this.market.market()?.id ?? this.config.defaultMarketId;
    const marketName = this.market.market()?.name ?? "Adeni";

    this.seo.update(
      {
        title: t(this.locale(), "discover.metaTitle", { market: marketName }),
        description: t(this.locale(), "discover.metaDescription", { market: marketName }),
        canonicalPath: "/discover",
      },
      this.config.publicAppUrl,
    );

    try {
      const [categoriesResult, discoveryResult] = await Promise.allSettled([
        client.getCategories({ market: marketId, wellness: true }),
        client.searchDiscovery({
          lat: loc.lat,
          lng: loc.lng,
          market: marketId,
          page: 1,
          pageSize: DISCOVERY_PAGE_SIZE,
          category: this.selectedCategory() || undefined,
          q: this.searchQuery().trim() || undefined,
          sort: this.sort(),
          minRating: this.minRating() ?? undefined,
        }),
      ]);

      if (generation !== this.loadGeneration) {
        return;
      }

      if (categoriesResult.status === "fulfilled") {
        this.categories.set(categoriesResult.value);
      } else {
        this.categories.set([]);
      }

      if (discoveryResult.status === "fulfilled") {
        this.items.set(discoveryResult.value.items);
        this.totalCount.set(discoveryResult.value.totalCount);
        this.activeLocationId.set(discoveryResult.value.items[0]?.locationId ?? null);
        this.loadFailed.set(false);
      } else {
        console.error("Discovery load failed", discoveryResult.reason);
        this.loadFailed.set(true);
        this.items.set([]);
        this.totalCount.set(0);
      }
    } catch (err) {
      if (generation !== this.loadGeneration) {
        return;
      }
      console.error("Discovery load failed", err);
      this.loadFailed.set(true);
      this.items.set([]);
      this.totalCount.set(0);
    } finally {
      this.pageLoading.hide();
      if (generation === this.loadGeneration) {
        this.lastLoadedMarketId = marketId;
        this.allowMarketReload = true;
        this.loading.set(false);
      }
    }
  }

  async loadMore(): Promise<void> {
    if (this.loading() || this.loadingMore() || this.loadMoreError() || !this.hasMore()) {
      return;
    }

    const generation = this.loadGeneration;
    const nextPage = this.page() + 1;
    let continuePaging = false;
    this.loadingMore.set(true);
    this.scheduleLoadMorePanel();

    const client = this.api.createPublicClient();
    const loc = this.market.searchLocation();
    const marketId = this.market.market()?.id ?? this.config.defaultMarketId;

    try {
      const result = await client.searchDiscovery({
        lat: loc.lat,
        lng: loc.lng,
        market: marketId,
        page: nextPage,
        pageSize: DISCOVERY_PAGE_SIZE,
        category: this.selectedCategory() || undefined,
        q: this.searchQuery().trim() || undefined,
        sort: this.sort(),
        minRating: this.minRating() ?? undefined,
      });

      if (generation !== this.loadGeneration) {
        return;
      }

      this.page.set(nextPage);
      this.totalCount.set(result.totalCount);

      if (result.items.length > 0) {
        this.items.update((current) => {
          const seen = new Set(current.map((item) => item.locationId));
          const appended = result.items.filter((item) => !seen.has(item.locationId));
          return appended.length > 0 ? [...current, ...appended] : current;
        });
      } else if (nextPage * DISCOVERY_PAGE_SIZE < result.totalCount) {
        continuePaging = true;
      }
    } catch (reason) {
      if (generation !== this.loadGeneration) {
        return;
      }
      console.error("Discovery load more failed", reason);
      this.loadMoreError.set(t(this.locale(), "discover.loadMoreError"));
    } finally {
      if (generation === this.loadGeneration) {
        this.loadingMore.set(false);
        this.clearLoadMorePanel();
      }
    }

    if (continuePaging && generation === this.loadGeneration) {
      void this.loadMore();
    }
  }

  private scheduleLoadMorePanel(): void {
    this.clearLoadMorePanel();
    this.loadMorePanelTimer = setTimeout(() => {
      if (this.loadingMore()) {
        this.pageLoading.show(
          t(this.locale(), "discover.loadingMore"),
          t(this.locale(), "discover.loadingMorePanel"),
        );
        this.loadMorePanelShown = true;
      }
    }, LOAD_MORE_PANEL_DELAY_MS);
  }

  private clearLoadMorePanel(): void {
    if (this.loadMorePanelTimer) {
      clearTimeout(this.loadMorePanelTimer);
      this.loadMorePanelTimer = null;
    }
    if (this.loadMorePanelShown) {
      this.pageLoading.hide();
      this.loadMorePanelShown = false;
    }
  }
}
