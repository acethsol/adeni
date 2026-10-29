import { signal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { AdeniLocaleService } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { CalendarSyncComponent } from "./calendar-sync.component";
import { BookingLinksComponent } from "./booking-links.component";

describe("calendar management and booking links",()=>{
  let calendar:Record<string,jasmine.Spy>;
  beforeEach(()=>{
    calendar={connections:jasmine.createSpy().and.resolveTo([]),conflicts:jasmine.createSpy().and.resolveTo([]),calendars:jasmine.createSpy().and.resolveTo([{id:"busy",name:"Work",timeZoneId:"UTC",canWrite:false},{id:"output",name:"Adeni",timeZoneId:"UTC",canWrite:true}]),select:jasmine.createSpy().and.resolveTo(true),publishLink:jasmine.createSpy().and.resolveTo({url:"https://discover.example.test/book/studio",html:'<a href="https://discover.example.test/book/studio">Book with Adeni</a>'})};
    TestBed.configureTestingModule({imports:[CalendarSyncComponent,BookingLinksComponent],providers:[provideRouter([]),{provide:AdeniLocaleService,useValue:{locale:signal("en")}},{provide:BusinessApiService,useValue:{withAuthorizedClient:(fn:(c:unknown)=>unknown)=>fn({calendar,listTenantStaff:async()=>[],getTenantLocations:async()=>[],getTenantServices:async()=>[]})}}]});
  });
  it("keeps busy sources and the optional write target separate",async()=>{
    const fixture=TestBed.createComponent(CalendarSyncComponent);fixture.detectChanges();await fixture.whenStable();const component=fixture.componentInstance;
    await component.edit({id:"connection",staffMemberId:"staff",provider:"google",status:"connected",lastSyncedAt:null,coveredUntil:null,failureCategory:null,selections:[]});
    component.toggle("busy");component.outputId="output";await component.save();
    expect(calendar["select"].calls.mostRecent().args[1]).toEqual([{id:"busy",timeZoneId:"UTC",isWriteTarget:false},{id:"output",timeZoneId:"UTC",isWriteTarget:true}]);
    expect(component.editing()).toBeNull();
  });
  it("aborts pending reads and ignores late results on destruction",async()=>{
    let finish!:(rows:unknown[])=>void;calendar["connections"].and.returnValue(new Promise(resolve=>finish=resolve));
    const fixture=TestBed.createComponent(CalendarSyncComponent);fixture.detectChanges();await Promise.resolve();await Promise.resolve();await Promise.resolve();
    const abort=calendar["connections"].calls.mostRecent().args[0] as AbortSignal;fixture.destroy();finish([{id:"late"}]);await Promise.resolve();await Promise.resolve();
    expect(abort.aborted).toBeTrue();expect(fixture.componentInstance.connections()).toEqual([]);
  });
  it("requests server-generated link/snippet and clears it when selection changes",async()=>{
    const fixture=TestBed.createComponent(BookingLinksComponent);fixture.detectChanges();await fixture.whenStable();const component=fixture.componentInstance;
    component.locationId="location";component.serviceId="service";await component.generate();expect(component.link()?.html).toContain("<a href=");
    expect(calendar["publishLink"].calls.mostRecent().args.slice(0,2)).toEqual(["location",{service:"service",practitioner:undefined}]);
    component.clear();expect(component.link()).toBeNull();
  });
});
