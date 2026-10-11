/// <reference path="./carbon-icon.types.d.ts" />

import { getAttributes, toString, toSVG } from "@carbon/icon-helpers";
import type IconDescriptor from "@carbon/icon-helpers/es/types";
import Add16 from "@carbon/icons/es/add/16";
import ArrowLeft16 from "@carbon/icons/es/arrow--left/16";
import ArrowRight16 from "@carbon/icons/es/arrow--right/16";
import ArrowRight20 from "@carbon/icons/es/arrow--right/20";
import Calendar16 from "@carbon/icons/es/calendar/16";
import Calendar32 from "@carbon/icons/es/calendar/32";
import Checkmark16 from "@carbon/icons/es/checkmark/16";
import CheckmarkFilled16 from "@carbon/icons/es/checkmark--filled/16";
import ChevronDown16 from "@carbon/icons/es/chevron--down/16";
import ChevronLeft16 from "@carbon/icons/es/chevron--left/16";
import ChevronRight16 from "@carbon/icons/es/chevron--right/16";
import Close16 from "@carbon/icons/es/close/16";
import Copy16 from "@carbon/icons/es/copy/16";
import Edit16 from "@carbon/icons/es/edit/16";
import Email16 from "@carbon/icons/es/email/16";
import Filter32 from "@carbon/icons/es/filter/32";
import FlashFilled16 from "@carbon/icons/es/flash--filled/16";
import Image16 from "@carbon/icons/es/image/16";
import Launch16 from "@carbon/icons/es/launch/16";
import LocationFilled20 from "@carbon/icons/es/location--filled/20";
import LocationFilled32 from "@carbon/icons/es/location--filled/32";
import MapCenter32 from "@carbon/icons/es/map--center/32";
import Misuse16 from "@carbon/icons/es/misuse/16";
import Save16 from "@carbon/icons/es/save/16";
import Search20 from "@carbon/icons/es/search/20";
import Send16 from "@carbon/icons/es/send/16";
import Time16 from "@carbon/icons/es/time/16";
import TrashCan16 from "@carbon/icons/es/trash-can/16";
import Upload16 from "@carbon/icons/es/upload/16";
import User16 from "@carbon/icons/es/user/16";
import UserFilled16 from "@carbon/icons/es/user--filled/16";
import UserFollow16 from "@carbon/icons/es/user--follow/16";

export type AdeniCarbonIconName =
  | "add"
  | "arrow--left"
  | "arrow--right"
  | "arrow--right-20"
  | "calendar"
  | "calendar-32"
  | "checkmark"
  | "checkmark--filled"
  | "chevron--down"
  | "chevron--left"
  | "chevron--right"
  | "close"
  | "copy"
  | "edit"
  | "email"
  | "filter"
  | "flash--filled"
  | "image"
  | "launch"
  | "location--filled"
  | "location--filled-32"
  | "map--center"
  | "misuse"
  | "save"
  | "search"
  | "send"
  | "time"
  | "trash-can"
  | "upload"
  | "user"
  | "user--filled"
  | "user--follow";

const ICONS: Record<AdeniCarbonIconName, IconDescriptor> = {
  add: Add16 as IconDescriptor,
  "arrow--left": ArrowLeft16 as IconDescriptor,
  "arrow--right": ArrowRight16 as IconDescriptor,
  "arrow--right-20": ArrowRight20 as IconDescriptor,
  calendar: Calendar16 as IconDescriptor,
  "calendar-32": Calendar32 as IconDescriptor,
  checkmark: Checkmark16 as IconDescriptor,
  "checkmark--filled": CheckmarkFilled16 as IconDescriptor,
  "chevron--down": ChevronDown16 as IconDescriptor,
  "chevron--left": ChevronLeft16 as IconDescriptor,
  "chevron--right": ChevronRight16 as IconDescriptor,
  close: Close16 as IconDescriptor,
  copy: Copy16 as IconDescriptor,
  edit: Edit16 as IconDescriptor,
  email: Email16 as IconDescriptor,
  filter: Filter32 as IconDescriptor,
  "flash--filled": FlashFilled16 as IconDescriptor,
  image: Image16 as IconDescriptor,
  launch: Launch16 as IconDescriptor,
  "location--filled": LocationFilled20 as IconDescriptor,
  "location--filled-32": LocationFilled32 as IconDescriptor,
  "map--center": MapCenter32 as IconDescriptor,
  misuse: Misuse16 as IconDescriptor,
  save: Save16 as IconDescriptor,
  search: Search20 as IconDescriptor,
  send: Send16 as IconDescriptor,
  time: Time16 as IconDescriptor,
  "trash-can": TrashCan16 as IconDescriptor,
  upload: Upload16 as IconDescriptor,
  user: User16 as IconDescriptor,
  "user--filled": UserFilled16 as IconDescriptor,
  "user--follow": UserFollow16 as IconDescriptor,
};

function iconSize(icon: IconDescriptor, override?: number): number {
  if (override != null) {
    return override;
  }
  const width = icon.attrs?.["width"];
  const parsed = width == null ? NaN : Number(width);
  return Number.isFinite(parsed) ? parsed : 16;
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
