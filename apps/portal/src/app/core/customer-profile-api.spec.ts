import { AdeniApiClient } from "@adeni/api-client";

describe("customer profile API facade", () => {
  const availability = { booking: "available", finance: "restricted", messaging: "restricted", preferences: "notImplemented", retention: "notImplemented" };
  const profile = { tenantCustomerId: "roster", asOf: "2026-10-25", locationId: null, availability,
    visitSummary: null, nextAppointment: null, recordedOnlinePaymentsByCurrency: null };

  it("preserves root imports, authentication, scope and AbortSignal", async () => {
    const fetcher = jasmine.createSpy("fetch").and.callFake(async () => Response.json(profile));
    const api = new AdeniApiClient({ baseUrl: "https://api.example", accessToken: "token", tenantId: "tenant", fetchImpl: fetcher });
    const abort = new AbortController();
    expect((await api.getTenantCustomerProfile("a/b", "branch a", abort.signal)).availability.finance).toBe("restricted");
    const [url, init] = fetcher.calls.mostRecent().args as unknown as [string, RequestInit];
    expect(url).toBe("https://api.example/api/v1/tenant/customers/a%2Fb/profile?locationId=branch%20a");
    expect(new Headers(init.headers).get("Authorization")).toBe("Bearer token");
    expect(new Headers(init.headers).get("X-Tenant-Id")).toBe("tenant");
    expect(init.signal).toBe(abort.signal);
  });

  it("encodes activity cursor/query and validates responses", async () => {
    const fetcher = jasmine.createSpy("fetch").and.callFake(async () => Response.json({ items: [], nextCursor: null,
      asOf: "2026-10-25", from: "2026-07-27", isTruncated: false, availability }));
    const api = new AdeniApiClient({ baseUrl: "", fetchImpl: fetcher });
    await api.getTenantCustomerActivity("id", { locationId: "b/b", pageSize: 2, cursor: "a+b/=" });
    expect(fetcher.calls.mostRecent().args[0]).toBe("/api/v1/tenant/customers/id/activity?locationId=b%2Fb&pageSize=2&cursor=a%2Bb%2F%3D");
    fetcher.and.callFake(async () => Response.json({ items: "invalid" }));
    await expectAsync(api.getTenantCustomerActivity("id")).toBeRejected();
  });
});
