import { signal } from "@angular/core";
import { AdeniLocaleService } from "@adeni/ui";
import { TestBed } from "@angular/core/testing";
import type { BillingDocumentResponse } from "@adeni/shared";
import { BusinessApiService } from "../../../core/services/business-api.service";
import { DocumentPreviewComponent } from "./document-preview.component";
const doc = { id: "90000000-0000-4000-8000-000000000001", documentNumber: "DRAFT-1", kind: "invoice", status: "draft" } as BillingDocumentResponse;
describe("DocumentPreviewComponent", () => {
  beforeEach(() => {
    spyOn(URL, "createObjectURL").and.returnValue("blob:" + location.origin + "/private-pdf"); spyOn(URL, "revokeObjectURL");
    TestBed.configureTestingModule({ imports: [DocumentPreviewComponent], providers: [{ provide: AdeniLocaleService, useValue: { locale: signal("en") } }, { provide: BusinessApiService, useValue: { withAuthorizedClient: (fn: (c: unknown) => unknown) => fn({ insights: { preview: async () => new Blob(["%PDF-test"], { type: "application/pdf" }) } }) } }] });
  });
  it("provides accessible actions outside the PDF, traps/restores focus and cleans its Blob", async () => {
    const opener = document.createElement("button"); document.body.appendChild(opener); opener.focus();
    const fixture = TestBed.createComponent(DocumentPreviewComponent); fixture.componentRef.setInput("document", doc); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).not.toBeNull(); expect(fixture.nativeElement.textContent).toContain("does not issue or send");
    expect(fixture.nativeElement.contains(document.activeElement)).toBeTrue();
    const links = fixture.nativeElement.querySelectorAll("a") as NodeListOf<HTMLAnchorElement>; expect(links.length).toBe(2); expect(links[0]?.textContent).toContain("Open PDF"); expect(links[1]?.download).toContain("invoice-");
    expect(fixture.nativeElement.querySelector("iframe").title).toBe("Preview");
    let closed = false; fixture.componentInstance.closed.subscribe(() => closed = true); document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true })); expect(closed).toBeTrue();
    fixture.destroy(); expect(URL.revokeObjectURL).toHaveBeenCalled(); expect(document.activeElement).toBe(opener); opener.remove();
  });
  it("keeps close controls and a generic error visible when generation fails", async () => {
    const api = TestBed.inject(BusinessApiService); spyOn(api, "withAuthorizedClient").and.rejectWith(new Error("private contents"));
    const fixture = TestBed.createComponent(DocumentPreviewComponent); fixture.componentRef.setInput("document", doc); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull(); expect(fixture.nativeElement.textContent).not.toContain("private contents"); expect(fixture.nativeElement.querySelector("iframe")).toBeNull();
    expect(fixture.nativeElement.querySelector("button")).not.toBeNull(); fixture.destroy();
  });
});
