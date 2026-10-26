import { Component, DestroyRef, OnInit, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { PortalPageComponent, AdeniLocaleService, injectAdeniApiError } from "@adeni/ui";
import { calendarUiText } from "@adeni/shared/calendar-copy";
import type { BookingLink, BusinessLocation, ServiceOffering, StaffMember } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";
@Component({ selector: "app-booking-links", standalone: true, imports: [FormsModule, PortalPageComponent], templateUrl: "./booking-links.component.html" })
export class BookingLinksComponent implements OnInit {
  private readonly locale=inject(AdeniLocaleService);
  readonly tr=(key:string)=>calendarUiText(this.locale.locale(),key.replace(/^calendarUi\./,"")); private readonly apiErr=injectAdeniApiError(); private readonly api=inject(BusinessApiService);
  readonly locations=signal<BusinessLocation[]>([]); readonly services=signal<ServiceOffering[]>([]); readonly staff=signal<StaffMember[]>([]);
  readonly link=signal<BookingLink|null>(null); readonly busy=signal(false); readonly error=signal<string|null>(null); readonly notice=signal("");
  locationId=""; serviceId=""; practitionerId=""; private abort=new AbortController(); private alive=true;
  constructor(){inject(DestroyRef).onDestroy(()=>{this.alive=false;this.abort.abort();});}
  async ngOnInit():Promise<void>{await this.run(async()=>{const data=await this.api.withAuthorizedClient(async c=>({locations:await c.getTenantLocations(),services:await c.getTenantServices(),staff:await c.listTenantStaff()}));if(!this.alive)return;this.locations.set(data.locations.filter(x=>x.isActive));this.services.set(data.services.filter(x=>x.isActive&&!x.isAddOn));this.staff.set(data.staff.filter(x=>x.isActive));this.locationId=data.locations.find(x=>x.isPrimary&&x.isActive)?.id??"";});}
  private async run(fn:()=>Promise<void>):Promise<void>{if(this.busy())return;this.busy.set(true);this.error.set(null);try{await fn();}catch(e){if(this.alive)this.error.set(this.apiErr(e,"calendarUi.error"));}finally{if(this.alive)this.busy.set(false);}}
  clear():void{this.link.set(null);this.notice.set("");}
  async generate():Promise<void>{await this.run(async()=>{this.link.set(null);const link=await this.api.withAuthorizedClient(c=>c.calendar.publishLink(this.locationId,{service:this.serviceId||undefined,practitioner:this.practitionerId||undefined},this.abort.signal));if(this.alive)this.link.set(link);});}
  async copy(html=false):Promise<void>{const link=this.link();if(!link)return;await this.run(async()=>{await navigator.clipboard.writeText(html?link.html:link.url);if(this.alive)this.notice.set(this.tr("calendarUi.copied"));});}
}
