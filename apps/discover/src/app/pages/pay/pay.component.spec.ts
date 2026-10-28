import { PLATFORM_ID, signal, TransferState } from '@angular/core';import { TestBed } from '@angular/core/testing';import { ActivatedRoute,provideRouter } from '@angular/router';import { of,Subject } from 'rxjs';import { vi } from 'vitest';import { RenderMode } from '@angular/ssr';import { AdeniLocaleService } from '@adeni/ui';
import { CustomerApiService } from '../../core/services/customer-api.service';import { serverRoutes } from '../../app.routes.server';import { PayComponent } from './pay.component';import { PurchasedGiftsComponent } from './purchased-gifts.component';
describe('private payment rendering',()=>{
 it('keeps pay and gift-code pages private while public business SEO remains server rendered',()=>{
  for(const path of ['pay','my-gift-cards']){const r=serverRoutes.find(r=>r.path===path)!;expect(r.renderMode).toBe(RenderMode.Client);expect(r.headers?.['Cache-Control']).toContain('no-store');expect(r.headers?.['X-Robots-Tag']).toContain('noindex');expect(r.headers?.['Referrer-Policy']).toBe('no-referrer');}
  expect(serverRoutes.find(r=>r.path==='businesses/**')!.renderMode).toBe(RenderMode.Server);
 });
 it('does not fetch or serialize tokens, gift codes or customer data during SSR',async()=>{
  const api={createPublicClient:vi.fn(),withAuthorizedClient:vi.fn()};TestBed.configureTestingModule({imports:[PayComponent,PurchasedGiftsComponent],providers:[provideRouter([]),{provide:PLATFORM_ID,useValue:'server'},{provide:AdeniLocaleService,useValue:{locale:signal('en')}},{provide:CustomerApiService,useValue:api},{provide:ActivatedRoute,useValue:{fragment:of('t=secret-sentinel')}}]});
  const pay=TestBed.createComponent(PayComponent);const gifts=TestBed.createComponent(PurchasedGiftsComponent);pay.detectChanges();gifts.detectChanges();await pay.whenStable();
  expect(api.createPublicClient).not.toHaveBeenCalled();expect(api.withAuthorizedClient).not.toHaveBeenCalled();expect(TestBed.inject(TransferState).toJson()).toBe('{}');expect(pay.nativeElement.textContent).not.toContain('secret-sentinel');
 });
 it('ignores browser success markers and aborts a stale token read',async()=>{
  const fragments=new Subject<string>();const resolve=vi.fn().mockImplementation((_t:string,s:AbortSignal)=>new Promise((_r,_j)=>{}));const api={createPublicClient:()=>({commerce:{resolve}}),isLoggedIn:async()=>false};
  TestBed.configureTestingModule({imports:[PayComponent],providers:[provideRouter([]),{provide:PLATFORM_ID,useValue:'browser'},{provide:AdeniLocaleService,useValue:{locale:signal('en')}},{provide:CustomerApiService,useValue:api},{provide:ActivatedRoute,useValue:{fragment:fragments}}]});
  const fixture=TestBed.createComponent(PayComponent);fragments.next('t=first&success=true');const first=resolve.mock.calls[0][1] as AbortSignal;expect(fixture.componentInstance.view()).toBeNull();fragments.next('t=second');expect(first.aborted).toBe(true);const second=resolve.mock.calls[1][1] as AbortSignal;fixture.destroy();expect(second.aborted).toBe(true);
 });
});
