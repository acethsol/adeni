import { signal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { AdeniLocaleService } from "@adeni/ui";
import type { CustomerSegments } from "@adeni/shared/customer-segments";
import { CustomerSegmentChipsComponent } from "./customer-segment-chips.component";
const value: CustomerSegments = { tenantCustomerId: "90000000-0000-4000-8000-000000000001", availability: "available", asOf: "2026-10-26T12:00:00Z", definitionVersion: "v1", locationId: null, timeZoneId: "Africa/Lagos", approximateVisitDates: false, matches: [{ id: "vip", count: 6, daysSinceLastVisit: 1 }] };
describe("CustomerSegmentChipsComponent", () => {
  it("updates computed presentation and explains frequency/scope without a financial score", () => {
    const locale = signal("en"); TestBed.configureTestingModule({ imports: [CustomerSegmentChipsComponent], providers: [{ provide: AdeniLocaleService, useValue: { locale } }] });
    const fixture = TestBed.createComponent(CustomerSegmentChipsComponent); fixture.componentRef.setInput("value", value); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain("VIP — frequent visitor"); expect(fixture.nativeElement.textContent).toContain("6 recorded visit(s)"); expect(fixture.nativeElement.textContent).toContain("Business scope");
    locale.set("fr"); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain("VIP — visites fréquentes");
  });
  it("distinguishes unavailable evidence from a supported segment with no matches", () => {
    TestBed.configureTestingModule({ imports: [CustomerSegmentChipsComponent], providers: [{ provide: AdeniLocaleService, useValue: { locale: signal("en") } }] }); const fixture = TestBed.createComponent(CustomerSegmentChipsComponent);
    fixture.componentRef.setInput("value", { ...value, matches: [], availability: "partial" }); fixture.detectChanges(); expect(fixture.nativeElement.querySelector("summary").textContent).toContain("Incomplete source history");
    fixture.componentRef.setInput("value", { ...value, matches: [] }); fixture.detectChanges(); expect(fixture.nativeElement.querySelector("summary").textContent).toContain("No current matches");
  });
});
