import { Component, computed, effect, inject, OnInit, signal } from "@angular/core";
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { listMarkets } from "@adeni/shared";
import { filter } from "rxjs";
import {
  ADENI_DISCOVER_CONFIG,
  isAuth0Configured,
  isDiscoverCustomerDevMode,
} from "../core/adeni-config";
import { CustomerApiService } from "../core/services/customer-api.service";
import { HeroSearchPinService } from "../core/services/hero-search-pin.service";
import { MarketContextService } from "../core/services/market-context.service";
import { DiscoverySearchComponent } from "../shared/discovery-search.component";
import { MarketGeoSyncComponent } from "../shared/market-geo-sync.component";
import { PublicFooterComponent } from "../shared/public-footer.component";
import {
  AdeniBrandLockupComponent,
  AdeniCarbonIconComponent,
  AdeniConfirmHostComponent,
  AdeniGlobalLoadingPanelComponent,
  AdeniToastHostComponent,
} from "@adeni/ui";

/** Header accent tones — Adeni brand purple/teal (Carbon UI chrome). */
const MARKET_HEADER_TONES: Record<string, { h1: string; h2: string; h3: string }> = {
  lagos: { h1: "#7f56ff", h2: "#13c2a3", h3: "#6f42ff" },
  ottawa: { h1: "#6f42ff", h2: "#13c2a3", h3: "#7f56ff" },
};

@Component({
  selector: "app-public-shell",
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MarketGeoSyncComponent,
    AdeniBrandLockupComponent,
    AdeniCarbonIconComponent,
    DiscoverySearchComponent,
    AdeniGlobalLoadingPanelComponent,
    AdeniToastHostComponent,
    AdeniConfirmHostComponent,
    PublicFooterComponent,
  ],
  templateUrl: "./public-shell.component.html",
  styleUrl: "./public-shell.component.scss",
})
export class PublicShellComponent implements OnInit {
  readonly market = inject(MarketContextService);
  private readonly router = inject(Router);
  readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly customerApi = inject(CustomerApiService);
  private readonly heroPin = inject(HeroSearchPinService);

  readonly auth0Mode = isAuth0Configured(this.config);
  readonly devCustomerMode = isDiscoverCustomerDevMode(this.config);
  readonly signedIn = signal(this.devCustomerMode);
  readonly homeLayout = signal(this.isHome(this.router.url));
  readonly exploreLayout = signal(this.isExplore(this.router.url));
  readonly flushLayout = signal(this.isFlush(this.router.url));
  readonly searchPinned = this.heroPin.pinned;
  readonly searchQuery = signal(this.readSearchQuery(this.router.url));
  readonly showHeaderSearch = computed(
    () => this.exploreLayout() || (this.homeLayout() && this.searchPinned()),
  );
  /** Home scroll-pin only — explores keeps nav + search together. */
  readonly headerCompact = computed(() => this.homeLayout() && this.searchPinned());
  readonly marketMenuOpen = signal(false);
  readonly markets = listMarkets();
  readonly headerTone = computed(() => {
    const id = this.market.market()?.id ?? "lagos";
    return MARKET_HEADER_TONES[id] ?? MARKET_HEADER_TONES["lagos"];
  });
  readonly headerBrandHeight = computed(() => (this.headerCompact() ? 30 : 36));

  get portalUrl(): string | null {
    const origin = this.config.portalAppUrl.trim();
    return origin || null;
  }

  constructor() {
    effect(() => {
      this.heroPin.watch(this.homeLayout());
    });
  }

  ngOnInit(): void {
    this.syncMarketQuery();
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => {
        this.syncMarketQuery();
        this.marketMenuOpen.set(false);
      });

    if (this.auth0Mode) {
      void this.customerApi.isLoggedIn().then((v) => this.signedIn.set(v));
    }
  }

  toggleMarketMenu(): void {
    this.marketMenuOpen.update((open) => !open);
  }

  selectMarket(marketId: string): void {
    this.market.applyMarketQueryParam(marketId);
    this.marketMenuOpen.set(false);
  }

  marketPlaceLine(marketId: string): string {
    if (marketId === "ottawa") {
      return "Canada · Rideau";
    }
    return "Nigeria · Atlantic";
  }

  private syncMarketQuery(): void {
    this.homeLayout.set(this.isHome(this.router.url));
    this.exploreLayout.set(this.isExplore(this.router.url));
    this.flushLayout.set(this.isFlush(this.router.url));
    this.searchQuery.set(this.readSearchQuery(this.router.url));
    const market = this.router.parseUrl(this.router.url).queryParams["market"];
    if (typeof market === "string" && market.trim()) {
      this.market.applyMarketQueryParam(market);
    }
  }

  private isHome(url: string): boolean {
    const primary = this.router.parseUrl(url).root.children["primary"];
    return !primary || primary.segments.length === 0;
  }

  private isExplore(url: string): boolean {
    const primary = this.router.parseUrl(url).root.children["primary"];
    return primary?.segments[0]?.path === "discover";
  }

  private readSearchQuery(url: string): string {
    const q = this.router.parseUrl(url).queryParams["q"];
    return typeof q === "string" ? q : "";
  }

  private isFlush(url: string): boolean {
    if (this.isHome(url) || this.isExplore(url)) {
      return true;
    }
    const primary = this.router.parseUrl(url).root.children["primary"];
    const first = primary?.segments[0]?.path;
    return first === "businesses";
  }

  login(): void {
    void this.customerApi.login(this.router.url);
  }

  logout(): void {
    void this.customerApi.logout();
  }
}
