import { describe, expect, it } from "vitest";
import {
  isSeoCategorySlug,
  isSeoMarketId,
  listSeoMarkets,
  localBusinessSchemaType,
  seoCategoryPath,
  seoMarketPath,
} from "./seo-paths";
import {
  getSeoNeighborhood,
  isSeoNeighborhoodSlug,
  seoNeighborhoodPath,
} from "./seo-neighborhoods";
import { listEnabledWellnessCategorySlugs } from "./wellness-taxonomy";

describe("seo paths", () => {
  it("lists V1 SEO markets including Ottawa", () => {
    const ids = listSeoMarkets().map((m) => m.id);
    expect(ids).toContain("lagos");
    expect(ids).toContain("ottawa");
  });

  it("validates market ids and category slugs", () => {
    expect(isSeoMarketId("Lagos")).toBe(true);
    expect(isSeoMarketId("toronto")).toBe(false);
    expect(isSeoCategorySlug("hair-grooming")).toBe(true);
    expect(isSeoCategorySlug("plumbers")).toBe(false);
  });

  it("reserves consumer-app path segments so SEO catch-alls never claim them", () => {
    // C1 `/app/*` is declared before market routes; this pins the canMatch contract.
    expect(isSeoMarketId("app")).toBe(false);
    expect(isSeoMarketId("me")).toBe(false);
    expect(isSeoMarketId("account")).toBe(false);
    expect(isSeoCategorySlug("app")).toBe(false);
  });

  it("builds canonical path segments", () => {
    expect(seoMarketPath("Lagos")).toBe("/lagos");
    expect(seoCategoryPath("Ottawa", "Hair-Grooming")).toBe("/ottawa/hair-grooming");
  });

  it("maps wellness categories to schema.org types", () => {
    expect(localBusinessSchemaType("hair-grooming")).toBe("HairSalon");
    expect(localBusinessSchemaType("nails")).toBe("NailSalon");
    expect(localBusinessSchemaType("unknown")).toBe("LocalBusiness");
  });

  it("exposes enabled wellness category slugs for sitemap", () => {
    const slugs = listEnabledWellnessCategorySlugs();
    expect(slugs.length).toBeGreaterThan(0);
    expect(slugs).toContain("hair-grooming");
  });

  it("resolves curated neighborhood hubs", () => {
    expect(isSeoNeighborhoodSlug("lagos", "lekki")).toBe(true);
    expect(isSeoNeighborhoodSlug("ottawa", "kanata")).toBe(true);
    expect(isSeoNeighborhoodSlug("lagos", "kanata")).toBe(false);
    expect(getSeoNeighborhood("lagos", "victoria-island")?.query).toBe("victoria island");
    expect(seoNeighborhoodPath("Lagos", "Lekki")).toBe("/lagos/lekki");
  });
});
