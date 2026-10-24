import { signal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { Subject } from "rxjs";
import { AdeniFeedbackService, AdeniLocaleService, MessagingRealtimeService, TenantOperationsRealtimeService } from "@adeni/ui";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
import { PortalSessionService } from "../../core/services/portal-session.service";
import { DashboardComponent } from "./dashboard.component";
import { DashboardDataService } from "./dashboard-data.service";

describe("Dashboard container lifecycle", () => {
  it("uses one data loader and tears down each realtime subscription", async () => {
    const messages = new Subject<unknown>();
    const pending = new Subject<{ pendingCount: number }>();
    const quotes = new Subject<{ openCount: number }>();
    const branches = new Subject<void>();
    const locale = signal<"en" | "fr">("en");
    const data = { load: jasmine.createSpy().and.rejectWith(new Error("unavailable")) };
    TestBed.configureTestingModule({ imports: [DashboardComponent], providers: [
      { provide: ADENI_PORTAL_CONFIG, useValue: {} },
      { provide: AdeniLocaleService, useValue: { locale } },
      { provide: BusinessApiService, useValue: { withAuthorizedClient: () => Promise.resolve(4) } },
      { provide: PortalActiveLocationService, useValue: { selectionChanged$: branches, runOnBranchSwitch: (fn: () => unknown) => fn() } },
      { provide: PortalSessionService, useValue: { ensureLoaded: () => Promise.resolve(), can: () => false, permissionRole: signal("owner") } },
      { provide: AdeniFeedbackService, useValue: { runLoading: (fn: () => unknown) => fn() } },
      { provide: MessagingRealtimeService, useValue: { conversationUpdated$: messages } },
      { provide: TenantOperationsRealtimeService, useValue: { pendingBookingsChanged$: pending, quotePipelineChanged$: quotes } },
    ] });
    TestBed.overrideComponent(DashboardComponent, { set: { template: "", imports: [], providers: [{ provide: DashboardDataService, useValue: data }] } });
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    fixture.detectChanges();
    await fixture.whenStable();
    const component = fixture.componentInstance;
    expect(data.load).toHaveBeenCalledTimes(1);
    expect(component.loadError()).toBeTrue();
    expect(messages.observers.length).toBe(1);
    expect(pending.observers.length).toBe(1);
    expect(quotes.observers.length).toBe(1);
    pending.next({ pendingCount: 7 });
    quotes.next({ openCount: 3 });
    expect(component.pendingCount()).toBe(7);
    expect(component.openQuotesCount()).toBe(3);
    const english = component.rangePresetOptions()[0].label;
    locale.set("fr");
    expect(component.rangePresetOptions()[0].label).not.toBe(english);
    fixture.destroy();
    expect(messages.observers.length + pending.observers.length + quotes.observers.length + branches.observers.length).toBe(0);
    pending.next({ pendingCount: 99 });
    expect(component.pendingCount()).toBe(7);
  });
});
