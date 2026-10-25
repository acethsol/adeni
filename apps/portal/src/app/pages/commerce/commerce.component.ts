import { Component, DestroyRef, OnInit, computed, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { PortalPageComponent, injectAdeniApiError, injectAdeniTranslate } from "@adeni/ui";
import { formatPrice, type CommerceRequest, type BusinessGiftCard, type GiftTransaction } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalSessionService } from "../../core/services/portal-session.service";
@Component({selector:"app-commerce",standalone:true,imports:[FormsModule,RouterLink,PortalPageComponent],
 styles:`label { display:block; margin-block:.75rem; } input,select,button { padding:.5rem; } article { border-block-end:1px solid var(--cds-border-subtle,#ddd); padding-block:1rem; } button,a { margin-inline-end:.5rem; }`,
 templateUrl:"./commerce.component.html"})
export class CommerceComponent implements OnInit {
 readonly tr=injectAdeniTranslate();readonly apiErr=injectAdeniApiError();private readonly api=inject(BusinessApiService);private readonly session=inject(PortalSessionService);
 readonly requests=signal<CommerceRequest[]>([]);readonly cards=signal<BusinessGiftCard[]>([]);readonly transactions=signal<GiftTransaction[]>([]);
 readonly selectedCard=signal("");readonly busy=signal(false);readonly error=signal<string|null>(null);readonly notice=signal("");readonly page=signal(1);
 readonly canManage=computed(()=>this.session.loaded() && ["owner","manager"].includes(this.session.permissionRole()));
 amount="";currency="NGN";label="";giftPurchase=false;allowGift=false;readonly formatPrice=formatPrice;
 private abort=new AbortController();private alive=true;private createKey="";private createBody="";private actionKeys=new Map<string,string>();
 constructor(){inject(DestroyRef).onDestroy(()=>{this.alive=false;this.abort.abort();this.requests.set([]);this.cards.set([]);});}
 async ngOnInit():Promise<void>{await this.session.ensureLoaded();await this.load();}
 private async run(action:()=>Promise<void>):Promise<void>{if(this.busy())return;this.busy.set(true);this.error.set(null);try{await action();}catch(e){if(this.alive)this.error.set(this.apiErr(e,"commerce.error"));}finally{if(this.alive)this.busy.set(false);}}
 async load():Promise<void>{await this.run(()=>this.refresh());}
 private async refresh():Promise<void>{const rows=await this.api.withAuthorizedClient(async c=>({requests:await c.commerce.list(this.page(),this.abort.signal),cards:await c.commerce.cards(this.page(),this.abort.signal)}));if(this.alive){this.requests.set(rows.requests);this.cards.set(rows.cards);}}
 async create():Promise<void>{await this.run(async()=>{
   const body={amount:Number(this.amount),currency:this.currency,label:this.label,giftCardPurchase:this.giftPurchase,allowsGiftCard:!this.giftPurchase&&this.allowGift};
   const signature=JSON.stringify(body);if(signature!==this.createBody||!this.createKey){this.createBody=signature;this.createKey=crypto.randomUUID();}
   await this.api.withAuthorizedClient(c=>c.commerce.create(body,this.createKey,this.abort.signal));this.createKey="";this.amount="";this.label="";await this.refresh();
 });}
 private key(action:string):string{if(!this.actionKeys.has(action))this.actionKeys.set(action,crypto.randomUUID());return this.actionKeys.get(action)!;}
 async cancel(r:CommerceRequest):Promise<void>{await this.run(async()=>{await this.api.withAuthorizedClient(c=>c.commerce.cancel(r.id,this.key(`cancel:${r.id}`),this.abort.signal));await this.refresh();});}
 async reverse(r:CommerceRequest):Promise<void>{await this.run(async()=>{await this.api.withAuthorizedClient(c=>c.commerce.reverseGift(r.id,this.key(`reverse:${r.id}`),this.abort.signal));await this.refresh();});}
 async history(card:BusinessGiftCard):Promise<void>{await this.run(async()=>{const rows=await this.api.withAuthorizedClient(c=>c.commerce.transactions(card.id,this.abort.signal));if(this.alive){this.selectedCard.set(card.id);this.transactions.set(rows);}});}
 async refund(r:CommerceRequest):Promise<void>{await this.run(async()=>{await this.api.withAuthorizedClient(c=>c.commerce.refund(r.id,`refund-${r.id}`,this.abort.signal));await this.refresh();});}
 async exportCards():Promise<void>{await this.run(async()=>{const to=new Date();const from=new Date(to);from.setUTCFullYear(from.getUTCFullYear()-1);const blob=await this.api.withAuthorizedClient(c=>c.downloadTenantReportExport({reportKey:"gift-cards",from:from.toISOString(),to:to.toISOString(),format:"csv"}));if(!this.alive)return;const url=URL.createObjectURL(blob);const anchor=document.createElement('a');anchor.href=url;anchor.download='business-gift-cards.csv';anchor.click();URL.revokeObjectURL(url);});}
 async copy(r:CommerceRequest):Promise<void>{await this.run(async()=>{await navigator.clipboard.writeText(r.shareUrl);if(this.alive)this.notice.set(this.tr("commerce.copied"));});}
 whatsapp(r:CommerceRequest):string{return `https://wa.me/?text=${encodeURIComponent(`${r.label} ${r.shareUrl}`)}`;}
 async paginate(delta:number):Promise<void>{if(this.busy())return;this.page.update(p=>Math.max(1,p+delta));this.transactions.set([]);await this.load();}
}
