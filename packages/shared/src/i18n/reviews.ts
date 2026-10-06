import type { LocaleId } from "./types";
import { enMessages } from "./messages/en";
import { frMessages } from "./messages/fr";
import { defaultLocale, isLocaleId, translate, type Messages } from "./types";

const catalog: Record<LocaleId, Messages> = {
  en: enMessages,
  fr: frMessages,
};

export function getReviewCountLabel(
  locale: string,
  reviewCount?: number | null,
): string {
  const normalized = isLocaleId(locale) ? locale : defaultLocale;
  const messages = catalog[normalized];

  if (!reviewCount) {
    return translate(messages, "business.awaitingReviews");
  }

  if (reviewCount === 1) {
    return translate(messages, "business.oneReview");
  }

  return translate(messages, "business.reviewCount", { count: reviewCount });
}
