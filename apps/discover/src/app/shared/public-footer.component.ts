import { Component, computed, inject, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  buildLocaleRegionPresets,
  formatFooterLanguageLabel,
  getCurrencySymbol,
  listMarkets,
  t,
  type LocaleRegionPreset,
  type MarketConfig,
} from "@adeni/shared";
import { AdeniBrandLockupComponent, AdeniLocaleService } from "@adeni/ui";
import { ADENI_DISCOVER_CONFIG } from "../core/adeni-config";
import { MarketContextService } from "../core/services/market-context.service";

@Component({
  selector: "app-public-footer",
  standalone: true,
  imports: [RouterLink, AdeniBrandLockupComponent],
  templateUrl: "./public-footer.component.html",
  styleUrl: "./public-footer.component.scss",
})
export class PublicFooterComponent {
  private readonly market = inject(MarketContextService);
  private readonly localeService = inject(AdeniLocaleService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);

  readonly pickerOpen = signal(false);
  readonly year = new Date().getFullYear();

  readonly locale = this.localeService.locale;
  readonly activeMarket = this.market.market;

  readonly presets = computed(() => buildLocaleRegionPresets(listMarkets()));

  readonly languageLabel = computed(() => {
    const market = this.activeMarket();
    if (!market) {
      return formatFooterLanguageLabel(this.locale(), "NG");
    }
    return formatFooterLanguageLabel(this.locale(), market.countryCode);
  });

  readonly currencyLabel = computed(() => {
    const market = this.activeMarket();
    if (!market) {
      return "";
    }
    return `${getCurrencySymbol(market.currency)} ${market.currency}`;
  });

  readonly tagline = computed(() => {
    const market = this.activeMarket();
    if (!market) {
      return t(this.locale(), "footer.taglineFallback");
    }
    return t(this.locale(), "footer.tagline", { market: market.name });
  });

  /** Live markets only — city switches, not category repeats. */
  readonly cityLinks = computed((): MarketConfig[] =>
    listMarkets().filter((m) => m.isLive),
  );

  readonly portalRegisterUrl = computed(() => {
    const portal = this.config.portalAppUrl.trim();
    return portal ? `${portal.replace(/\/$/, "")}/register` : null;
  });

  readonly portalUrl = computed(() => {
    const portal = this.config.portalAppUrl.trim();
    return portal ? portal.replace(/\/$/, "") : null;
  });

  label(key: string, params?: Record<string, string | number>): string {
    return t(this.locale(), key, params);
  }

  openPicker(): void {
    this.pickerOpen.set(true);
  }

  closePicker(): void {
    this.pickerOpen.set(false);
  }

  applyPreset(preset: LocaleRegionPreset): void {
    this.localeService.setLocale(preset.locale);
    this.market.applyMarketQueryParam(preset.marketId);
    this.closePicker();
  }

  selectCity(marketId: string): void {
    this.market.applyMarketQueryParam(marketId);
  }

  isCurrent(preset: LocaleRegionPreset): boolean {
    return (
      preset.locale === this.locale() &&
      preset.marketId === (this.activeMarket()?.id ?? "")
    );
  }
}
