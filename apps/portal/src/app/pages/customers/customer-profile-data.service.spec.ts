import { TestBed } from "@angular/core/testing";
import type { AdeniApiClient } from "@adeni/api-client";
import { customerActivitySchema, customerProfileSchema, type CustomerActivity } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";
import { CustomerProfileDataService } from "./customer-profile-data.service";

const availability = { booking: "available", finance: "restricted", messaging: "restricted", preferences: "notImplemented", retention: "notImplemented" };
const profile = customerProfileSchema.parse({ tenantCustomerId: "roster", asOf: "2026-10-25", locationId: null,
  availability, visitSummary: { completedBookings: 2, completedWalkIns: 1 }, nextAppointment: null, recordedOnlinePaymentsByCurrency: null });
const activity = customerActivitySchema.parse({ items: [], nextCursor: "next", asOf: "2026-10-25", from: "2026-07-27", isTruncated: false, availability });

describe("CustomerProfileDataService", () => {
  let client: jasmine.SpyObj<AdeniApiClient>;
  let service: CustomerProfileDataService;
  beforeEach(() => {
    client = jasmine.createSpyObj<AdeniApiClient>("client", ["getTenantCustomerProfile", "getTenantCustomerActivity"]);
    client.getTenantCustomerProfile.and.resolveTo(profile);
    client.getTenantCustomerActivity.and.resolveTo(activity);
    TestBed.configureTestingModule({ providers: [CustomerProfileDataService,
      { provide: BusinessApiService, useValue: { withAuthorizedClient: (fn: (api: AdeniApiClient) => unknown) => fn(client) } }] });
    service = TestBed.inject(CustomerProfileDataService);
  });

  it("loads one profile and activity request for the selected scope", async () => {
    await service.load("roster", "branch");
    expect(client.getTenantCustomerProfile).toHaveBeenCalledOnceWith("roster", "branch", jasmine.any(AbortSignal));
    expect(client.getTenantCustomerActivity).toHaveBeenCalledOnceWith("roster", { locationId: "branch" }, jasmine.any(AbortSignal));
    expect(service.profile()?.visitSummary?.completedBookings).toBe(2);
    expect(service.loading()).toBeFalse();
    expect(service.error()).toBeNull();
  });

  it("aborts obsolete scope requests and ignores responses that arrive late", async () => {
    let finish!: (value: CustomerActivity) => void;
    client.getTenantCustomerActivity.and.returnValue(new Promise(resolve => { finish = resolve; }));
    const old = service.load("old", "old-branch");
    const signal = client.getTenantCustomerActivity.calls.mostRecent().args[2]!;
    client.getTenantCustomerActivity.and.resolveTo(activity);
    await service.load("new", "new-branch");
    expect(signal.aborted).toBeTrue();
    finish({ ...activity, nextCursor: "obsolete" });
    await old;
    expect(service.activity()?.nextCursor).toBe("next");
  });

  it("paginates within scope and guards duplicate load-more requests", async () => {
    await service.load("roster", "branch");
    client.getTenantCustomerActivity.and.resolveTo({ ...activity, nextCursor: null, isTruncated: true });
    await Promise.all([service.loadMore(), service.loadMore()]);
    expect(client.getTenantCustomerActivity.calls.count()).toBe(2);
    expect(client.getTenantCustomerActivity.calls.mostRecent().args[1]).toEqual({ locationId: "branch", cursor: "next" });
    expect(service.activity()?.isTruncated).toBeTrue();
    await service.loadMore();
    expect(client.getTenantCustomerActivity.calls.count()).toBe(2);
  });

  it("reports source errors instead of displaying fabricated empty totals", async () => {
    client.getTenantCustomerProfile.and.rejectWith(new Error("denied"));
    await service.load("roster");
    expect(service.error()).toEqual(jasmine.any(Error));
    expect(service.profile()).toBeNull();
    expect(service.activity()).toBeNull();
    expect(service.loading()).toBeFalse();
  });

  it("aborts outstanding requests on destruction without adding realtime subscriptions", async () => {
    await service.load("roster");
    const signal = client.getTenantCustomerProfile.calls.mostRecent().args[2]!;
    service.ngOnDestroy();
    expect(signal.aborted).toBeTrue();
    await service.loadMore();
    expect(client.getTenantCustomerActivity.calls.count()).toBe(1);
  });
});
