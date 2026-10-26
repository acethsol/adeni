import { z } from "zod";

export const customerSegmentIdSchema = z.enum(["newCustomer", "recentlyActive", "lapsed", "atRisk", "vip", "frequentQueueNoShow"]);
export const customerSegmentDefinitionSchema = z.object({ id: customerSegmentIdSchema, windowDays: z.number().int().positive(), minimumVisits: z.number().int().positive(), minimumRecencyDays: z.number().int().nullable() });
export const customerSegmentsSchema = z.object({
  tenantCustomerId: z.string().uuid(), availability: z.enum(["available", "partial", "notAvailable", "notEntitled", "erased"]),
  asOf: z.string().datetime({ offset: true }), definitionVersion: z.literal("v1"), locationId: z.string().uuid().nullable(),
  timeZoneId: z.string().nullable(), approximateVisitDates: z.boolean(),
  matches: z.array(z.object({ id: customerSegmentIdSchema, count: z.number().int().nonnegative(), daysSinceLastVisit: z.number().int().nonnegative().nullable() })),
});
export const customerSegmentsBatchRequestSchema = z.object({ customerIds: z.array(z.string().uuid()).min(1).max(100).refine(ids => new Set(ids).size === ids.length), locationId: z.string().uuid().optional() });
export type CustomerSegments = z.infer<typeof customerSegmentsSchema>;
export type CustomerSegmentId = z.infer<typeof customerSegmentIdSchema>;
