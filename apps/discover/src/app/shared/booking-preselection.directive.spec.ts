import { Component, PLATFORM_ID, signal, TransferState } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { ActivatedRoute, convertToParamMap, provideRouter } from "@angular/router";
import { BehaviorSubject } from "rxjs";
import { vi } from "vitest";
import { AdeniLocaleService } from "@adeni/ui";
import type { ServiceOffering } from "@adeni/shared";
import { CustomerApiService } from "../core/services/customer-api.service";
import { BookingPanelComponent } from "./booking-panel.component";
import { BookingPreselectionDirective } from "./booking-preselection.directive";
import { serverRoutes } from "../app.routes.server";
import { RenderMode } from "@angular/ssr";
const id="11111111-1111-4111-8111-111111111111";
@Component({standalone:true,imports:[BookingPanelComponent,BookingPreselectionDirective],template:`<app-booking-panel adeniBookingPreselection slug="studio" [tenantId]="id" [locationId]="id" [services]="services" [bookingEnabled]="true" />`})
class Host {id=id;services:ServiceOffering[]=[{id,name:"Service",description:null,priceAmount:10,currency:"CAD",pricingType:"fixed",durationMinutes:30,isActive:true,isAddOn:false}];}
describe("booking preselection hydration",()=>{
  function configure(platform:string){
    const query=new BehaviorSubject(convertToParamMap({service:id}));const context=vi.fn().mockResolvedValue({serviceId:id,practitionerId:null,path:`/businesses/studio?service=${id}#book`});
    const api={createPublicClient:()=>({calendar:{bookingContext:context}})};
    TestBed.configureTestingModule({imports:[Host],providers:[provideRouter([]),{provide:PLATFORM_ID,useValue:platform},{provide:AdeniLocaleService,useValue:{locale:signal("en")}},{provide:CustomerApiService,useValue:api},{provide:ActivatedRoute,useValue:{queryParamMap:query}}]});return{query,context};
  }
  it("leaves business SSR, canonical route policy and TransferState independent of selectors",async()=>{
    const {context}=configure("server");const fixture=TestBed.createComponent(Host);fixture.detectChanges();await fixture.whenStable();
    expect(context).not.toHaveBeenCalled();expect(TestBed.inject(TransferState).toJson()).toBe("{}");expect(serverRoutes.find(r=>r.path==="businesses/**")?.renderMode).toBe(RenderMode.Server);
  });
  it("applies validated public context once after rendering without creating a booking",async()=>{
    const {context}=configure("browser");const fixture=TestBed.createComponent(Host);fixture.detectChanges();await fixture.whenStable();fixture.detectChanges();await fixture.whenStable();
    expect(context).toHaveBeenCalledTimes(1);expect(fixture.nativeElement.textContent).toContain("Service");
    fixture.detectChanges();expect(context).toHaveBeenCalledTimes(1);
  });
  it("aborts superseded context and prevents an unsafe parameter from reaching the API",async()=>{
    const {context,query}=configure("browser");context.mockImplementation(()=>new Promise(()=>{}));const fixture=TestBed.createComponent(Host);fixture.detectChanges();
    await Promise.resolve();fixture.detectChanges();
    const first=context.mock.calls[0]?.[2] as AbortSignal;expect(first).toBeDefined();
    query.next(convertToParamMap({service:id,price:"1"}));fixture.detectChanges();await Promise.resolve();expect(first.aborted).toBe(true);expect(context).toHaveBeenCalledTimes(1);
    fixture.destroy();
  });
});
