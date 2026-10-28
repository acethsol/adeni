import { signal } from '@angular/core';import { TestBed } from '@angular/core/testing';import { provideRouter } from '@angular/router';
import { AdeniLocaleService } from '@adeni/ui';import { BusinessApiService } from '../../core/services/business-api.service';import { PortalSessionService } from '../../core/services/portal-session.service';import { CommerceComponent } from './commerce.component';
describe('Quick Pay page',()=>{
 let commerce:{list:jasmine.Spy;cards:jasmine.Spy;create:jasmine.Spy};let role:ReturnType<typeof signal<string>>;
 beforeEach(()=>{role=signal('owner');commerce={list:jasmine.createSpy().and.resolveTo([]),cards:jasmine.createSpy().and.resolveTo([]),create:jasmine.createSpy().and.resolveTo({})};
  TestBed.configureTestingModule({imports:[CommerceComponent],providers:[provideRouter([]),{provide:AdeniLocaleService,useValue:{locale:signal('en')}},{provide:PortalSessionService,useValue:{ensureLoaded:async()=>{},loaded:signal(true),permissionRole:role}},{provide:BusinessApiService,useValue:{withAuthorizedClient:(fn:(c:unknown)=>unknown)=>fn({commerce})}}]});});
 it('keeps purchase requests separate from gift redemption and preserves retry keys after failed responses',async()=>{
  const fixture=TestBed.createComponent(CommerceComponent);fixture.detectChanges();await fixture.whenStable();const c=fixture.componentInstance;
  commerce.create.and.rejectWith(new Error('lost response'));c.amount='50';c.currency='CAD';c.label='Gift';c.giftPurchase=true;c.allowGift=true;
  await c.create();const key=commerce.create.calls.mostRecent().args[1];await c.create();expect(commerce.create.calls.mostRecent().args[1]).toBe(key);
  expect(commerce.create.calls.mostRecent().args[0].allowsGiftCard).toBeFalse();
  c.amount='60';await c.create();expect(commerce.create.calls.mostRecent().args[1]).not.toBe(key);
 });
 it('limits financial controls to management and uses ordinary WhatsApp deep links',async()=>{
  role.set('accountant');const fixture=TestBed.createComponent(CommerceComponent);fixture.detectChanges();await fixture.whenStable();
  expect(fixture.nativeElement.querySelector('form')).toBeNull();expect(fixture.componentInstance.canManage()).toBeFalse();
  const share=fixture.componentInstance.whatsapp({label:'Walk-in',shareUrl:'https://discover/pay#t=opaque'} as never);expect(share).toContain('https://wa.me/?text=');expect(decodeURIComponent(share)).toContain('#t=opaque');
 });
 it('aborts pending reads and discards late responses on destruction',async()=>{
  let finish!:(rows:unknown[])=>void;commerce.list.and.returnValue(new Promise(resolve=>{finish=resolve;}));
  const fixture=TestBed.createComponent(CommerceComponent);fixture.detectChanges();await Promise.resolve();await Promise.resolve();
  const abort=commerce.list.calls.mostRecent().args[1] as AbortSignal;fixture.destroy();finish([]);await Promise.resolve();expect(abort.aborted).toBeTrue();expect(fixture.componentInstance.requests()).toEqual([]);
 });
});
