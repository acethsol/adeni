import { DecimalPipe } from "@angular/common";
import { Component, inject, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { FormsModule } from "@angular/forms";
import type { Category, DiscoveryBusinessItem } from "@adeni/shared";
import {
  DISCOVERY_PAGE_SIZE,
  getCategoryLabel,
  resolveBusinessCoverImage,
} from "@adeni/shared";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";
import { CustomerApiService } from "../../core/services/customer-api.service";
import { MarketContextService } from "../../core/services/market-context.service";
import { SeoService } from "../../core/services/seo.service";

@Component({
  selector: "app-discover-page",
  standalone: true,
  imports: [RouterLink, FormsModule, DecimalPipe],
  templateUrl: "./discover.component.html",
  styleUrl: "./discover.component.scss",
})
export class DiscoverComponent {
  private readonly api = inject(CustomerApiService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly route = inject(ActivatedRoute);
  readonly market = inject(MarketContextService);
  private readonly seo = inject(SeoService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly items = signal<DiscoveryBusinessItem[]>([]);
  readonly totalCount = signal(0);

  searchQuery = "";
  selectedCategory = "";
  sort: "distance" | "featured" = "distance";
  minRating: number | null = null;

  readonly coverFor = resolveBusinessCoverImage;
  readonly categoryLabel = getCategoryLabel;

  constructor() {
    this.route.queryParamMap.subscribe(() => {
      const params = this.route.snapshot.queryParamMap;
      this.selectedCategory = params.get("category") ?? "";
      this.searchQuery = params.get("q") ?? "";
      const sortParam = params.get("sort");
      this.sort = sortParam === "featured" ? "featured" : "distance";
      const rating = Number(params.get("minRating"));
      this.minRating =
        Number.isFinite(rating) && rating >= 1 && rating <= 5 ? Math.round(rating) : null;
      void this.load();
    });
  }

  filterQuery(extra: Record<string, string | number | null | undefined> = {}): Record<string, string | null> {
    const category =
      "category" in extra ? (extra["category"] as string | null) : this.selectedCategory || null;
    const sortValue =
      "sort" in extra
        ? (extra["sort"] as string | null)
        : this.sort === "featured"
          ? "featured"
          : null;
    const minRatingValue =
      "minRating" in extra
        ? extra["minRating"] == null
          ? null
          : String(extra["minRating"])
        : this.minRating != null
          ? String(this.minRating)
          : null;

    return {
      category: category || null,
      q: this.searchQuery.trim() || null,
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
        title: `Discover — Adeni ${marketName}`,
        description: `Find and book verified local services in ${marketName}.`,
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
          category: this.selectedCategory || undefined,
          q: this.searchQuery.trim() || undefined,
          sort: this.sort,
          minRating: this.minRating ?? undefined,
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
        this.error.set("Could not load discovery results. Is the API running?");
        this.items.set([]);
        this.totalCount.set(0);
      }
    } catch {
      this.error.set("Could not load discovery results. Is the API running?");
      this.items.set([]);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }
}
