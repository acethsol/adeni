import { TestBed } from "@angular/core/testing";
import type { AdeniApiClient } from "@adeni/api-client";
import { businessProfileSchema, PLAN_ENTITLEMENTS, tenantEntitlementsSchema } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
import { PORTAL_ALL_LOCATIONS_SCOPE } from "../../core/services/portal-active-location-store";
import { DashboardDataService } from "./dashboard-data.service";

describe("DashboardDataService", () => {
  let client: jasmine.SpyObj<AdeniApiClient>;
  let location: { syncFromProfile: jasmine.Spy; tenantApiLocationId: jasmine.Spy; selectedLocationId: jasmine.Spy; resolveContextLocation: jasmine.Spy };
  const entitlements = tenantEntitlementsSchema.parse({ ...PLAN_ENTITLEMENTS.business, multiLocation: true });
  const profile = businessProfileSchema.parse({ tenantId: "tenant", businessName: "Shop", categorySlug: "barbers",
    phone: "", description: "", status: 2, createdAt: "2026-01-01", locations: [], verificationDocuments: [], entitlements });

  beforeEach(() => {
    localStorage.clear();
    client = jasmine.createSpyObj<AdeniApiClient>("client", ["getTenantProfile", "getTenantEntitlements", "getTenantDashboardLayout", "putTenantDashboardLayout",
      "listTenantBookings", "getTenantServices", "getTenantSubscriptionUsage", "getTenantDashboardSummary", "getTenantDashboardSeries",
      "getTenantDashboardBreakdown", "getTenantMessageUnreadCount", "listTenantConversations", "getTenantQueueFloor", "listTenantQuotes", "getBusinessProfile"]);
    client.getTenantProfile.and.resolveTo(profile);
    client.getTenantEntitlements.and.resolveTo(entitlements);
    client.getTenantDashboardLayout.and.resolveTo({ version: 1, range: "7d", widgets: [{ id: "messages_unread", w: 3 }, { id: "collected_revenue", w: 12 }] });
    client.listTenantBookings.and.resolveTo({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    client.getTenantServices.and.resolveTo([]);
    client.getTenantSubscriptionUsage.and.rejectWith(new Error("optional"));
    client.getTenantDashboardSummary.and.resolveTo({ from: "2026-01-01", to: "2026-01-07", pendingCount: 2, appointmentsCount: 3, analyticsIncluded: false });
    client.getTenantDashboardSeries.and.resolveTo({ metric: "appointments", from: "2026-01-01", to: "2026-01-07", points: [] });
    client.getTenantMessageUnreadCount.and.resolveTo(4);
    client.listTenantConversations.and.resolveTo({ items: [], totalCount: 0, page: 1, pageSize: 4 });
    location = { syncFromProfile: jasmine.createSpy(), tenantApiLocationId: jasmine.createSpy().and.returnValue("branch"),
      selectedLocationId: jasmine.createSpy().and.returnValue(PORTAL_ALL_LOCATIONS_SCOPE), resolveContextLocation: jasmine.createSpy() };
    TestBed.configureTestingModule({ providers: [DashboardDataService,
      { provide: BusinessApiService, useValue: { withAuthorizedClient: (fn: (api: AdeniApiClient) => unknown) => fn(client) } },
      { provide: PortalActiveLocationService, useValue: location }] });
  });

  it("preserves branch-scoped request orchestration and optional failure fallbacks", async () => {
    const snapshot = await TestBed.inject(DashboardDataService).load("7d", () => false);
    expect(location.syncFromProfile).toHaveBeenCalledWith(profile);
    expect(client.listTenantBookings).toHaveBeenCalledWith({ tab: "pending", page: 1, pageSize: 100, locationId: "branch" });
    expect(client.getTenantMessageUnreadCount).toHaveBeenCalledOnceWith("branch");
    expect(snapshot.unreadCount).toBe(4);
    expect(snapshot.usage).toBeNull();
    expect(snapshot.rating).toBeNull();
    expect(client.getBusinessProfile).not.toHaveBeenCalled();
    expect(client.getTenantQueueFloor).not.toHaveBeenCalled();
  });

  it("does not turn purchased entitlements into workflow capabilities", async () => {
    await TestBed.inject(DashboardDataService).load("7d", () => false);
    expect(client.listTenantQuotes).not.toHaveBeenCalled();
    expect(client.getTenantDashboardSeries.calls.allArgs().map(args => args[0])).toEqual(["appointments", "collected"]);
  });

  it("propagates required request failures to the page", async () => {
    client.getTenantDashboardSummary.and.rejectWith(new Error("summary failed"));
    await expectAsync(TestBed.inject(DashboardDataService).load("7d", () => false)).toBeRejectedWithError("summary failed");
  });
});
