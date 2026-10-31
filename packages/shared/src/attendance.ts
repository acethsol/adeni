import { z } from "zod";

export const staffAttendanceEventTypeSchema = z.enum([
  "clock_in",
  "break_start",
  "break_end",
  "clock_out",
]);

export const staffAttendanceWorkStateSchema = z.enum(["not_working", "working", "on_break"]);

export type StaffAttendanceWorkState = z.infer<typeof staffAttendanceWorkStateSchema>;

export const staffAttendanceEventResponseSchema = z.object({
  id: z.string().uuid(),
  eventType: z.string(),
  occurredAt: z.string(),
  businessLocationId: z.string().uuid(),
  locationVerificationStatus: z.string(),
  verificationMethod: z.string(),
});

export const staffAttendanceScheduleWindowSchema = z.object({
  openTime: z.string(),
  closeTime: z.string(),
});

export const staffAttendanceLocationFixSchema = z.object({
  latitude: z.number().min(-90).max(90),
  longitude: z.number().min(-180).max(180),
  accuracyMeters: z.number().int().min(0).max(100),
  capturedAt: z.string(),
});

export const staffAttendanceTodayResponseSchema = z.object({
  staffMemberId: z.string().uuid(),
  businessLocationId: z.string().uuid(),
  workState: staffAttendanceWorkStateSchema,
  eventsToday: z.array(staffAttendanceEventResponseSchema),
  scheduledWindows: z.array(staffAttendanceScheduleWindowSchema),
  attendanceGeofenceEnabled: z.boolean(),
  attendanceGeofenceRequired: z.boolean(),
  locationHasCoordinates: z.boolean(),
  attendanceGeofenceRadiusMeters: z.number().int(),
  locationName: z.string().nullable().optional(),
});

export const recordStaffAttendanceEventRequestSchema = z.object({
  eventType: staffAttendanceEventTypeSchema,
  businessLocationId: z.string().uuid(),
  locationFix: staffAttendanceLocationFixSchema.optional(),
});

export const managerStaffAttendanceOverrideRequestSchema = z.object({
  eventType: staffAttendanceEventTypeSchema,
  businessLocationId: z.string().uuid(),
  reason: z.string().min(10).max(500),
});

export type StaffAttendanceTodayResponse = z.infer<typeof staffAttendanceTodayResponseSchema>;
export type RecordStaffAttendanceEventRequest = z.infer<typeof recordStaffAttendanceEventRequestSchema>;
export type ManagerStaffAttendanceOverrideRequest = z.infer<typeof managerStaffAttendanceOverrideRequestSchema>;

export const staffAttendanceTeamMemberSchema = z.object({
  staffMemberId: z.string().uuid(),
  displayName: z.string(),
  workState: staffAttendanceWorkStateSchema,
  lastEventAt: z.string().nullable().optional(),
  lastEventType: z.string().nullable().optional(),
});

export const staffAttendanceTeamResponseSchema = z.object({
  date: z.string(),
  businessLocationId: z.string().uuid().nullable().optional(),
  members: z.array(staffAttendanceTeamMemberSchema),
});

export type StaffAttendanceTeamResponse = z.infer<typeof staffAttendanceTeamResponseSchema>;
export type StaffAttendanceTeamMember = z.infer<typeof staffAttendanceTeamMemberSchema>;

export const staffAttendanceScheduleTodayResponseSchema = z.object({
  staffMemberId: z.string().uuid(),
  date: z.string(),
  scheduledWindows: z.array(staffAttendanceScheduleWindowSchema),
  locationName: z.string().nullable().optional(),
});

export const staffAttendanceHistoryResponseSchema = z.object({
  staffMemberId: z.string().uuid(),
  displayName: z.string(),
  from: z.string(),
  to: z.string(),
  events: z.array(staffAttendanceEventResponseSchema),
});

export const appendStaffAttendanceCorrectionRequestSchema = z.object({
  relatedEventId: z.string().uuid(),
  reason: z.string().min(10).max(500),
});

export type StaffAttendanceScheduleTodayResponse = z.infer<
  typeof staffAttendanceScheduleTodayResponseSchema
>;
export type StaffAttendanceHistoryResponse = z.infer<typeof staffAttendanceHistoryResponseSchema>;
export type AppendStaffAttendanceCorrectionRequest = z.infer<
  typeof appendStaffAttendanceCorrectionRequestSchema
>;

export const verifiedPresenceSessionResponseSchema = z.object({
  id: z.string().uuid(),
  businessLocationId: z.string().uuid(),
  verificationMethod: z.string(),
  verifiedAt: z.string(),
  expiresAt: z.string(),
  distanceFromLocationMeters: z.number().int().nullable().optional(),
  reportedAccuracyMeters: z.number().int().nullable().optional(),
});

export const verifyPresenceRequestSchema = z.object({
  businessLocationId: z.string().uuid(),
  locationFix: staffAttendanceLocationFixSchema.optional(),
});

export type VerifiedPresenceSessionResponse = z.infer<typeof verifiedPresenceSessionResponseSchema>;
export type VerifyPresenceRequest = z.infer<typeof verifyPresenceRequestSchema>;
