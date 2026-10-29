import { z } from "zod";
export const commerceRequestSchema = z.object({ id:z.string().uuid(), purpose:z.enum(["CustomCharge","GiftCardPurchase"]), amount:z.number().positive(), currency:z.string(), label:z.string(), allowsGiftCard:z.boolean(), state:z.enum(["Active","PaymentPending","Paid","Expired","Cancelled","Refunded","RefundPending"]), createdAt:z.string(), expiresAt:z.string(), paidAt:z.string().nullable(), shareUrl:z.string(), tender:z.enum(["GiftCard","Provider"]).nullable().optional() });
export const publicCommerceSchema = commerceRequestSchema.pick({label:true,amount:true,currency:true,state:true,allowsGiftCard:true}).extend({businessName:z.string(),requiresCustomerLogin:z.boolean()});
export const commerceCheckoutSchema=z.object({state:z.string(),checkoutUrl:z.string().nullable()});
export const businessGiftCardSchema=z.object({id:z.string().uuid(),businessName:z.string(),originalAmount:z.number(),remainingAmount:z.number(),currency:z.string(),state:z.enum(["PendingPayment","Active","Exhausted","Suspended","Voided"]),codeSuffix:z.string(),issuedAt:z.string().nullable(),businessSlug:z.string().nullable().optional()});
export const giftTransactionSchema=z.object({id:z.string().uuid(),kind:z.string(),delta:z.number(),balanceAfter:z.number(),occurredAt:z.string()});
export const giftCodeSchema=z.object({code:z.string().regex(/^[A-F0-9]{64}$/)});
export type CommerceRequest=z.infer<typeof commerceRequestSchema>;
export type PublicCommerce=z.infer<typeof publicCommerceSchema>;
export type BusinessGiftCard=z.infer<typeof businessGiftCardSchema>;
export type GiftTransaction=z.infer<typeof giftTransactionSchema>;
export interface CreateCommerceRequest { amount:number; currency:string; label:string; giftCardPurchase?:boolean; allowsGiftCard?:boolean; expiresAt?:string }
