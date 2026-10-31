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

export const attendanceTeamChangedSchema = z.object({
  tenantId: z.string(),
  businessLocationId: z.string().nullable().optional(),
});
export type AttendanceTeamChangedEvent = z.infer<typeof attendanceTeamChangedSchema>;

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

/** Tenant group: admin approved or rejected verification. */
export const tenantVerificationChangedSchema = z.object({
  tenantId: z.string(),
  status: z.string(),
});
export type TenantVerificationChangedEvent = z.infer<typeof tenantVerificationChangedSchema>;

/** Tenant group: trust badge requested, granted or revoked (`badgeType` null = batch after approval). */
export const tenantBadgesChangedSchema = z.object({
  tenantId: z.string(),
  badgeType: z.string().nullable().optional(),
  status: z.string(),
});
export type TenantBadgesChangedEvent = z.infer<typeof tenantBadgesChangedSchema>;

/** Admin verification group: pending trust-badge queue size. */
export const adminBadgesChangedSchema = z.object({
  pendingCount: z.number(),
});
export type AdminBadgesChangedEvent = z.infer<typeof adminBadgesChangedSchema>;

export const readUpdatedRealtimeSchema = z.object({
  conversationId: z.string(),
  participantId: z.string(),
  lastReadMessageId: z.string(),
});
export type ReadUpdatedRealtimeEvent = z.infer<typeof readUpdatedRealtimeSchema>;
