import { getMarketById, getMarketByIdFromCatalog, listMarkets, type MarketConfig } from "../markets";
import { getLocaleOption, type LocaleId } from "./types";

export type LocaleRegionPreset = {
  id: string;
  locale: LocaleId;
  marketId: string;
  languageLabel: string;
  regionLabel: string;
  countryCode: string;
  currency: string;
  suggested?: boolean;
};

const countryNames: Record<string, string> = {
  NG: "Nigeria",
  CA: "Canada",
  US: "United States",
};

function languageLabel(locale: LocaleId): string {
  return getLocaleOption(locale).nativeLabel;
}

function regionLabelForMarket(
  marketId: string,
  countryCode: string,
  catalog?: MarketConfig[],
): string {
  const market = catalog
    ? getMarketByIdFromCatalog(marketId, catalog)
    : getMarketById(marketId);
  if (!market) {
    return countryNames[countryCode] ?? countryCode;
  }

  return market.name;
}

export function buildLocaleRegionPresets(catalog?: MarketConfig[]): LocaleRegionPreset[] {
  const presets: LocaleRegionPreset[] = [];
  const markets = catalog ?? listMarkets();

  for (const market of markets) {
    for (const locale of market.languages) {
      presets.push({
        id: `${locale}-${market.id}`,
        locale,
        marketId: market.id,
        languageLabel: languageLabel(locale),
        regionLabel: regionLabelForMarket(market.id, market.countryCode, markets),
        countryCode: market.countryCode,
        currency: market.currency,
        suggested: market.isLive || (market.countryCode === "CA" && locale === "en"),
      });
    }
  }

  return presets;
}

export function findLocaleRegionPreset(
  locale: string,
  marketId: string,
  catalog?: MarketConfig[],
): LocaleRegionPreset | null {
  return (
    buildLocaleRegionPresets(catalog).find(
      (preset) => preset.locale === locale && preset.marketId === marketId,
    ) ?? null
  );
}

export function formatFooterLanguageLabel(locale: string, countryCode: string): string {
  const language = getLocaleOption(locale).nativeLabel;
  return `${language} (${countryCode})`;
}

export function countryName(countryCode: string): string {
  return countryNames[countryCode] ?? countryCode;
}
