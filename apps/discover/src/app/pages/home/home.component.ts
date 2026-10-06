import {
  Component,
  computed,
  effect,
  inject,
  makeStateKey,
  OnInit,
  PendingTasks,
  signal,
  TransferState,
} from "@angular/core";
import { RouterLink } from "@angular/router";
import type { Category, DiscoveryBusinessItem } from "@adeni/shared";
import {
  FEATURED_PAGE_SIZE,
  getCategoryGroupLabel,
  getCategoryLabel,
  getCategoryVisual,
  getMarketDescription,
  getMarketTagline,
  t,
} from "@adeni/shared";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";
import { CustomerApiService } from "../../core/services/customer-api.service";
import { DiscoverLocaleService } from "../../core/services/discover-locale.service";
import { MarketContextService } from "../../core/services/market-context.service";
import { SeoService } from "../../core/services/seo.service";
import { DiscoveryBusinessCardComponent } from "../../shared/discovery-business-card.component";
import { HeroCategoryStageComponent } from "../../shared/hero-category-stage.component";

const HOME_STATE_KEY = makeStateKey<{
  categories: Category[];
  featured: DiscoveryBusinessItem[];
}>("discover-home");

type CategoryGroup = {
  slug: string;
  label: string;
  icon: string;
  items: Category[];
};

@Component({
  selector: "app-home",
  standalone: true,
  imports: [RouterLink, HeroCategoryStageComponent, DiscoveryBusinessCardComponent],
  templateUrl: "./home.component.html",
  styleUrl: "./home.component.scss",
})
export class HomeComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly api = inject(CustomerApiService);
  private readonly pendingTasks = inject(PendingTasks);
  private readonly transferState = inject(TransferState);
  private readonly localeService = inject(DiscoverLocaleService);
  private readonly hydratedFromServer = signal(false);
  private allowMarketReload = false;
  private lastLoadedMarketId: string | null = null;
  readonly market = inject(MarketContextService);

  readonly locale = this.localeService.locale;
  readonly tagline = computed(() => getMarketTagline(this.locale()));
  readonly description = computed(() => getMarketDescription(this.locale()));
  readonly popularTitle = computed(() => t(this.locale(), "home.popularNearYou"));
  readonly popularDescription = computed(() => t(this.locale(), "home.popularDescription"));
  readonly seeAllLabel = computed(() => t(this.locale(), "home.seeAll"));
  readonly browseCategoryTitle = computed(() => t(this.locale(), "home.browseCategory"));
  readonly browseCategoryDescription = computed(() =>
    t(this.locale(), "home.browseCategoryDescription"),
  );

  readonly loading = signal(true);
  readonly offlineMessage = signal<string | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly featured = signal<DiscoveryBusinessItem[]>([]);
  readonly categoryGroups = computed(() => this.groupCategories(this.categories()));

  readonly categoryLabel = (slug: string, name?: string) =>
    getCategoryLabel(this.locale(), slug, name);
  readonly categoryVisual = (category: Category) =>
    getCategoryVisual(category.slug, this.categoryLabel(category.slug, category.name));

  constructor() {
    const cached = this.transferState.get(HOME_STATE_KEY, null);
    if (cached) {
      this.categories.set(cached.categories);
      this.featured.set(cached.featured);
      this.loading.set(false);
      this.hydratedFromServer.set(true);
      this.transferState.remove(HOME_STATE_KEY);
    }

    effect(() => {
      const marketId = this.market.market()?.id ?? null;
      if (!this.allowMarketReload || !marketId || marketId === this.lastLoadedMarketId) {
        return;
      }
      void this.load();
    });
  }

  async ngOnInit(): Promise<void> {
    await this.market.bootstrap();
    const marketName = this.market.market()?.name ?? "Adeni";
    this.seo.update(
      {
        title: t(this.locale(), "home.metaTitle", {
          tagline: this.tagline(),
          market: marketName,
        }),
        description: t(this.locale(), "home.metaDescription", {
          market: marketName,
          description: this.description(),
        }),
        canonicalPath: "/",
      },
      this.config.publicAppUrl,
    );
    if (this.hydratedFromServer()) {
      this.lastLoadedMarketId = this.market.market()?.id ?? null;
      this.allowMarketReload = true;
      return;
    }

    await this.pendingTasks.run(() => this.load());
    this.allowMarketReload = true;
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.offlineMessage.set(null);
    const client = this.api.createPublicClient();
    const loc = this.market.searchLocation();
    const marketId = this.market.market()?.id ?? this.config.defaultMarketId;

    const [categoriesResult, featuredResult] = await Promise.allSettled([
      client.getCategories({ market: marketId, wellness: true }),
      client.searchDiscovery({
        lat: loc.lat,
        lng: loc.lng,
        market: marketId,
        page: 1,
        pageSize: FEATURED_PAGE_SIZE,
        sort: "featured",
      }),
    ]);

    if (categoriesResult.status === "fulfilled") {
      this.categories.set(categoriesResult.value);
    } else {
      this.categories.set([]);
      this.offlineMessage.set(
        t(this.locale(), "home.apiOffline", { url: this.config.apiBaseUrl }),
      );
    }

    const featured =
      featuredResult.status === "fulfilled" ? featuredResult.value.items : [];
    this.featured.set(featured);
    this.lastLoadedMarketId = marketId;
    this.transferState.set(HOME_STATE_KEY, {
      categories: this.categories(),
      featured,
    });
    this.loading.set(false);
  }

  private groupCategories(categories: Category[]): CategoryGroup[] {
    const groups = new Map<string, Category[]>();
    for (const category of categories) {
      const key = category.parentSlug?.trim() || "general";
      const existing = groups.get(key) ?? [];
      existing.push(category);
      groups.set(key, existing);
    }

    return [...groups.entries()].map(([slug, items]) => {
      const label = getCategoryGroupLabel(this.locale(), slug);
      return {
        slug,
        label,
        icon: getCategoryVisual(slug, label).icon,
        items,
      };
    });
  }
}
