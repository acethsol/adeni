/** Category imagery and icons — placeholders until businesses upload cover photos. */
export type CategoryVisual = {
  slug: string;
  label: string;
  icon: string;
  /** Curated Unsplash image (category representative). */
  imageUrl: string;
  gradient: [string, string];
};

const DEFAULT_VISUAL: Omit<CategoryVisual, "slug" | "label"> = {
  icon: "✦",
  imageUrl: "https://images.unsplash.com/photo-1560066984-138dadb4c035?w=800&auto=format&fit=crop&q=80",
  gradient: ["#1b4332", "#40916c"],
};

export const categoryVisuals: Record<string, CategoryVisual> = {
  barbers: {
    slug: "barbers",
    label: "Barbers",
    icon: "✂️",
    imageUrl:
      "https://images.unsplash.com/photo-1621605815971-fbc98d665033?w=800&auto=format&fit=crop&q=80",
    gradient: ["#1b4332", "#2d6a4f"],
  },
  "hair-salons": {
    slug: "hair-salons",
    label: "Hair Salons",
    icon: "💇",
    imageUrl:
      "https://images.unsplash.com/photo-1560066984-138dadb4c035?w=800&auto=format&fit=crop&q=80",
    gradient: ["#40916c", "#52b788"],
  },
  "nail-spa": {
    slug: "nail-spa",
    label: "Nail & Spa",
    icon: "💅",
    imageUrl:
      "https://images.unsplash.com/photo-1604654894610-df63bc536371?w=800&auto=format&fit=crop&q=80",
    gradient: ["#b5179e", "#7209b7"],
  },
  "makeup-brows": {
    slug: "makeup-brows",
    label: "Makeup & Brows",
    icon: "💄",
    imageUrl:
      "https://images.unsplash.com/photo-1512496015851-a90fb38ba796?w=800&auto=format&fit=crop&q=80",
    gradient: ["#9d4edd", "#c77dff"],
  },
  plumbers: {
    slug: "plumbers",
    label: "Plumbers",
    icon: "🔧",
    imageUrl:
      "https://images.unsplash.com/photo-1621905251189-08b45d6a269e?w=800&auto=format&fit=crop&q=80",
    gradient: ["#023e8a", "#0077b6"],
  },
  electricians: {
    slug: "electricians",
    label: "Electricians",
    icon: "⚡",
    imageUrl:
      "https://images.unsplash.com/photo-1621905251189-08b45d6a269e?w=800&auto=format&fit=crop&q=80",
    gradient: ["#e85d04", "#faa307"],
  },
  cleaning: {
    slug: "cleaning",
    label: "Cleaning",
    icon: "🧹",
    imageUrl:
      "https://images.unsplash.com/photo-1581578731548-c64695cc6952?w=800&auto=format&fit=crop&q=80",
    gradient: ["#0077b6", "#48cae4"],
  },
  beauty: {
    slug: "beauty",
    label: "Beauty",
    icon: "✨",
    imageUrl:
      "https://images.unsplash.com/photo-1522337360788-8b13dee7a37e?w=800&auto=format&fit=crop&q=80",
    gradient: ["#40916c", "#74c69d"],
  },
  "home-services": {
    slug: "home-services",
    label: "Home Services",
    icon: "🏠",
    imageUrl:
      "https://images.unsplash.com/photo-1581578731548-c64695cc6952?w=800&auto=format&fit=crop&q=80",
    gradient: ["#1b4332", "#40916c"],
  },
};

export function getCategoryVisual(slug: string, fallbackLabel?: string): CategoryVisual {
  const normalized = slug.trim().toLowerCase();
  const known = categoryVisuals[normalized];
  if (known) {
    return known;
  }

  return {
    slug: normalized,
    label: fallbackLabel ?? formatCategoryLabel(normalized),
    ...DEFAULT_VISUAL,
  };
}

export function getBusinessCoverImage(categorySlug: string): string {
  return getCategoryVisual(categorySlug).imageUrl;
}

export function getDefaultBusinessCoverImage(): string {
  return DEFAULT_VISUAL.imageUrl;
}

/** Prefer tenant-uploaded cover; fall back to category placeholder. */
export function resolveBusinessCoverImage(
  categorySlug: string,
  coverImageUrl?: string | null,
): string {
  const uploaded = coverImageUrl?.trim();
  if (uploaded) {
    return uploaded;
  }

  return getBusinessCoverImage(categorySlug);
}

export function formatCategoryLabel(slug: string): string {
  return slug
    .split("-")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

export const ASK_ADENI_PROMPTS = [
  "Barber near me",
  "Hair salon for braids",
  "Nails this weekend",
  "Plumber in my area",
] as const;
