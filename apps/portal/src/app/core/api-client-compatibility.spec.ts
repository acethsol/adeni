import { AdeniApiClient, AdeniApiError, type AdeniApiClientOptions, type DiscoverySearchParams } from "@adeni/api-client";

describe("API client compatibility facade", () => {
  const payment = { id: "payment", tenantId: "tenant", type: "link", amount: 100,
    platformFeeAmount: 0, currency: "NGN", status: "pending", checkoutUrl: "/checkout", providerReference: "ref" };

  it("shares mutable authentication and tenant headers across domains", async () => {
    const requests: { url: string; headers: Headers }[] = [];
    const options: AdeniApiClientOptions = { baseUrl: "https://api.example/", devAuth0Sub: "dev", fetchImpl: async (url, init) => {
      requests.push({ url: String(url), headers: new Headers(init?.headers) });
      return Response.json({ count: 2 });
    } };
    const api = new AdeniApiClient(options);
    expect(await api.getTenantMessageUnreadCount()).toBe(2);
    expect(requests[0].headers.get("X-Dev-Auth0-Sub")).toBe("dev");
    api.setAccessToken("token"); api.setTenantId("branch-tenant");
    await api.markTenantConversationRead("a/b", "m 1");
    expect(requests[1].url).toBe("https://api.example/api/v1/tenant/messages/conversations/a%2Fb/read?messageId=m%201");
    expect(requests[1].headers.get("Authorization")).toBe("Bearer token");
    expect(requests[1].headers.has("X-Dev-Auth0-Sub")).toBeFalse();
    expect(requests[1].headers.get("X-Tenant-Id")).toBe("branch-tenant");
    api.setAccessToken(null); api.setTenantId(null); api.setDevAuth0Sub("next-dev");
    await api.getTenantMessageUnreadCount();
    expect(requests[2].headers.get("X-Dev-Auth0-Sub")).toBe("next-dev");
    expect(requests[2].headers.has("X-Tenant-Id")).toBeFalse();
  });

  it("preserves finance JSON validation, headers, idempotency and response parsing", async () => {
    const fetcher = jasmine.createSpy("fetch").and.callFake(async (_url: string, _init?: RequestInit) => Response.json(payment));
    const api = new AdeniApiClient({ baseUrl: "https://api.example", fetchImpl: fetcher });
    const request = { tenantId: "11111111-1111-4111-8111-111111111111", amount: 100, currency: "NGN", type: "link" as const };
    expect((await api.initializePayment(request, "retry-key")).id).toBe("payment");
    const [url, init] = fetcher.calls.mostRecent().args;
    expect(url).toBe("https://api.example/api/v1/finance/initialize");
    expect(init?.method).toBe("POST");
    expect(new Headers(init?.headers).get("Idempotency-Key")).toBe("retry-key");
    expect(new Headers(init?.headers).get("Content-Type")).toBe("application/json");
    expect(JSON.parse(String(init?.body)).amount).toBe(100);
    fetcher.calls.reset();
    await expectAsync(api.initializePayment({ ...request, amount: -1 })).toBeRejected();
    expect(fetcher).not.toHaveBeenCalled();
  });

  it("retains response validation and the public error class", async () => {
    const api = new AdeniApiClient({ baseUrl: "", fetchImpl: async () => Response.json({ count: "invalid" }) });
    await expectAsync(api.getTenantMessageUnreadCount()).toBeRejected();
    let locale = "en";
    const failed = new AdeniApiClient({ baseUrl: "", locale: () => locale,
      fetchImpl: async () => Response.json({ code: "permission.denied", title: "Denied" }, { status: 403 }) });
    try { await failed.getPayment("id"); fail("Expected failure"); }
    catch (error) { expect(error instanceof AdeniApiError).toBeTrue(); expect((error as AdeniApiError).statusCode).toBe(403); }
    locale = "fr";
    await expectAsync(failed.getPayment("id")).toBeRejectedWith(jasmine.any(AdeniApiError));
  });

  it("preserves discovery query and report blob downloads", async () => {
    const fetcher = jasmine.createSpy("fetch").and.callFake(async (_url: string, _init?: RequestInit) => new Response("csv", { status: 200 }));
    const api = new AdeniApiClient({ baseUrl: "https://api.example", fetchImpl: fetcher });
    const blob = await api.downloadTenantReportExport({ reportKey: "sales-summary", from: "start", to: "end", format: "csv", locationId: "a/b" });
    expect(await blob.text()).toBe("csv");
    expect(fetcher.calls.mostRecent().args[0]).toContain("/api/v1/tenant/reporting/exports/sales-summary?");
    expect(fetcher.calls.mostRecent().args[0]).toContain("locationId=a%2Fb");
    const search: DiscoverySearchParams = { lat: 1, lng: 2, q: "cut & colour", market: "lagos" };
    await api.searchDiscovery(search).catch(() => undefined);
    expect(fetcher.calls.mostRecent().args[0]).toContain("q=cut+%26+colour");
    expect(fetcher.calls.mostRecent().args[0]).toContain("market=lagos");
  });

  it("keeps convenience-method dispatch on the facade", async () => {
    const api = new AdeniApiClient({ baseUrl: "" });
    spyOn(api, "listTenantBookings").and.resolveTo({ items: [], page: 1, pageSize: 25, totalCount: 0 });
    expect(await api.getTenantBookings()).toEqual([]);
    expect(api.listTenantBookings).toHaveBeenCalled();
  });
  it("routes every domain through the same transport and error contract", async () => {
    const fetcher = jasmine.createSpy("fetch").and.callFake(async (_url: string, _init?: RequestInit) =>
      Response.json({ title: "Forbidden", code: "permission.denied" }, { status: 403 }));
    const api = new AdeniApiClient({ baseUrl: "https://api.example", accessToken: "shared-token", fetchImpl: fetcher });
    const cases: [string, () => Promise<unknown>][] = [
      ["/api/v1/categories", () => api.getCategories()],
      ["/api/v1/bookings", () => api.getMyBookings()],
      ["/api/v1/admin/metrics/subscription", () => api.getAdminSubscriptionMetrics()],
      ["/api/v1/finance/id", () => api.getPayment("id")],
      ["/api/v1/tenant/profile", () => api.getTenantProfile()],
      ["/api/v1/tenant/entitlements", () => api.getTenantEntitlements()],
      ["/api/v1/quotes", () => api.listCustomerQuotes()],
      ["/api/v1/tenant/dashboard/layout", () => api.getTenantDashboardLayout()],
      ["/api/v1/tenant/kiosk/status", () => api.getTenantKioskStatus()],
      ["/api/v1/tenant/customers", () => api.listTenantCustomers()],
      ["/api/v1/tenant/staff", () => api.listTenantStaff()],
      ["/api/v1/tenant/messages/unread-count", () => api.getTenantMessageUnreadCount()],
      ["/api/v1/tenant/services", () => api.getTenantServiceCatalog()],
    ];
    for (const [path, invoke] of cases) {
      await expectAsync(invoke()).toBeRejectedWith(jasmine.any(AdeniApiError));
      const [url, init] = fetcher.calls.mostRecent().args;
      expect(url).withContext(path).toBe(`https://api.example${path}`);
      expect(new Headers(init?.headers).get("Authorization")).toBe("Bearer shared-token");
    }
    expect(fetcher).toHaveBeenCalledTimes(cases.length);
  });

});
