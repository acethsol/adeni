import { z } from "zod";

export const customerSourceAvailabilitySchema = z.object({
  booking: z.enum(["available"]),
  finance: z.enum(["available", "restricted", "notEntitled", "notLinked"]),
  messaging: z.enum(["available", "restricted", "notEntitled", "notLinked", "unscoped"]),
  preferences: z.literal("notImplemented"),
  retention: z.literal("notImplemented"),
});

export const customerProfileSchema = z.object({
  tenantCustomerId: z.string(),
  asOf: z.string(),
  locationId: z.string().nullable(),
  availability: customerSourceAvailabilitySchema,
  visitSummary: z.object({ completedBookings: z.number().int().nonnegative(), completedWalkIns: z.number().int().nonnegative() }).nullable(),
  nextAppointment: z.object({ bookingId: z.string(), startAt: z.string(), serviceName: z.string(), status: z.enum(["Pending", "Confirmed"]) }).nullable(),
  recordedOnlinePaymentsByCurrency: z.array(z.object({ currency: z.string(), amount: z.number() })).nullable(),
});

export const customerActivityItemSchema = z.object({
  id: z.string(),
  kind: z.enum(["bookingCreated", "appointmentScheduled", "queueJoined", "queueCompleted", "queueNoShow", "onlinePaymentRecorded", "messageSent"]),
  occurredAt: z.string(),
  source: z.enum(["booking", "queue", "finance", "messaging"]),
  sourceId: z.string(),
  locationId: z.string().nullable(),
  serviceName: z.string().nullable(),
  currentStatus: z.string().nullable(),
  amount: z.number().nullable(),
  currency: z.string().nullable(),
});

export const customerActivitySchema = z.object({
  items: z.array(customerActivityItemSchema),
  nextCursor: z.string().nullable(),
  asOf: z.string(),
  from: z.string(),
  isTruncated: z.boolean(),
  availability: customerSourceAvailabilitySchema,
});

export type CustomerProfile = z.infer<typeof customerProfileSchema>;
export type CustomerActivity = z.infer<typeof customerActivitySchema>;
export type CustomerActivityItem = z.infer<typeof customerActivityItemSchema>;
