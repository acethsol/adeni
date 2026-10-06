import { Component, computed, effect, ElementRef, HostListener, inject, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import type { Category, DiscoveryBusinessItem } from "@adeni/shared";
import {
  DISCOVERY_PAGE_SIZE,
  getCategoryLabel,
  getCategoryVisual,
  t,
} from "@adeni/shared";
import { AdeniLocaleService } from "@adeni/ui";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";
import { CustomerApiService } from "../../core/services/customer-api.service";
import { MarketContextService } from "../../core/services/market-context.service";
import { SeoService } from "../../core/services/seo.service";
import { DiscoveryBusinessCardComponent } from "../../shared/discovery-business-card.component";

const MARKET_TONES: Record<string, { h1: string; h2: string; h3: string }> = {
  lagos: { h1: "#1b4332", h2: "#13c2a3", h3: "#d8a23a" },
  ottawa: { h1: "#123b4a", h2: "#2a9d8f", h3: "#7eb8c9" },
};

@Component({
  selector: "app-discover-page",
  standalone: true,
  imports: [RouterLink, DiscoveryBusinessCardComponent],
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

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly items = signal<DiscoveryBusinessItem[]>([]);
  readonly totalCount = signal(0);

  readonly searchQuery = signal("");
  readonly selectedCategory = signal("");
  readonly sort = signal<"distance" | "featured">("distance");
  readonly minRating = signal<number | null>(null);
  readonly filtersOpen = signal(false);

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
  readonly filterCount = computed(
    () =>
      (this.sort() === "featured" ? 1 : 0) +
      (this.minRating() != null ? 1 : 0),
  );
  private readonly host = inject(ElementRef<HTMLElement>);
  private lastLoadedMarketId: string | null = null;
  private allowMarketReload = false;

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
      this.filtersOpen.set(false);
      void this.load();
    });

    effect(() => {
      const marketId = this.market.market()?.id ?? null;
      if (!this.allowMarketReload || !marketId || marketId === this.lastLoadedMarketId) {
        return;
      }
      void this.load();
    });
  }

  toggleFilters(): void {
    this.filtersOpen.update((open) => !open);
  }

  @HostListener("document:mousedown", ["$event"])
  onDocumentMouseDown(event: MouseEvent): void {
    if (!this.filtersOpen()) {
      return;
    }
    const target = event.target as Node;
    if (!this.host.nativeElement.querySelector(".filters-wrap")?.contains(target)) {
      this.filtersOpen.set(false);
    }
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
    await this.market.bootstrap();
    this.loading.set(true);
    this.error.set(null);
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

      if (categoriesResult.status === "fulfilled") {
        this.categories.set(categoriesResult.value);
      } else {
        this.categories.set([]);
      }

      if (discoveryResult.status === "fulfilled") {
        this.items.set(discoveryResult.value.items);
        this.totalCount.set(discoveryResult.value.totalCount);
      } else {
        console.error("Discovery load failed", discoveryResult.reason);
        this.error.set(t(this.locale(), "discover.loadError"));
        this.items.set([]);
        this.totalCount.set(0);
      }
    } catch {
      this.error.set(t(this.locale(), "discover.loadError"));
      this.items.set([]);
      this.totalCount.set(0);
    } finally {
      this.lastLoadedMarketId = marketId;
      this.allowMarketReload = true;
      this.loading.set(false);
    }
  }
}
