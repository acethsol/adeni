import { getMarketById, listMarkets } from "../markets";
import { getLocaleOption, supportedLocales, type LocaleId } from "./types";

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

/** Languages offered per country — Airbnb-style pairings. */
const languagesByCountry: Record<string, LocaleId[]> = {
  NG: ["en"],
  CA: ["en", "fr"],
  US: ["en", "es"],
};

function languageLabel(locale: LocaleId): string {
  return getLocaleOption(locale).nativeLabel;
}

function regionLabelForMarket(marketId: string, countryCode: string): string {
  const market = getMarketById(marketId);
  if (!market) {
    return countryNames[countryCode] ?? countryCode;
  }

  // City-first for Adeni; country shown in footer as (CA), (NG), etc.
  return market.name;
}

export function buildLocaleRegionPresets(): LocaleRegionPreset[] {
  const presets: LocaleRegionPreset[] = [];

  for (const market of listMarkets()) {
    const locales = languagesByCountry[market.countryCode] ?? ["en"];

    for (const locale of locales) {
      presets.push({
        id: `${locale}-${market.id}`,
        locale,
        marketId: market.id,
        languageLabel: languageLabel(locale),
        regionLabel: regionLabelForMarket(market.id, market.countryCode),
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
): LocaleRegionPreset | null {
  return (
    buildLocaleRegionPresets().find(
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
