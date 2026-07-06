import {
  adeniColors,
  adeniRadius,
  adeniShadows,
  adeniSpacing,
  adeniTypography,
} from "@adeni/shared";

/** Mobile theme — derived from shared design tokens. */
export const adeniTheme = {
  ...adeniColors,
  spacing: adeniSpacing,
  radius: adeniRadius,
  typography: adeniTypography,
  shadows: adeniShadows,
} as const;

export type AdeniTheme = typeof adeniTheme;
