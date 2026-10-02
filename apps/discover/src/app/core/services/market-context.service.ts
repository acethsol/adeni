import { inject, Injectable, signal } from "@angular/core";
import {
  COORDS_COOKIE_NAME,
  formatCoordinatePair,
  MARKET_COOKIE_NAME,
  parseCoordinatePair,
  resolveMarket,
  resolveSearchLocation,
  type MarketConfig,
  type MarketLocation,
} from "@adeni/shared";
import { readCookie, writeCookie } from "../cookie.util";
import { ADENI_DISCOVER_CONFIG } from "../adeni-config";
import { CustomerApiService } from "./customer-api.service";

const MARKET_COOKIE_MAX_AGE = 60 * 60 * 24 * 365;
const COORDS_COOKIE_MAX_AGE = 60 * 60 * 24;

@Injectable({ providedIn: "root" })
export class MarketContextService {
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly api = inject(CustomerApiService);

  private catalog: MarketConfig[] = [];

  readonly ready = signal(false);
  readonly market = signal<MarketConfig | null>(null);
  readonly searchLocation = signal<MarketLocation>(this.config.defaultLocation);

  private bootPromise: Promise<void> | null = null;

  bootstrap(): Promise<void> {
    if (!this.bootPromise) {
      this.bootPromise = this.doBootstrap();
    }
    return this.bootPromise;
  }

  private async doBootstrap(): Promise<void> {
    try {
      this.catalog = await this.api.createPublicClient().getMarkets();
    } catch {
      this.catalog = [];
    }

    this.applyResolution();
    this.ready.set(true);
  }

  applyMarketQueryParam(marketId: string | null): void {
    if (!marketId?.trim()) {
      return;
    }

    const normalized = marketId.trim().toLowerCase();
    const exists = this.catalog.some((m) => m.id === normalized);
    if (!exists && this.catalog.length > 0) {
      return;
    }

    writeCookie(MARKET_COOKIE_NAME, normalized, MARKET_COOKIE_MAX_AGE);
    this.applyResolution();
  }

  setCoordinates(coords: MarketLocation): void {
    writeCookie(
      COORDS_COOKIE_NAME,
      formatCoordinatePair(coords),
      COORDS_COOKIE_MAX_AGE,
    );
    this.applyResolution();
  }

  private applyResolution(): void {
    const cookieMarket = readCookie(MARKET_COOKIE_NAME);
    const coords = parseCoordinatePair(readCookie(COORDS_COOKIE_NAME));
    const envMarket = this.config.envMarketId || null;

    const resolved = resolveMarket(
      {
        marketId: cookieMarket ?? envMarket,
        coordinates: coords,
      },
      this.catalog.length > 0 ? this.catalog : undefined,
    );

    this.market.set(resolved.market);
    this.searchLocation.set(resolveSearchLocation(resolved.market, coords));
  }
}
