import { describe, it, expect } from 'vitest';
import { businessGiftCardSchema, publicCommerceSchema, commerceRequestSchema } from './commerce';
describe('commerce contracts',()=>{
 it('keeps publicly payable requests free of internal identifiers and redeemable codes',()=>{
  const parsed=publicCommerceSchema.parse({businessName:'Tenant business',label:'Walk-in',amount:100,currency:'NGN',state:'Active',allowsGiftCard:true,requiresCustomerLogin:false,tenantId:'private',code:'secret'});
  expect(parsed).not.toHaveProperty('tenantId');expect(parsed).not.toHaveProperty('code');expect(parsed.state).toBe('Active');
 });
 it('models merchant-specific card value without a wallet or implicit currency conversion',()=>{
  const card=businessGiftCardSchema.parse({id:'11111111-1111-4111-8111-111111111111',businessName:'Merchant',originalAmount:100,remainingAmount:40,currency:'CAD',state:'Active',codeSuffix:'12345678',issuedAt:'2026-10-25'});
  expect(card.currency).toBe('CAD');expect(card).not.toHaveProperty('code');expect(card).not.toHaveProperty('platformBalance');
 });
 it('rejects unsupported states and malformed payment schemas',()=>{
  expect(publicCommerceSchema.safeParse({state:'browser-success'}).success).toBe(false);
  expect(commerceRequestSchema.safeParse({amount:-1}).success).toBe(false);
 });
});
