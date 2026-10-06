import { Component, computed, inject, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  buildLocaleRegionPresets,
  formatFooterLanguageLabel,
  getCurrencySymbol,
  listMarkets,
  t,
  type LocaleRegionPreset,
} from "@adeni/shared";
import { ADENI_DISCOVER_CONFIG } from "../core/adeni-config";
import { DiscoverLocaleService } from "../core/services/discover-locale.service";
import { MarketContextService } from "../core/services/market-context.service";

type FooterLink = { labelKey: string; href: string; external?: boolean };
type FooterSection = { titleKey: string; links: FooterLink[] };

@Component({
  selector: "app-public-footer",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./public-footer.component.html",
  styleUrl: "./public-footer.component.scss",
})
export class PublicFooterComponent {
  private readonly market = inject(MarketContextService);
  private readonly localeService = inject(DiscoverLocaleService);
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

  readonly sections = computed((): FooterSection[] => {
    const portal = this.config.portalAppUrl.trim();
    return [
      {
        titleKey: "footer.sections.support",
        links: [
          { labelKey: "footer.links.helpCentre", href: "/discover" },
          { labelKey: "footer.links.trustVerification", href: "/discover" },
          { labelKey: "footer.links.bookingHelp", href: "/my-bookings" },
          { labelKey: "footer.links.contactUs", href: "/discover" },
        ],
      },
      {
        titleKey: "footer.sections.discover",
        links: [
          { labelKey: "footer.links.browseServices", href: "/discover" },
          {
            labelKey: "footer.links.beautyGrooming",
            href: "/discover?category=hair-grooming",
          },
          {
            labelKey: "footer.links.verifiedProviders",
            href: "/discover",
          },
        ],
      },
      {
        titleKey: "footer.sections.business",
        links: [
          {
            labelKey: "footer.links.listBusiness",
            href: portal ? `${portal}/register` : "/discover",
            external: Boolean(portal),
          },
          {
            labelKey: "footer.links.businessPortal",
            href: portal || "/discover",
            external: Boolean(portal),
          },
        ],
      },
      {
        titleKey: "footer.sections.adeni",
        links: [
          { labelKey: "footer.links.howItWorks", href: "/" },
          { labelKey: "footer.links.about", href: "/" },
          { labelKey: "footer.links.privacy", href: "/privacy" },
          { labelKey: "footer.links.terms", href: "/terms" },
        ],
      },
    ];
  });

  readonly legalLinks: FooterLink[] = [
    { labelKey: "footer.links.privacy", href: "/privacy" },
    { labelKey: "footer.links.terms", href: "/terms" },
    { labelKey: "footer.links.sitemap", href: "/discover" },
  ];

  label(key: string): string {
    return t(this.locale(), key);
  }

  queryParams(href: string): Record<string, string> {
    const queryIndex = href.indexOf("?");
    if (queryIndex < 0) {
      return {};
    }

    const params = new URLSearchParams(href.slice(queryIndex + 1));
    const result: Record<string, string> = {};
    for (const [key, value] of params.entries()) {
      result[key] = value;
    }
    return result;
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

  isCurrent(preset: LocaleRegionPreset): boolean {
    return (
      preset.locale === this.locale() &&
      preset.marketId === (this.activeMarket()?.id ?? "")
    );
  }
}
