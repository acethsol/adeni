import { TestBed } from "@angular/core/testing";
import { BusinessApiService } from "../../core/services/business-api.service";
import { CustomerSegmentsDataService } from "./customer-segments-data.service";
import type { CustomerSegments } from "@adeni/shared/customer-segments";
describe("CustomerSegmentsDataService", () => {
  let batch: jasmine.Spy; let service: CustomerSegmentsDataService;
  beforeEach(() => {
    batch = jasmine.createSpy().and.resolveTo([]);
    TestBed.configureTestingModule({ providers: [CustomerSegmentsDataService, { provide: BusinessApiService, useValue: { withAuthorizedClient: (fn: (c: unknown) => unknown) => fn({ insights: { batch } }) } }] });
    service = TestBed.inject(CustomerSegmentsDataService);
  });
  it("batches a visible page once and avoids empty page requests", async () => {
    await service.load(["a", "b"], "branch"); expect(batch).toHaveBeenCalledTimes(1);
    expect(batch.calls.mostRecent().args[0]).toEqual(["a", "b"]); expect(batch.calls.mostRecent().args[1]).toBe("branch");
    await service.load([]); expect(batch).toHaveBeenCalledTimes(1); expect(service.rows().size).toBe(0);
  });
  it("aborts stale page requests and prevents late responses restoring another scope", async () => {
    let resolve!: (rows: CustomerSegments[]) => void; batch.and.returnValue(new Promise<CustomerSegments[]>(r => resolve = r));
    const first = service.load(["a"], "old"); const signal = batch.calls.mostRecent().args[2] as AbortSignal;
    batch.and.resolveTo([{ tenantCustomerId: "b" }]); await service.load(["b"], "new");
    expect(signal.aborted).toBeTrue(); resolve([{ tenantCustomerId: "a" } as CustomerSegments]); await first;
    expect(service.rows().has("a")).toBeFalse(); expect(service.rows().has("b")).toBeTrue();
    service.cancel(); expect(service.rows().size).toBe(0);
  });
  it("reports failure without retaining source values or fabricating labels", async () => {
    batch.and.rejectWith(new Error("sensitive source detail")); await service.load(["a"]);
    expect(service.failed()).toBeTrue(); expect(service.rows().size).toBe(0); expect(service.loading()).toBeFalse();
  });
});
