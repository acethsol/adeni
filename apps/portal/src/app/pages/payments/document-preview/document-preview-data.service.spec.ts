import { TestBed } from "@angular/core/testing";
import { BusinessApiService } from "../../../core/services/business-api.service";
import { DocumentPreviewDataService } from "./document-preview-data.service";
describe("DocumentPreviewDataService", () => {
  let preview: jasmine.Spy; let service: DocumentPreviewDataService;
  beforeEach(() => {
    preview = jasmine.createSpy().and.resolveTo(new Blob(["%PDF-test"], { type: "application/pdf" }));
    spyOn(URL, "createObjectURL").and.returnValue("blob:private-pdf"); spyOn(URL, "revokeObjectURL");
    TestBed.configureTestingModule({ providers: [DocumentPreviewDataService, { provide: BusinessApiService, useValue: { withAuthorizedClient: (fn: (c: unknown) => unknown) => fn({ insights: { preview } }) } }] });
    service = TestBed.inject(DocumentPreviewDataService);
  });
  it("uses a private PDF Blob and revokes it when closed or replaced", async () => {
    await service.load("doc", "branch"); expect(service.url()).toBe("blob:private-pdf");
    const signal = preview.calls.mostRecent().args[2] as AbortSignal; service.clear();
    expect(signal.aborted).toBeTrue(); expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:private-pdf"); expect(service.url()).toBeNull();
  });
  it("does not create URLs for a stale document response", async () => {
    let resolve!: (blob: Blob) => void; preview.and.returnValue(new Promise<Blob>(r => resolve = r));
    const request = service.load("doc"); service.clear(); resolve(new Blob(["%PDF-test"], { type: "application/pdf" })); await request;
    expect(URL.createObjectURL).not.toHaveBeenCalled(); expect(service.url()).toBeNull();
  });
  it("rejects non-PDF and oversized content with a generic failure state", async () => {
    for (const blob of [new Blob(["private html"], { type: "text/html" }), new Blob([new Uint8Array(5 * 1024 * 1024 + 1)], { type: "application/pdf" })]) {
      preview.and.resolveTo(blob); await service.load("doc"); expect(service.failed()).toBeTrue(); expect(service.url()).toBeNull();
    }
    expect(URL.createObjectURL).not.toHaveBeenCalled();
  });
});
