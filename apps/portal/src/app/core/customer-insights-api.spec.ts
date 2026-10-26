import { AdeniApiClient } from "@adeni/api-client";
const id = "90000000-0000-4000-8000-000000000001";
const row = { tenantCustomerId: id, availability: "available", asOf: "2026-10-26T12:00:00Z", definitionVersion: "v1", locationId: null, timeZoneId: "Africa/Lagos", approximateVisitDates: false, matches: [] };
describe("customer insights public API facade", () => {
  it("preserves authorization, tenant, bounded batch body and AbortSignal", async () => {
    const fetcher = jasmine.createSpy().and.callFake(async () => Response.json([row]));
    const api = new AdeniApiClient({ baseUrl: "https://api.example", tenantId: "tenant", accessToken: "token", fetchImpl: fetcher });
    const abort = new AbortController();
    expect((await api.insights.batch([id], undefined, abort.signal))[0]?.tenantCustomerId).toBe(id);
    const [url, init] = fetcher.calls.mostRecent().args as [string, RequestInit];
    expect(url).toBe("https://api.example/api/v1/tenant/customers/segments/batch"); expect(init.method).toBe("POST");
    expect(new Headers(init.headers).get("Authorization")).toBe("Bearer token"); expect(new Headers(init.headers).get("X-Tenant-Id")).toBe("tenant");
    expect(JSON.parse(init.body as string)).toEqual({ customerIds: [id] }); expect(init.signal).toBe(abort.signal); expect(init.cache).toBe("no-store");
    await expectAsync(api.insights.batch([id, id])).toBeRejected(); expect(fetcher).toHaveBeenCalledTimes(1);
  });
  it("encodes scope and document identifiers and rejects non-PDF previews", async () => {
    const fetcher = jasmine.createSpy().and.callFake(async () => new Response("%PDF-test", { headers: { "Content-Type": "application/pdf" } }));
    const api = new AdeniApiClient({ baseUrl: "", fetchImpl: fetcher }); const abort = new AbortController();
    expect((await api.insights.preview("a/b", "branch a", abort.signal)).type).toBe("application/pdf");
    expect(fetcher.calls.mostRecent().args[0]).toBe("/api/v1/tenant/reporting/documents/a%2Fb/preview?locationId=branch%20a");
    fetcher.and.callFake(async () => Response.json({ token: "must not embed" })); await expectAsync(api.insights.preview(id)).toBeRejected();
  });
  it("keeps existing final download calls compatible while allowing an authorized branch scope", async () => {
    const fetcher = jasmine.createSpy().and.callFake(async () => new Response("%PDF-test"));
    const api = new AdeniApiClient({ baseUrl: "", fetchImpl: fetcher });
    await api.downloadBillingDocumentPdf(id); expect(fetcher.calls.mostRecent().args[0]).toBe(`/api/v1/finance/documents/${id}/pdf`);
    await api.downloadBillingDocumentPdf(id, "b/b"); expect(fetcher.calls.mostRecent().args[0]).toContain("?locationId=b%2Fb");
  });
});
