/** Cross-platform Adeni design tokens — single source of truth for web and mobile. */
export const adeniColors = {
  background: "#ffffff",
  surface: "#ffffff",
  subtle: "#f7faf8",
  text: "#1b4332",
  textMuted: "rgba(27, 67, 50, 0.7)",
  textSubtle: "rgba(27, 67, 50, 0.55)",
  accent: "#40916c",
  primary: "#1b4332",
  primaryForeground: "#ffffff",
  border: "rgba(27, 67, 50, 0.1)",
  borderStrong: "rgba(27, 67, 50, 0.2)",
  destructive: "#991b1b",
  destructiveBg: "#fef2f2",
  success: "#40916c",
} as const;

export const adeniSpacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  "2xl": 24,
  "3xl": 32,
  "4xl": 40,
} as const;

export const adeniRadius = {
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  full: 9999,
} as const;

export const adeniTypography = {
  eyebrow: {
    fontSize: 12,
    fontWeight: "700" as const,
    letterSpacing: 1.2,
    textTransform: "uppercase" as const,
  },
  titleLg: {
    fontSize: 28,
    fontWeight: "700" as const,
    lineHeight: 34,
  },
  titleMd: {
    fontSize: 22,
    fontWeight: "700" as const,
    lineHeight: 28,
  },
  titleSm: {
    fontSize: 18,
    fontWeight: "600" as const,
    lineHeight: 24,
  },
  body: {
    fontSize: 15,
    lineHeight: 22,
  },
  bodySm: {
    fontSize: 14,
    lineHeight: 20,
  },
  caption: {
    fontSize: 13,
    lineHeight: 18,
  },
} as const;

export const adeniShadows = {
  sm: {
    shadowColor: "#1b4332",
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.06,
    shadowRadius: 2,
    elevation: 1,
  },
  md: {
    shadowColor: "#1b4332",
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.08,
    shadowRadius: 12,
    elevation: 3,
  },
} as const;

/** Client cache keys — aligned with API Redis TTLs in CacheTtl. */
export const queryKeys = {
  categories: ["categories"] as const,
  discovery: (params: {
    lat: number;
    lng: number;
    market: string;
    category?: string | null;
    q?: string | null;
    pageSize?: number;
    sort?: "distance" | "featured";
  }) => ["discovery", params] as const,
  businessProfile: (slug: string) => ["business-profile", slug] as const,
  myBookings: ["bookings", "mine"] as const,
  tenantBookings: (tenantId?: string | null) => ["bookings", "tenant", tenantId ?? "unknown"] as const,
  tenantProfile: (tenantId?: string | null) => ["tenant-profile", tenantId ?? "unknown"] as const,
} as const;

/** Stale times in ms — mirror backend CacheTtl where applicable. */
export const staleTimes = {
  categories: 60 * 60 * 1000,
  discovery: 2 * 60 * 1000,
  businessProfile: 5 * 60 * 1000,
  bookings: 30 * 1000,
} as const;

export const DISCOVERY_PAGE_SIZE = 20;
export const FEATURED_PAGE_SIZE = 12;
