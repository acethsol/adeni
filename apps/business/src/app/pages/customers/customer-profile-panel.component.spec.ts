import { signal, type WritableSignal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { AdeniLocaleService } from "@adeni/ui";
import { customerActivitySchema, customerProfileSchema, type CustomerProfile, type CustomerActivity, type BusinessPermission } from "@adeni/shared";
import { BusinessSessionService } from "../../core/services/business-session.service";
import { CustomerProfileDataService } from "./customer-profile-data.service";
import { CustomerProfilePanelComponent } from "./customer-profile-panel.component";

describe("CustomerProfilePanelComponent", () => {
  const availability = { booking: "available", finance: "restricted", messaging: "unscoped", preferences: "notImplemented", retention: "notImplemented" };
  let canBook: boolean;
  let data: { profile: WritableSignal<CustomerProfile>;
    activity: WritableSignal<CustomerActivity>;
    loading: WritableSignal<boolean>; loadingMore: WritableSignal<boolean>; error: WritableSignal<unknown>;
    load: jasmine.Spy; loadMore: jasmine.Spy; cancel: jasmine.Spy };

  beforeEach(() => {
    canBook = true;
    data = {
      profile: signal(customerProfileSchema.parse({ tenantCustomerId: "roster", asOf: "2026-10-25", locationId: null, availability,
        visitSummary: { completedBookings: 2, completedWalkIns: 1 },
        nextAppointment: { bookingId: "booking", startAt: "2026-10-26T10:00:00Z", serviceName: "Éclat", status: "Pending" }, recordedOnlinePaymentsByCurrency: null,
        trust: { phoneVerified: true, customerSince: "2026-01-15T00:00:00Z" } })),
      activity: signal(customerActivitySchema.parse({ items: [], nextCursor: null, asOf: "2026-10-25", from: "2026-07-27", isTruncated: true, availability })),
      loading: signal(false), loadingMore: signal(false), error: signal<unknown>(null),
      load: jasmine.createSpy().and.resolveTo(), loadMore: jasmine.createSpy().and.resolveTo(), cancel: jasmine.createSpy(),
    };
    TestBed.configureTestingModule({
      imports: [CustomerProfilePanelComponent],
      providers: [
        provideRouter([]),
        { provide: CustomerProfileDataService, useValue: data },
        { provide: BusinessSessionService, useValue: { ensureLoaded: async () => {}, can: (permission: BusinessPermission) => canBook && permission === "business.bookings" } },
        { provide: AdeniLocaleService, useValue: { locale: signal("en") } },
      ],
    });
  });

  it("renders visits and next appointment while hiding restricted finance", async () => {
    const fixture = TestBed.createComponent(CustomerProfilePanelComponent);
    fixture.componentRef.setInput("customerId", "roster");
    fixture.componentRef.setInput("overviewVisible", true);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain("Completed visits");
    expect(text).toContain("Adeni account trust");
    expect(text).toContain("Phone verified on Adeni");
    expect(text).toContain("Éclat");
    expect(text).toContain("Awaiting approval");
    expect(text).not.toContain("Recorded online payments");
  });

  it("does not request or render the aggregate for self-only practitioners", async () => {
    canBook = false;
    const fixture = TestBed.createComponent(CustomerProfilePanelComponent);
    fixture.componentRef.setInput("customerId", "roster");
    fixture.componentRef.setInput("activityVisible", true);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector("section")).toBeNull();
  });

  it("shows bounded-history and branch-coverage labels without fabricated events", async () => {
    const fixture = TestBed.createComponent(CustomerProfilePanelComponent);
    fixture.componentRef.setInput("customerId", "roster");
    fixture.componentRef.setInput("activityVisible", true);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain("No activity in this view");
    expect(fixture.nativeElement.textContent).toContain("Some older activity");
    expect(fixture.nativeElement.textContent).toContain("Messaging activity is unavailable in branch view");
    fixture.destroy();
  });
});
