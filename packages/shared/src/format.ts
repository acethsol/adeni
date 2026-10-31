/** Cross-platform date/currency formatting helpers. */

/** Queue walk-in / guest flows store a single display name on the API. */
export function combineGuestName(firstName: string, lastName: string): string {
  return [firstName.trim(), lastName.trim()].filter(Boolean).join(" ");
}

/** Trim; empty string → undefined for optional API fields. */
export function optionalEmail(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

/** Ledger amounts: NGN stored as whole naira; CAD/USD stored as minor (cents). */
export function formatAmountMinor(amountMinor: number, currency: string, locale?: string): string {
  const major = currency.toUpperCase() === "NGN" ? amountMinor : amountMinor / 100;
  return formatPrice(major, currency, locale);
}

export function formatPrice(amount: number, currency: string, locale?: string): string {
  try {
    return new Intl.NumberFormat(locale, {
      style: "currency",
      currency,
    }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

export function formatSlotTime(iso: string, locale?: string): string {
  try {
    return new Intl.DateTimeFormat(locale, {
      weekday: "short",
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

export function formatWhen(iso: string, locale?: string): string {
  try {
    return new Date(iso).toLocaleString(locale, {
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
    });
  } catch {
    return iso;
  }
}

export function slotRange(from: Date, days: number) {
  const start = new Date(from);
  start.setHours(0, 0, 0, 0);
  const end = new Date(start);
  end.setDate(end.getDate() + days);
  return { from: start.toISOString(), to: end.toISOString() };
}

/** Hosted checkout URLs may be relative; resolve against discover/public web origin. */
export function resolvePaymentCheckoutUrl(
  checkoutUrl: string,
  publicWebOrigin: string,
  details?: { amount?: number; currency?: string; checkoutRealtimeToken?: string },
): string {
  const origin = publicWebOrigin.replace(/\/$/, "");
  const absolute = checkoutUrl.startsWith("http")
    ? checkoutUrl
    : `${origin}${checkoutUrl.startsWith("/") ? checkoutUrl : `/${checkoutUrl}`}`;

  if (details?.amount == null || !details.currency) {
    return absolute;
  }

  try {
    const url = new URL(absolute);
    if (url.pathname.includes("/checkout/stub/")) {
      url.searchParams.set("amount", String(details.amount));
      url.searchParams.set("currency", details.currency);
      const rt = details.checkoutRealtimeToken?.trim();
      if (rt && !url.searchParams.has("rt")) {
        url.searchParams.set("rt", rt);
      }
    }
    return url.toString();
  } catch {
    return absolute;
  }
}
