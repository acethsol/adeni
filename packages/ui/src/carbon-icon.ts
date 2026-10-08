/// <reference path="./carbon-icon.types.d.ts" />

import { getAttributes, toString, toSVG } from "@carbon/icon-helpers";
import type IconDescriptor from "@carbon/icon-helpers/es/types";
import ArrowRight20 from "@carbon/icons/es/arrow--right/20";
import Checkmark16 from "@carbon/icons/es/checkmark/16";
import CheckmarkFilled16 from "@carbon/icons/es/checkmark--filled/16";
import ChevronDown16 from "@carbon/icons/es/chevron--down/16";
import FlashFilled16 from "@carbon/icons/es/flash--filled/16";
import LocationFilled20 from "@carbon/icons/es/location--filled/20";
import LocationFilled32 from "@carbon/icons/es/location--filled/32";
import Search20 from "@carbon/icons/es/search/20";

export type AdeniCarbonIconName =
  | "location--filled"
  | "location--filled-32"
  | "search"
  | "arrow--right"
  | "chevron--down"
  | "checkmark"
  | "checkmark--filled"
  | "flash--filled";

const ICONS: Record<AdeniCarbonIconName, IconDescriptor> = {
  "location--filled": LocationFilled20 as IconDescriptor,
  "location--filled-32": LocationFilled32 as IconDescriptor,
  search: Search20 as IconDescriptor,
  "arrow--right": ArrowRight20 as IconDescriptor,
  "chevron--down": ChevronDown16 as IconDescriptor,
  checkmark: Checkmark16 as IconDescriptor,
  "checkmark--filled": CheckmarkFilled16 as IconDescriptor,
  "flash--filled": FlashFilled16 as IconDescriptor,
};

function iconSize(icon: IconDescriptor, override?: number): number {
  if (override != null) {
    return override;
  }
  const width = icon.attrs?.["width"];
  const parsed = width == null ? NaN : Number(width);
  return Number.isFinite(parsed) ? parsed : 20;
}

function withSize(icon: IconDescriptor, size: number): IconDescriptor {
  return {
    ...icon,
    attrs: {
      ...stringAttrs(
        getAttributes({
          ...(icon.attrs ?? {}),
          width: size,
          height: size,
          "aria-hidden": true,
          focusable: "false",
        }),
      ),
      width: String(size),
      height: String(size),
    },
  };
}

function stringAttrs(attrs: Record<string, unknown>): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(attrs)) {
    if (value == null) {
      continue;
    }
    out[key] = String(value);
  }
  return out;
}

export function getAdeniCarbonIcon(name: AdeniCarbonIconName): IconDescriptor {
  return ICONS[name];
}

/** SVG markup string for templates (aria-hidden by default). */
export function carbonIconMarkup(
  name: AdeniCarbonIconName,
  options?: { size?: number; className?: string },
): string {
  const icon = getAdeniCarbonIcon(name);
  const svg = toString(withSize(icon, iconSize(icon, options?.size)));
  if (!options?.className) {
    return svg;
  }
  return svg.replace("<svg", `<svg class="${options.className}"`);
}

/** DOM node for Mapbox markers and imperative UI. */
export function createCarbonIconElement(
  name: AdeniCarbonIconName,
  options?: { size?: number; className?: string },
): SVGSVGElement {
  const icon = getAdeniCarbonIcon(name);
  const node = toSVG(withSize(icon, iconSize(icon, options?.size))) as SVGSVGElement;
  if (options?.className) {
    node.classList.add(...options.className.split(/\s+/).filter(Boolean));
  }
  return node;
}
