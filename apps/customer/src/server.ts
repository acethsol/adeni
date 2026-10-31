import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from "@angular/ssr/node";
import express from "express";
import { bookingLinkHandler } from "./booking-link-handler";
import { join } from "node:path";
import {
  getMarketById,
  listEnabledWellnessCategorySlugs,
  listSeoMarkets,
  listSeoNeighborhoods,
  seoCategoryPath,
  seoMarketPath,
  seoNeighborhoodPath,
} from "@adeni/shared";

const browserDistFolder = join(import.meta.dirname, "../browser");

const app = express();
const angularApp = new AngularNodeAppEngine();

// Baseline security headers for all customer SSR responses (see docs/ci.md path-aware smoke).
app.use((_req, res, next) => {
  res.setHeader("X-Content-Type-Options", "nosniff");
  res.setHeader("X-Frame-Options", "DENY");
  res.setHeader("Referrer-Policy", "strict-origin-when-cross-origin");
  res.setHeader("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
  if (_req.secure || _req.headers["x-forwarded-proto"] === "https") {
    res.setHeader("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
  }
  next();
});

const API_BASE = (process.env["ADENI_API_BASE_URL"] ?? "http://localhost:5169").replace(
  /\/$/,
  "",
);
const PUBLIC_ORIGIN = (
  process.env["ADENI_PUBLIC_APP_URL"] ?? "http://localhost:5190"
).replace(/\/$/, "");

app.get("/book/:slug", bookingLinkHandler(API_BASE));

type SitemapCache = { xml: string; expiresAt: number };
let sitemapCache: SitemapCache | null = null;
const SITEMAP_TTL_MS = 15 * 60 * 1000;

app.get("/sitemap.xml", async (_req, res) => {
  try {
    const now = Date.now();
    if (sitemapCache && sitemapCache.expiresAt > now) {
      res.status(200).type("application/xml").send(sitemapCache.xml);
      return;
    }

    const xml = await buildSitemapXml(PUBLIC_ORIGIN, API_BASE);
    sitemapCache = { xml, expiresAt: now + SITEMAP_TTL_MS };
    res.status(200).type("application/xml").send(xml);
  } catch (error) {
    console.error("sitemap generation failed", error);
    res.status(503).type("text/plain").send("Sitemap temporarily unavailable");
  }
});

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: "1y",
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) => (response ? writeResponseToNodeResponse(response, res) : next()))
    .catch(next);
});

if (isMainModule(import.meta.url) || process.env["pm_id"]) {
  const port = process.env["PORT"] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

export const reqHandler = createNodeRequestHandler(app);

async function buildSitemapXml(origin: string, apiBase: string): Promise<string> {
  const urls: { loc: string; changefreq: string; priority: string }[] = [
    { loc: `${origin}/`, changefreq: "daily", priority: "1.0" },
    { loc: `${origin}/discover`, changefreq: "daily", priority: "0.9" },
  ];

  const markets = listSeoMarkets();
  const categories = listEnabledWellnessCategorySlugs();

  for (const market of markets) {
    urls.push({
      loc: `${origin}${seoMarketPath(market.id)}`,
      changefreq: "daily",
      priority: "0.9",
    });
    for (const category of categories) {
      urls.push({
        loc: `${origin}${seoCategoryPath(market.id, category)}`,
        changefreq: "daily",
        priority: "0.8",
      });
    }
    for (const neighborhood of listSeoNeighborhoods(market.id)) {
      urls.push({
        loc: `${origin}${seoNeighborhoodPath(neighborhood.marketId, neighborhood.slug)}`,
        changefreq: "daily",
        priority: "0.8",
      });
    }
  }

  const slugs = await fetchVerifiedSlugs(apiBase, markets);
  for (const slug of slugs) {
    urls.push({
      loc: `${origin}/businesses/${encodeURIComponent(slug)}`,
      changefreq: "weekly",
      priority: "0.7",
    });
  }

  const body = urls
    .map(
      (u) => `  <url>
    <loc>${escapeXml(u.loc)}</loc>
    <changefreq>${u.changefreq}</changefreq>
    <priority>${u.priority}</priority>
  </url>`,
    )
    .join("\n");

  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${body}
</urlset>
`;
}

async function fetchVerifiedSlugs(
  apiBase: string,
  markets: { id: string }[],
): Promise<string[]> {
  const slugs = new Set<string>();
  for (const market of markets) {
    const config = getMarketById(market.id);
    if (!config) {
      continue;
    }
    let page = 1;
    const pageSize = 100;
    for (;;) {
      const query = new URLSearchParams({
        lat: String(config.defaultLocation.lat),
        lng: String(config.defaultLocation.lng),
        market: market.id,
        page: String(page),
        pageSize: String(pageSize),
        sort: "featured",
      });
      const response = await fetch(`${apiBase}/api/v1/discovery?${query}`);
      if (!response.ok) {
        break;
      }
      const payload = (await response.json()) as {
        items?: { slug?: string }[];
        totalCount?: number;
      };
      for (const item of payload.items ?? []) {
        if (item.slug) {
          slugs.add(item.slug);
        }
      }
      const total = payload.totalCount ?? 0;
      if (page * pageSize >= total || (payload.items?.length ?? 0) === 0) {
        break;
      }
      page += 1;
      if (page > 50) {
        break;
      }
    }
  }
  return [...slugs].sort();
}

function escapeXml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&apos;");
}
