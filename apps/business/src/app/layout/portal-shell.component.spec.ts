import { signal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { Subject } from "rxjs";
import { AdeniDocumentTitleStrategy, AdeniLocaleService, MessagingRealtimeService, PortalRealtimeBootstrapService, TenantOperationsRealtimeService } from "@adeni/ui";
import { ADENI_PORTAL_CONFIG } from "../core/adeni-config";
import { PortalThemeService } from "../core/portal-theme.service";
import { BusinessApiService } from "../core/services/business-api.service";
import { PortalActiveLocationService } from "../core/services/portal-active-location.service";
import { PortalNavBadgesService } from "../core/services/portal-nav-badges.service";
import { PortalSessionService } from "../core/services/portal-session.service";
import { PortalShellComponent } from "./portal-shell.component";

describe("Portal shell lifecycle", () => {
  it("keeps one subscription per event and cleans up when the shell is destroyed", async () => {
    const messages = new Subject<void>();
    const pending = new Subject<unknown>();
    const verification = new Subject<unknown>();
    const refresh = new Subject<void>();
    const branches = new Subject<void>();
    TestBed.configureTestingModule({ imports: [PortalShellComponent], providers: [provideRouter([]),
      { provide: ADENI_PORTAL_CONFIG, useValue: { auth0: { domain: "", clientId: "" }, devBusinessAuth0Sub: "dev", discoverWebUrl: "https://discover.example" } },
      { provide: AdeniLocaleService, useValue: { locale: signal("en"), locales: [] } },
      { provide: AdeniDocumentTitleStrategy, useValue: { setAppTitle: () => undefined } },
      { provide: PortalThemeService, useValue: { preference: signal("system"), theme: signal("light") } },
      { provide: BusinessApiService, useValue: { getTenantProfile: () => Promise.reject(new Error("not registered")), withAuthorizedClient: () => Promise.resolve([{ items: [] }, 0]) } },
      { provide: PortalSessionService, useValue: { refresh: () => Promise.resolve(), permissions: signal([]), permissionRole: signal("owner") } },
      { provide: PortalActiveLocationService, useValue: { selectionChanged$: branches, runOnBranchSwitch: () => Promise.resolve(), tenantApiLocationId: () => undefined } },
      { provide: PortalNavBadgesService, useValue: { refreshRequested$: refresh } },
      { provide: PortalRealtimeBootstrapService, useValue: { stop: () => Promise.resolve() } },
      { provide: MessagingRealtimeService, useValue: { conversationUpdated$: messages } },
      { provide: TenantOperationsRealtimeService, useValue: { pendingBookingsChanged$: pending, tenantVerificationChanged$: verification } },
    ] });
    TestBed.overrideComponent(PortalShellComponent, { set: { template: "", imports: [] } });
    const fixture = TestBed.createComponent(PortalShellComponent);
    fixture.detectChanges();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.componentInstance.profileLoaded()).toBeTrue();
    expect(fixture.componentInstance.visibleGroups().flatMap(group => group.items).some(item => item.path === "/register")).toBeTrue();
    for (const stream of [messages, pending, verification, refresh, branches]) expect(stream.observers.length).toBe(1);
    fixture.destroy();
    for (const stream of [messages, pending, verification, refresh, branches]) expect(stream.observers.length).toBe(0);
  });
});
