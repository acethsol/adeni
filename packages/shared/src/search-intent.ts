/** Rule-based search intent parser — MVP bridge until LLM agent (Sprint 20). */
export type SearchIntent = {
  query: string | null;
  category: string | null;
  area: string | null;
  /** Market implied by a known neighborhood / area phrase. */
  marketId: string | null;
  summary: string;
};

const CATEGORY_KEYWORDS: Record<string, string[]> = {
  barbers: [
    "barber",
    "barbers",
    "barbershop",
    "haircut",
    "fade",
    "line-up",
    "lineup",
    "trim",
    "cut",
    "cuts",
  ],
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

/** Area phrase → Adeni market id (V1: Lagos + Ottawa only). */
const AREA_TO_MARKET: Record<string, string> = {
  lekki: "lagos",
  ikeja: "lagos",
  "victoria island": "lagos",
  vi: "lagos",
  yaba: "lagos",
  surulere: "lagos",
  ajah: "lagos",
  centretown: "ottawa",
  glebe: "ottawa",
  kanata: "ottawa",
};

const AREA_HINTS = Object.keys(AREA_TO_MARKET);

export function parseSearchIntent(input: string): SearchIntent {
  const normalized = input.trim().toLowerCase();
  if (!normalized) {
    return {
      query: null,
      category: null,
      area: null,
      marketId: null,
      summary: "Browse all services",
    };
  }

  let category: string | null = null;
  for (const [slug, keywords] of Object.entries(CATEGORY_KEYWORDS)) {
    if (keywords.some((keyword) => normalized.includes(keyword))) {
      category = slug;
      break;
    }
  }

  let area: string | null = null;
  // Longer phrases first so "victoria island" wins over "vi".
  for (const hint of [...AREA_HINTS].sort((a, b) => b.length - a.length)) {
    if (normalized.includes(hint)) {
      area = hint;
      break;
    }
  }

  const marketId = area ? (AREA_TO_MARKET[area] ?? null) : null;

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
    marketId,
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
  market?: string;
} {
  const params: { category?: string; q?: string; market?: string } = {};
  if (intent.category) {
    params.category = intent.category;
  }

  // Prefer area for q when present — joining "cuts"+"lekki" into one phrase
  // fails LIKE '%cuts lekki%' against names like "Lekki Cuts".
  if (intent.area) {
    params.q = intent.area;
  } else if (intent.query) {
    params.q = intent.query;
  }

  if (intent.marketId) {
    params.market = intent.marketId;
  }

  return params;
}

const CONVERSATIONAL_PATTERN =
  /\b(near me|i need|i want|looking for|find me|someone|appointment|book a|help me)\b/i;

/** True when input looks like natural language rather than a short keyword. */
export function shouldParseAsIntent(input: string): boolean {
  const trimmed = input.trim();
  if (!trimmed) {
    return false;
  }

  if (trimmed.split(/\s+/).length >= 4) {
    return true;
  }

  if (CONVERSATIONAL_PATTERN.test(trimmed)) {
    return true;
  }

  const normalized = trimmed.toLowerCase();
  for (const keywords of Object.values(CATEGORY_KEYWORDS)) {
    if (keywords.some((keyword) => normalized.includes(keyword))) {
      return true;
    }
  }

  for (const hint of AREA_HINTS) {
    if (normalized.includes(hint)) {
      return true;
    }
  }

  return false;
}

export type DiscoverySearchParams = {
  category?: string;
  q?: string;
  market?: string;
  summary?: string;
};

/** Route a single search box to keyword or intent-based discover params. */
export function resolveDiscoverySearch(input: string): DiscoverySearchParams {
  const trimmed = input.trim();
  if (!trimmed) {
    return {};
  }

  if (shouldParseAsIntent(trimmed)) {
    const intent = parseSearchIntent(trimmed);
    return {
      ...buildDiscoverSearchParams(intent),
      summary: intent.summary,
    };
  }

  return {
    q: trimmed,
    summary: `Search for “${trimmed}”`,
  };
}

export function discoverSearchToPath(params: DiscoverySearchParams): string {
  const search = new URLSearchParams();
  if (params.category) {
    search.set("category", params.category);
  }
  if (params.q) {
    search.set("q", params.q);
  }
  if (params.market) {
    search.set("market", params.market);
  }

  const query = search.toString();
  return query ? `/discover?${query}` : "/discover";
}
