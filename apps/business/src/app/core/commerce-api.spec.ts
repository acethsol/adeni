import { AdeniApiClient } from '@adeni/api-client';
describe('Commerce API compatibility and credential transport',()=>{
 it('uses a header capability, no-store, cancellation and a stable idempotency header',async()=>{
  const fetcher=jasmine.createSpy().and.callFake(async()=>Response.json({state:'PaymentPending',checkoutUrl:'/checkout/stub/ref'}));
  const client=new AdeniApiClient({baseUrl:'https://api.example',accessToken:'jwt',fetchImpl:fetcher});const abort=new AbortController();
  await client.commerce.checkout('opaque-secret','retry-key',abort.signal);
  const [url,init]=fetcher.calls.mostRecent().args;const headers=new Headers(init.headers);
  expect(url).toBe('https://api.example/api/v1/public/pay-links/checkout');expect(url).not.toContain('opaque-secret');
  expect(headers.get('X-Adeni-Pay-Token')).toBe('opaque-secret');expect(headers.get('Idempotency-Key')).toBe('retry-key');expect(headers.get('Authorization')).toBe('Bearer jwt');expect(init.cache).toBe('no-store');expect(init.signal).toBe(abort.signal);
  expect(client.createPaymentLink).toEqual(jasmine.any(Function));expect(client.intake.submit).toEqual(jasmine.any(Function));
 });
 it('keeps gift codes in the bounded redemption body and validates returned state',async()=>{
  const fetcher=jasmine.createSpy().and.callFake(async()=>Response.json({businessName:'Business',label:'Charge',amount:10,currency:'NGN',state:'Paid',allowsGiftCard:true,requiresCustomerLogin:false}));
  const client=new AdeniApiClient({baseUrl:'',fetchImpl:fetcher});expect((await client.commerce.redeem('token','code','retry')).state).toBe('Paid');
  const [url,init]=fetcher.calls.mostRecent().args;expect(url).not.toContain('code');expect(JSON.parse(init.body)).toEqual({code:'code'});
 });
});
