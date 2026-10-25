import { Component, DestroyRef, PLATFORM_ID, inject, signal } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { injectAdeniTranslate, injectAdeniApiError } from "@adeni/ui";
import { formatPrice, type PublicCommerce } from "@adeni/shared";
import { CustomerApiService } from "../../core/services/customer-api.service";
@Component({selector:"app-pay",standalone:true,imports:[FormsModule,RouterLink],
 styles:`:host {display:block;max-width:36rem;margin:auto;padding:1.5rem;} label {display:block;margin-block:1rem;} input,button {padding:.75rem;max-width:100%;} p {margin-block:1rem;}`,
 template:`<h1>{{tr('commerce.title')}}</h1><p>{{tr('commerce.sandbox')}}</p><p>{{tr('commerce.noWallet')}}</p>
 @if(error()){<p role="alert">{{error()}}</p>}
 @if(view();as r){<h2>{{r.businessName}}</h2><p>{{r.label}}</p><p>{{formatPrice(r.amount,r.currency)}}</p>
  @if(r.state==='Paid'){<p role="status">{{tr('commerce.paid')}}</p><a routerLink="/my-gift-cards">{{tr('commerce.giftTitle')}}</a>}
  @else if(r.state==='Active'||r.state==='PaymentPending'){
    @if(r.state==='PaymentPending'){<p role="status">{{tr('commerce.pending')}}</p>}
    @if(r.requiresCustomerLogin&&!loggedIn()){<p>{{tr('commerce.reopenAfterLogin')}}</p><button type="button" (click)="login()">{{tr('commerce.login')}}</button>}
    @else{<button type="button" [disabled]="busy()" (click)="checkout()">{{tr('commerce.pay')}}</button>}
    @if(r.allowsGiftCard&&r.state==='Active'){<form ngNativeValidate (ngSubmit)="redeem()"><label>{{tr('commerce.code')}}<input name="giftCode" [(ngModel)]="code" required minlength="64" maxlength="64" autocomplete="off" spellcheck="false" /></label><button type="submit" [disabled]="busy()">{{tr('commerce.redeem')}}</button></form>}
  }@else{<p>{{tr('commerce.closed')}}</p>}
  <button type="button" [disabled]="busy()" (click)="refresh()">{{tr('commerce.refresh')}}</button>
 }`})
export class PayComponent {
 readonly tr=injectAdeniTranslate();readonly apiErr=injectAdeniApiError();private readonly api=inject(CustomerApiService);private readonly route=inject(ActivatedRoute);
 readonly view=signal<PublicCommerce|null>(null);readonly error=signal<string|null>(null);readonly busy=signal(false);readonly loggedIn=signal(false);readonly formatPrice=formatPrice;
 code="";private token="";private checkoutKey="";private redeemKey="";private redeemCode="";private abort=new AbortController();private alive=true;
 constructor(){const destroy=inject(DestroyRef);destroy.onDestroy(()=>{this.alive=false;this.abort.abort();this.token="";this.code="";this.redeemCode="";this.checkoutKey="";this.redeemKey="";this.view.set(null);});
   if(isPlatformBrowser(inject(PLATFORM_ID)))this.route.fragment.pipe(takeUntilDestroyed(destroy)).subscribe(fragment=>{void this.load(new URLSearchParams(fragment??"").get("t")??"");});}
 async load(token:string):Promise<void>{this.abort.abort();this.abort=new AbortController();this.token=token;this.checkoutKey=crypto.randomUUID();this.redeemKey="";this.code="";this.view.set(null);await this.refresh();}
 async refresh():Promise<void>{const signal=this.abort.signal;this.busy.set(true);this.error.set(null);try{const r=await this.api.createPublicClient().commerce.resolve(this.token,signal);const loggedIn=await this.api.isLoggedIn();if(!signal.aborted){this.view.set(r);this.loggedIn.set(loggedIn);}}catch(e){if(!signal.aborted)this.error.set(this.apiErr(e,"commerce.error"));}finally{if(this.alive&&!signal.aborted)this.busy.set(false);}}
 async login():Promise<void>{await this.api.login("/my-gift-cards");}
 async checkout():Promise<void>{if(this.busy()||!this.view())return;const signal=this.abort.signal;this.busy.set(true);this.error.set(null);try{
   const result=this.view()!.requiresCustomerLogin?await this.api.withAuthorizedClient(c=>c.commerce.checkout(this.token,this.checkoutKey,signal)):await this.api.createPublicClient().commerce.checkout(this.token,this.checkoutKey,signal);
   if(signal.aborted)return;
   if(result.checkoutUrl){const url=new URL(result.checkoutUrl,window.location.origin);if(url.origin!==window.location.origin||!url.pathname.startsWith('/checkout/stub/'))throw new Error('Unsupported checkout destination');window.location.assign(url.href);}
   else await this.refresh();
 }catch(e){if(!signal.aborted)this.error.set(this.apiErr(e,"commerce.error"));}finally{if(this.alive&&!signal.aborted)this.busy.set(false);}}
 async redeem():Promise<void>{if(this.busy())return;const signal=this.abort.signal;this.busy.set(true);this.error.set(null);if(this.redeemCode!==this.code||!this.redeemKey){this.redeemCode=this.code;this.redeemKey=crypto.randomUUID();}try{const result=await this.api.createPublicClient().commerce.redeem(this.token,this.code,this.redeemKey,signal);if(!signal.aborted){this.view.set(result);this.code="";this.redeemCode="";}}catch(e){if(!signal.aborted)this.error.set(this.apiErr(e,"commerce.error"));}finally{if(this.alive&&!signal.aborted)this.busy.set(false);}}
}
