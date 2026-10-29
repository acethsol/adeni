import { Component, DestroyRef, PLATFORM_ID, inject, signal } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { CustomerApiService } from "../../core/services/customer-api.service";
import { formatPrice, type BusinessGiftCard } from "@adeni/shared";
import { injectAdeniTranslate, injectAdeniApiError } from "@adeni/ui";
@Component({selector:"app-purchased-gifts",standalone:true,styles:`:host{display:block;width:min(46rem,100%);margin:0 auto;}h1{margin:0 0 .35rem;font-size:1.6rem;letter-spacing:-.03em;}.lede{margin:0 0 1.25rem;color:var(--text-muted);}article{margin-bottom:.85rem;padding:1.1rem 1.25rem;border:1px solid var(--cds-border-subtle,#e0e0e0);background:var(--cds-layer-01,#fff);}article h2{margin:0;font-size:1.05rem;}article p{margin:.35rem 0 .85rem;color:var(--text-muted);}output{overflow-wrap:anywhere;display:block;margin:.85rem 0;padding:.65rem .75rem;background:var(--bg-subtle);font-family:monospace;}[role=alert]{padding:.75rem 1rem;border-left:3px solid #da1e28;background:#fff1f1;color:#750e13;}`,
 template:`<h1>{{tr('commerce.giftTitle')}}</h1><p class="lede">{{tr('commerce.noWallet')}}</p>@if(error()){<p role="alert">{{error()}}</p>}
 @for(card of cards();track card.id){<article><h2>{{card.businessName}}</h2><p>••••{{card.codeSuffix}} · {{formatPrice(card.remainingAmount,card.currency)}} · {{tr('commerce.states.'+card.state)}}</p>
 <button type="button" class="cds-btn cds-btn--tertiary cds-btn--sm" [disabled]="busy()" (click)="reveal(card.id)">{{tr('commerce.reveal')}}</button>
 @if(revealedId()===card.id){<output>{{code()}}</output><button type="button" class="cds-btn cds-btn--ghost cds-btn--sm" (click)="hide()">{{tr('commerce.hide')}}</button>}</article>}@empty{<p>{{tr('commerce.empty')}}</p>}`})
export class PurchasedGiftsComponent {
 readonly tr=injectAdeniTranslate();readonly apiErr=injectAdeniApiError();private readonly api=inject(CustomerApiService);readonly cards=signal<BusinessGiftCard[]>([]);readonly error=signal<string|null>(null);readonly busy=signal(false);readonly code=signal('');readonly revealedId=signal('');readonly formatPrice=formatPrice;private readonly abort=new AbortController();
 constructor(){inject(DestroyRef).onDestroy(()=>{this.abort.abort();this.hide();this.cards.set([]);});if(isPlatformBrowser(inject(PLATFORM_ID)))void this.load();}
 hide():void{this.code.set('');this.revealedId.set('');}
 async load():Promise<void>{try{const rows=await this.api.withAuthorizedClient(c=>c.commerce.purchasedCards(this.abort.signal));if(!this.abort.signal.aborted)this.cards.set(rows);}catch(e){if(!this.abort.signal.aborted)this.error.set(this.apiErr(e,'commerce.error'));}}
 async reveal(id:string):Promise<void>{if(this.busy())return;this.hide();this.busy.set(true);try{const result=await this.api.withAuthorizedClient(c=>c.commerce.reveal(id,this.abort.signal));if(!this.abort.signal.aborted){this.code.set(result.code);this.revealedId.set(id);}}catch(e){if(!this.abort.signal.aborted)this.error.set(this.apiErr(e,'commerce.error'));}finally{if(!this.abort.signal.aborted)this.busy.set(false);}}
}
