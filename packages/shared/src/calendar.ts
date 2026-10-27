import { z } from "zod";

export const calendarProviderSchema = z.enum(["google", "microsoft"]);
export const calendarSelectionSchema = z.object({ id: z.string().min(1).max(2048), timeZoneId: z.string().max(128), isWriteTarget: z.boolean() });
export const calendarConnectionSchema = z.object({ id: z.string().uuid(), staffMemberId: z.string().uuid(), provider: calendarProviderSchema,
  status: z.string(), lastSyncedAt: z.string().nullable(), coveredUntil: z.string().nullable(), failureCategory: z.string().nullable(), selections: z.array(calendarSelectionSchema) });
export const externalCalendarSchema = z.object({ id: z.string().min(1).max(2048), name: z.string(), timeZoneId: z.string(), canWrite: z.boolean() });
export const calendarConflictSchema = z.object({ id: z.string().uuid(), staffMemberId: z.string().uuid(), bookingId: z.string().uuid(), detectedAt: z.string(), acknowledgedAt: z.string().nullable(), resolvedAt: z.string().nullable(), category: z.enum(["assigned_overlap", "unassigned_capacity"]), startAt: z.string(), endAt: z.string() });
export const bookingLinkSchema = z.object({ url: z.string().url(), html: z.string() });
export const bookingLinkDestinationSchema = z.object({ path: z.string().refine(p => /^\/businesses\/[a-z0-9-]+(?:\?[^#]*)?#book$/.test(p)), serviceId: z.string().uuid().nullable(), practitionerId: z.string().uuid().nullable() });
export type CalendarProvider = z.infer<typeof calendarProviderSchema>;
export type CalendarConnection = z.infer<typeof calendarConnectionSchema>;
export type CalendarSelection = z.infer<typeof calendarSelectionSchema>;
export type ExternalCalendar = z.infer<typeof externalCalendarSchema>;
export type CalendarConflict = z.infer<typeof calendarConflictSchema>;
export type BookingLink = z.infer<typeof bookingLinkSchema>;
export type BookingLinkDestination = z.infer<typeof bookingLinkDestinationSchema>;
export { parseBookingPreselection, bookingPreselectionQuery } from "./booking-preselection";
export type { BookingPreselection } from "./booking-preselection";

export const calendarConnectionsSchema = z.array(calendarConnectionSchema);
export const externalCalendarsSchema = z.array(externalCalendarSchema);
export const calendarSelectionsSchema = z.array(calendarSelectionSchema);
export const calendarConflictsSchema = z.array(calendarConflictSchema);
export const calendarActionSchema = z.boolean();
export const calendarAuthorizationSchema = z.object({ launchUrl: z.string().url() });
