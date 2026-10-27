/// <reference path="./carbon-icon.types.d.ts" />

import { getAttributes, toString, toSVG } from "@carbon/icon-helpers";
import type IconDescriptor from "@carbon/icon-helpers/es/types";
import Add16 from "@carbon/icons/es/add/16";
import ArrowLeft16 from "@carbon/icons/es/arrow--left/16";
import ArrowRight16 from "@carbon/icons/es/arrow--right/16";
import ArrowRight20 from "@carbon/icons/es/arrow--right/20";
import Calendar16 from "@carbon/icons/es/calendar/16";
import ChartLine16 from "@carbon/icons/es/chart--line/16";
import Calendar32 from "@carbon/icons/es/calendar/32";
import Certificate16 from "@carbon/icons/es/certificate/16";
import Document16 from "@carbon/icons/es/document/16";
import Location16 from "@carbon/icons/es/location/16";
import Phone16 from "@carbon/icons/es/phone/16";
import Security16 from "@carbon/icons/es/security/16";
import WarningFilled16 from "@carbon/icons/es/warning--filled/16";
import Checkmark16 from "@carbon/icons/es/checkmark/16";
import CheckmarkFilled16 from "@carbon/icons/es/checkmark--filled/16";
import ChevronDown16 from "@carbon/icons/es/chevron--down/16";
import ChevronLeft16 from "@carbon/icons/es/chevron--left/16";
import ChevronRight16 from "@carbon/icons/es/chevron--right/16";
import Close16 from "@carbon/icons/es/close/16";
import Copy16 from "@carbon/icons/es/copy/16";
import Download16 from "@carbon/icons/es/download/16";
import Grid16 from "@carbon/icons/es/grid/16";
import Table16 from "@carbon/icons/es/table/16";
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
import Restart16 from "@carbon/icons/es/restart/16";
import Save16 from "@carbon/icons/es/save/16";
import Search20 from "@carbon/icons/es/search/20";
import Send16 from "@carbon/icons/es/send/16";
import Time16 from "@carbon/icons/es/time/16";
import TrashCan16 from "@carbon/icons/es/trash-can/16";
import Upload16 from "@carbon/icons/es/upload/16";
import User16 from "@carbon/icons/es/user/16";
import UserFilled16 from "@carbon/icons/es/user--filled/16";
import UserFollow16 from "@carbon/icons/es/user--follow/16";
import UserAvatar16 from "@carbon/icons/es/user--avatar/16";
import Wallet16 from "@carbon/icons/es/wallet/16";
import Task16 from "@carbon/icons/es/task/16";
import Template16 from "@carbon/icons/es/template/16";
import Screen16 from "@carbon/icons/es/screen/16";

export type AdeniCarbonIconName =
  | "add"
  | "arrow--left"
  | "arrow--right"
  | "arrow--right-20"
  | "calendar"
  | "calendar-32"
  | "certificate"
  | "document"
  | "location"
  | "phone"
  | "security"
  | "warning--filled"
  | "chart--line"
  | "checkmark"
  | "checkmark--filled"
  | "chevron--down"
  | "chevron--left"
  | "chevron--right"
  | "close"
  | "copy"
  | "download"
  | "edit"
  | "grid"
  | "email"
  | "filter"
  | "flash--filled"
  | "image"
  | "launch"
  | "location--filled"
  | "location--filled-32"
  | "map--center"
  | "misuse"
  | "restart"
  | "save"
  | "search"
  | "send"
  | "table"
  | "time"
  | "trash-can"
  | "upload"
  | "user"
  | "user--filled"
  | "user--follow"
  | "user--avatar"
  | "task"
  | "template"
  | "screen"
  | "wallet";

const ICONS: Record<AdeniCarbonIconName, IconDescriptor> = {
  add: Add16 as IconDescriptor,
  "arrow--left": ArrowLeft16 as IconDescriptor,
  "arrow--right": ArrowRight16 as IconDescriptor,
  "arrow--right-20": ArrowRight20 as IconDescriptor,
  calendar: Calendar16 as IconDescriptor,
  "calendar-32": Calendar32 as IconDescriptor,
  certificate: Certificate16 as IconDescriptor,
  document: Document16 as IconDescriptor,
  location: Location16 as IconDescriptor,
  phone: Phone16 as IconDescriptor,
  security: Security16 as IconDescriptor,
  "warning--filled": WarningFilled16 as IconDescriptor,
  "chart--line": ChartLine16 as IconDescriptor,
  checkmark: Checkmark16 as IconDescriptor,
  "checkmark--filled": CheckmarkFilled16 as IconDescriptor,
  "chevron--down": ChevronDown16 as IconDescriptor,
  "chevron--left": ChevronLeft16 as IconDescriptor,
  "chevron--right": ChevronRight16 as IconDescriptor,
  close: Close16 as IconDescriptor,
  copy: Copy16 as IconDescriptor,
  download: Download16 as IconDescriptor,
  edit: Edit16 as IconDescriptor,
  grid: Grid16 as IconDescriptor,
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
  restart: Restart16 as IconDescriptor,
  search: Search20 as IconDescriptor,
  send: Send16 as IconDescriptor,
  table: Table16 as IconDescriptor,
  time: Time16 as IconDescriptor,
  "trash-can": TrashCan16 as IconDescriptor,
  upload: Upload16 as IconDescriptor,
  user: User16 as IconDescriptor,
  "user--filled": UserFilled16 as IconDescriptor,
  "user--follow": UserFollow16 as IconDescriptor,
  "user--avatar": UserAvatar16 as IconDescriptor,
  task: Task16 as IconDescriptor,
  template: Template16 as IconDescriptor,
  screen: Screen16 as IconDescriptor,
  wallet: Wallet16 as IconDescriptor,
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
  return ICONS[name] ?? Misuse16;
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
