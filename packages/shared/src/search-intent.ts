/** Rule-based search intent parser — MVP bridge until LLM agent (Sprint 11d). */
export type SearchIntent = {
  query: string | null;
  category: string | null;
  area: string | null;
  summary: string;
};

const CATEGORY_KEYWORDS: Record<string, string[]> = {
  barbers: ["barber", "barbers", "barbershop", "haircut", "fade", "line-up", "lineup", "trim", "cut"],
  "hair-salons": [
    "salon",
    "hair",
    "braids",
    "braid",
    "weave",
    "locs",
    "silk press",
    "stylist",
    "hairdresser",
  ],
  "nail-spa": ["nail", "nails", "manicure", "pedicure", "spa", "massage"],
  "makeup-brows": ["makeup", "mua", "brows", "eyebrow", "lashes", "lash"],
  plumbers: ["plumber", "plumbing", "pipe", "leak", "drain"],
  electricians: ["electrician", "electrical", "wiring", "outlet"],
  cleaning: ["clean", "cleaning", "cleaner", "housekeeping", "maid"],
};

const STOP_WORDS = new Set([
  "a",
  "an",
  "the",
  "for",
  "in",
  "at",
  "on",
  "my",
  "me",
  "near",
  "around",
  "this",
  "that",
  "with",
  "and",
  "or",
  "to",
  "of",
  "is",
  "are",
  "i",
  "need",
  "want",
  "find",
  "looking",
  "book",
  "appointment",
  "today",
  "tomorrow",
  "weekend",
  "please",
  "someone",
  "local",
  "verified",
  "service",
  "services",
]);

const AREA_HINTS = [
  "lekki",
  "ikeja",
  "victoria island",
  "vi",
  "yaba",
  "surulere",
  "ajah",
  "wuse",
  "garki",
  "maitama",
  "centretown",
  "glebe",
  "kanata",
  "annex",
  "montrose",
  "midtown",
  "uptown",
  "frisco",
];

export function parseSearchIntent(input: string): SearchIntent {
  const normalized = input.trim().toLowerCase();
  if (!normalized) {
    return { query: null, category: null, area: null, summary: "Browse all services" };
  }

  let category: string | null = null;
  for (const [slug, keywords] of Object.entries(CATEGORY_KEYWORDS)) {
    if (keywords.some((keyword) => normalized.includes(keyword))) {
      category = slug;
      break;
    }
  }

  let area: string | null = null;
  for (const hint of AREA_HINTS) {
    if (normalized.includes(hint)) {
      area = hint;
      break;
    }
  }

  const tokens = normalized
    .split(/[\s,]+/)
    .map((token) => token.replace(/[^a-z0-9-]/g, ""))
    .filter((token) => token.length > 1 && !STOP_WORDS.has(token));

  const categoryKeywords = category ? new Set(CATEGORY_KEYWORDS[category]) : null;
  const queryTokens = tokens.filter((token) => {
    if (categoryKeywords?.has(token)) {
      return false;
    }
    if (area && token === area.replace(/\s+/g, "")) {
      return false;
    }
    return true;
  });

  const query = queryTokens.length > 0 ? queryTokens.join(" ") : area ?? null;

  const parts: string[] = [];
  if (category) {
    parts.push(formatCategory(category));
  }
  if (area) {
    parts.push(`in ${area}`);
  }
  if (query && query !== area) {
    parts.push(`matching “${query}”`);
  }

  return {
    query,
    category,
    area,
    summary: parts.length > 0 ? parts.join(" ") : `Search for “${input.trim()}”`,
  };
}

function formatCategory(slug: string): string {
  return slug
    .split("-")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

export function buildDiscoverSearchParams(intent: SearchIntent): {
  category?: string;
  q?: string;
} {
  const params: { category?: string; q?: string } = {};
  if (intent.category) {
    params.category = intent.category;
  }

  const qParts = [intent.query, intent.area].filter(Boolean);
  if (qParts.length > 0) {
    params.q = qParts.join(" ");
  }

  return params;
}
