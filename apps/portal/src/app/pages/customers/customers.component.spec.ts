import { TestBed } from "@angular/core/testing";
import { signal } from "@angular/core";
import { Subject } from "rxjs";
import { AdeniFeedbackService, AdeniLocaleService } from "@adeni/ui";
import { PLAN_ENTITLEMENTS, tenantEntitlementsSchema } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
import { CustomerSegmentsDataService } from "./customer-segments-data.service";
import { CustomersComponent } from "./customers.component";
describe("CustomersComponent segmentation integration", () => {
  it("preserves server paging/count/search while requesting one segment batch for the visible page", async () => {
    const entitlements = tenantEntitlementsSchema.parse({ ...PLAN_ENTITLEMENTS.professional, multiLocation: false });
    const list = jasmine.createSpy().and.resolveTo({ items: [{ id: "first", displayName: "Ada" }], totalCount: 250 });
    const segments = { load: jasmine.createSpy().and.resolveTo(), cancel: jasmine.createSpy(), rows: signal(new Map()) };
    const client = { getTenantProfile: async () => ({ entitlements }), getTenantEntitlements: async () => entitlements, getTenantSubscriptionUsage: async () => null, listTenantCustomers: list };
    TestBed.configureTestingModule({ providers: [
      { provide: BusinessApiService, useValue: { withAuthorizedClient: (fn: (c: unknown) => unknown) => fn(client) } },
      { provide: AdeniFeedbackService, useValue: { runLoading: (fn: () => unknown) => fn(), error: jasmine.createSpy() } },
      { provide: AdeniLocaleService, useValue: { locale: signal("en") } },
      { provide: PortalActiveLocationService, useValue: { tenantApiLocationId: () => "branch", selectionChanged$: new Subject() } },
      { provide: CustomerSegmentsDataService, useValue: segments },
    ] });
    const component = TestBed.runInInjectionContext(() => new CustomersComponent()); component.page.set(3); component.searchQuery.set(" Ada ");
    await component.load(); expect(component.totalCount()).toBe(250); expect(component.page()).toBe(3); expect(component.items().length).toBe(1);
    expect(list.calls.mostRecent().args[0]).toEqual(jasmine.objectContaining({ search: "Ada", page: 3, locationId: "branch" }));
    expect(segments.load).toHaveBeenCalledOnceWith(["first"], "branch"); component.ngOnDestroy(); expect(segments.cancel).toHaveBeenCalled();
  });
});
