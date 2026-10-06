/** Curated public-page templates for `/businesses/{slug}` (Sprint 21 Phase A). */

export const PUBLIC_PAGE_TEMPLATE_IDS = ["studio", "spa", "barber", "luxe"] as const;

export type PublicPageTemplateId = (typeof PUBLIC_PAGE_TEMPLATE_IDS)[number];

export const DEFAULT_PUBLIC_PAGE_TEMPLATE: PublicPageTemplateId = "studio";

export type PublicPageTemplateMeta = {
  id: PublicPageTemplateId;
  label: string;
  description: string;
};

export const PUBLIC_PAGE_TEMPLATES: PublicPageTemplateMeta[] = [
  {
    id: "studio",
    label: "Studio",
    description: "Full-bleed hero with a sticky booking rail — the Adeni default.",
  },
  {
    id: "spa",
    label: "Spa",
    description: "Calmer type and softer contrast for wellness brands.",
  },
  {
    id: "barber",
    label: "Barber",
    description: "High contrast, compact menu, bold calls to action.",
  },
  {
    id: "luxe",
    label: "Luxe",
    description: "Editorial serif hero and generous whitespace.",
  },
];

export function isPublicPageTemplateId(value: string): value is PublicPageTemplateId {
  return (PUBLIC_PAGE_TEMPLATE_IDS as readonly string[]).includes(value);
}

export function resolvePublicPageTemplateId(
  value?: string | null,
): PublicPageTemplateId {
  const normalized = value?.trim().toLowerCase() ?? "";
  return isPublicPageTemplateId(normalized) ? normalized : DEFAULT_PUBLIC_PAGE_TEMPLATE;
}
