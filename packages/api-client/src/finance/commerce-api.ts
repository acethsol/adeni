import { commerceRequestSchema, publicCommerceSchema, commerceCheckoutSchema, businessGiftCardSchema, giftCodeSchema, giftTransactionSchema, type CreateCommerceRequest } from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";
export class CommerceApi {
  constructor(private readonly client:ApiClientContext){}
  private async call<T>(path:string,schema:{parse(value:unknown):T},options:RequestInit & {idempotencyKey?:string}={}) {
    const response=await this.client.request(path,{cache:"no-store",...options});return schema.parse(await response.json());
  }
  list(page=1,signal?:AbortSignal){return this.call(`/api/v1/tenant/finance/pay-links?page=${page}`,commerceRequestSchema.array(),{signal});}
  create(body:CreateCommerceRequest,key:string,signal?:AbortSignal){return this.call("/api/v1/tenant/finance/pay-links",commerceRequestSchema,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(body),idempotencyKey:key,signal});}
  cancel(id:string,key:string,signal?:AbortSignal){return this.call(`/api/v1/tenant/finance/pay-links/${encodeURIComponent(id)}/cancel`,commerceRequestSchema,{method:"POST",idempotencyKey:key,signal});}
  reverseGift(id:string,key:string,signal?:AbortSignal){return this.call(`/api/v1/tenant/finance/pay-links/${encodeURIComponent(id)}/reverse-gift-redemption`,commerceRequestSchema,{method:"POST",idempotencyKey:key,signal});}
  refund(id:string,key:string,signal?:AbortSignal){return this.call(`/api/v1/tenant/finance/pay-links/${encodeURIComponent(id)}/refund`,commerceRequestSchema,{method:"POST",idempotencyKey:key,signal});}
  customerCards(id:string,signal?:AbortSignal){return this.call(`/api/v1/tenant/customers/${encodeURIComponent(id)}/purchased-gift-cards`,businessGiftCardSchema.array(),{signal});}
  cards(page=1,signal?:AbortSignal){return this.call(`/api/v1/tenant/finance/gift-cards?page=${page}`,businessGiftCardSchema.array(),{signal});}
  transactions(id:string,signal?:AbortSignal){return this.call(`/api/v1/tenant/finance/gift-cards/${encodeURIComponent(id)}/transactions`,giftTransactionSchema.array(),{signal});}
  resolve(token:string,signal?:AbortSignal){return this.call("/api/v1/public/pay-links/resolve",publicCommerceSchema,{headers:{"X-Adeni-Pay-Token":token},signal});}
  checkout(token:string,key:string,signal?:AbortSignal){return this.call("/api/v1/public/pay-links/checkout",commerceCheckoutSchema,{method:"POST",headers:{"X-Adeni-Pay-Token":token},idempotencyKey:key,signal});}
  redeem(token:string,code:string,key:string,signal?:AbortSignal){return this.call("/api/v1/public/pay-links/redeem",publicCommerceSchema,{method:"POST",headers:{"X-Adeni-Pay-Token":token,"Content-Type":"application/json"},body:JSON.stringify({code}),idempotencyKey:key,signal});}
  purchasedCards(signal?:AbortSignal){return this.call("/api/v1/me/gift-cards",businessGiftCardSchema.array(),{signal});}
  reveal(id:string,signal?:AbortSignal){return this.call(`/api/v1/me/gift-cards/${encodeURIComponent(id)}/reveal`,giftCodeSchema,{method:"POST",signal});}
}
