import brandTokens from "./brand-tokens.json";

/**
 * Corrected Option 3 raster pack — PNG URLs under `/brand/*` (synced from `packages/brand/assets`).
 * See `packages/brand/CURSOR_BRAND_INSTRUCTIONS.md` and `reference/`. Do not redraw in app code.
 */
export const ADENI_BRAND_NAME = "Adeni";

export const ADENI_BRAND_TAGLINE = brandTokens.tagline;

export const adeniBrandColors = brandTokens.brand;

const brandBase = "/brand";

export const adeniBrandMarkPath = `${brandBase}/adeni-mark.png`;
export const adeniBrandMarkDarkPath = `${brandBase}/adeni-mark-dark.png`;

export const adeniBrandLogoPath = `${brandBase}/adeni-logo.png`;
export const adeniBrandLogoDarkPath = `${brandBase}/adeni-logo-dark.png`;

export const adeniBrandLogoStackedPath = `${brandBase}/adeni-logo-stacked.png`;
export const adeniBrandLogoTaglinePath = `${brandBase}/adeni-logo-tagline.png`;

export const adeniBrandWordmarkPath = `${brandBase}/adeni-wordmark.png`;
export const adeniBrandWordmarkDarkPath = `${brandBase}/adeni-wordmark-dark.png`;

export const adeniBrandMarkAspect = 199 / 147;
export const adeniBrandHorizontalAspect = 502 / 150;
export const adeniBrandHorizontalDarkAspect = 454 / 146;
export const adeniBrandStackedAspect = 355 / 197;
export const adeniBrandHorizontalTaglineAspect = 485 / 166;
export const adeniBrandWordmarkAspect = 285 / 106;

export type AdeniBrandLogoVariant =
  | "mark"
  | "horizontal"
  | "horizontal-tagline"
  | "stacked"
  | "wordmark"
  | "logo"
  | "logo-tagline";

export type AdeniBrandSurface = "light" | "dark";

function normalizeVariant(variant: AdeniBrandLogoVariant): AdeniBrandLogoVariant {
  if (variant === "logo") {
    return "horizontal";
  }
  if (variant === "logo-tagline") {
    return "horizontal-tagline";
  }
  return variant;
}

/** Resolve shipped PNG path — no transforms in app code. */
export function resolveAdeniBrandImagePath(
  variant: AdeniBrandLogoVariant = "mark",
  surface: AdeniBrandSurface = "light",
): string {
  const v = normalizeVariant(variant);
  if (v === "mark") {
    return surface === "dark" ? adeniBrandMarkDarkPath : adeniBrandMarkPath;
  }
  if (v === "wordmark") {
    return surface === "dark" ? adeniBrandWordmarkDarkPath : adeniBrandWordmarkPath;
  }
  if (v === "stacked") {
    return adeniBrandLogoStackedPath;
  }
  if (v === "horizontal-tagline") {
    return adeniBrandLogoTaglinePath;
  }
  return surface === "dark" ? adeniBrandLogoDarkPath : adeniBrandLogoPath;
}

export function adeniBrandAspectForVariant(
  variant: AdeniBrandLogoVariant,
  surface: AdeniBrandSurface = "light",
): number {
  const v = normalizeVariant(variant);
  switch (v) {
    case "mark":
      return adeniBrandMarkAspect;
    case "wordmark":
      return adeniBrandWordmarkAspect;
    case "stacked":
      return adeniBrandStackedAspect;
    case "horizontal-tagline":
      return adeniBrandHorizontalTaglineAspect;
    default:
      return surface === "dark" ? adeniBrandHorizontalDarkAspect : adeniBrandHorizontalAspect;
  }
}

export const adeniBrandFaviconPath = "/favicon.ico";
