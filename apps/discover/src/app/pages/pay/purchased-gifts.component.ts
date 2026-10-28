import { Component, DestroyRef, PLATFORM_ID, inject, signal } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { CustomerApiService } from "../../core/services/customer-api.service";
import { formatPrice, type BusinessGiftCard } from "@adeni/shared";
import { injectAdeniTranslate, injectAdeniApiError } from "@adeni/ui";
@Component({selector:"app-purchased-gifts",standalone:true,styles:`:host{display:block;max-width:40rem;margin:auto;padding:1.5rem;}article{padding-block:1rem;}output{overflow-wrap:anywhere;display:block;}`,
 template:`<h1>{{tr('commerce.giftTitle')}}</h1><p>{{tr('commerce.noWallet')}}</p>@if(error()){<p role="alert">{{error()}}</p>}
 @for(card of cards();track card.id){<article><h2>{{card.businessName}}</h2><p>••••{{card.codeSuffix}} · {{formatPrice(card.remainingAmount,card.currency)}} · {{tr('commerce.states.'+card.state)}}</p>
 <button type="button" [disabled]="busy()" (click)="reveal(card.id)">{{tr('commerce.reveal')}}</button>
 @if(revealedId()===card.id){<output>{{code()}}</output><button type="button" (click)="hide()">{{tr('commerce.hide')}}</button>}</article>}@empty{<p>{{tr('commerce.empty')}}</p>}`})
export class PurchasedGiftsComponent {
 readonly tr=injectAdeniTranslate();readonly apiErr=injectAdeniApiError();private readonly api=inject(CustomerApiService);readonly cards=signal<BusinessGiftCard[]>([]);readonly error=signal<string|null>(null);readonly busy=signal(false);readonly code=signal('');readonly revealedId=signal('');readonly formatPrice=formatPrice;private readonly abort=new AbortController();
 constructor(){inject(DestroyRef).onDestroy(()=>{this.abort.abort();this.hide();this.cards.set([]);});if(isPlatformBrowser(inject(PLATFORM_ID)))void this.load();}
 hide():void{this.code.set('');this.revealedId.set('');}
 async load():Promise<void>{try{const rows=await this.api.withAuthorizedClient(c=>c.commerce.purchasedCards(this.abort.signal));if(!this.abort.signal.aborted)this.cards.set(rows);}catch(e){if(!this.abort.signal.aborted)this.error.set(this.apiErr(e,'commerce.error'));}}
 async reveal(id:string):Promise<void>{if(this.busy())return;this.hide();this.busy.set(true);try{const result=await this.api.withAuthorizedClient(c=>c.commerce.reveal(id,this.abort.signal));if(!this.abort.signal.aborted){this.code.set(result.code);this.revealedId.set(id);}}catch(e){if(!this.abort.signal.aborted)this.error.set(this.apiErr(e,'commerce.error'));}finally{if(!this.abort.signal.aborted)this.busy.set(false);}}
}
