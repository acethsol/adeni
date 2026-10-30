import { describe, expect, it } from "vitest";
import {
  listEnabledWellnessCategorySlugs,
  listSeoMarkets,
  listSeoNeighborhoods,
  seoCategoryPath,
  seoMarketPath,
  seoNeighborhoodPath,
} from "@adeni/shared";

/**
 * Mirrors the static URL seed in `server.ts` `buildSitemapXml`.
 * Authenticated `/app/*` must never appear in the public sitemap.
 */
function listPublicSitemapPaths(): string[] {
  const paths = ["/", "/discover"];
  for (const market of listSeoMarkets()) {
    paths.push(seoMarketPath(market.id));
    for (const category of listEnabledWellnessCategorySlugs()) {
      paths.push(seoCategoryPath(market.id, category));
    }
    for (const neighborhood of listSeoNeighborhoods(market.id)) {
      paths.push(seoNeighborhoodPath(neighborhood.marketId, neighborhood.slug));
    }
  }
  return paths;
}

describe("public sitemap contract", () => {
  it("never emits authenticated /app paths or reserved consumer segments", () => {
    const paths = listPublicSitemapPaths();
    expect(paths.length).toBeGreaterThan(4);
    for (const path of paths) {
      expect(path.startsWith("/app")).toBe(false);
      expect(path.split("/").includes("app")).toBe(false);
    }
  });

  it("includes V1 market and neighborhood hubs", () => {
    const paths = listPublicSitemapPaths();
    expect(paths).toContain("/lagos");
    expect(paths).toContain("/ottawa");
    expect(paths).toContain("/lagos/lekki");
    expect(paths).toContain("/ottawa/kanata");
  });
});
