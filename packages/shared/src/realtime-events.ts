import { z } from "zod";

export const pendingBookingsChangedSchema = z.object({
  tenantId: z.string(),
  pendingCount: z.number(),
});
export type PendingBookingsChangedEvent = z.infer<typeof pendingBookingsChangedSchema>;

export const queueFloorChangedSchema = z.object({
  tenantId: z.string(),
});
export type QueueFloorChangedEvent = z.infer<typeof queueFloorChangedSchema>;

export const quotePipelineChangedSchema = z.object({
  tenantId: z.string(),
  openCount: z.number(),
});
export type QuotePipelineChangedEvent = z.infer<typeof quotePipelineChangedSchema>;

export const paymentCompletedRealtimeSchema = z.object({
  tenantId: z.string(),
  paymentIntentId: z.string(),
  bookingId: z.string().nullable().optional(),
  providerReference: z.string().nullable().optional(),
});
export type PaymentCompletedRealtimeEvent = z.infer<typeof paymentCompletedRealtimeSchema>;

export const adminVerificationChangedSchema = z.object({
  pendingCount: z.number(),
});
export type AdminVerificationChangedEvent = z.infer<typeof adminVerificationChangedSchema>;

export const readUpdatedRealtimeSchema = z.object({
  conversationId: z.string(),
  participantId: z.string(),
  lastReadMessageId: z.string(),
});
export type ReadUpdatedRealtimeEvent = z.infer<typeof readUpdatedRealtimeSchema>;
