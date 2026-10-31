import {
  appendStaffAttendanceCorrectionRequestSchema,
  managerStaffAttendanceOverrideRequestSchema,
  recordStaffAttendanceEventRequestSchema,
  staffAttendanceEventResponseSchema,
  staffAttendanceHistoryResponseSchema,
  staffAttendanceScheduleTodayResponseSchema,
  staffAttendanceTeamResponseSchema,
  staffAttendanceTodayResponseSchema,
  type AppendStaffAttendanceCorrectionRequest,
  type ManagerStaffAttendanceOverrideRequest,
  type RecordStaffAttendanceEventRequest,
  type StaffAttendanceHistoryResponse,
  type StaffAttendanceScheduleTodayResponse,
  type StaffAttendanceTeamResponse,
  type StaffAttendanceTodayResponse,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class AttendanceApi {
  constructor(private readonly context: ApiClientContext) {}

  async getMyAttendanceToday(): Promise<StaffAttendanceTodayResponse> {
    const response = await this.context.request("/api/v1/tenant/attendance/me/today");
    return staffAttendanceTodayResponseSchema.parse(await response.json());
  }

  async getMyScheduleToday(): Promise<StaffAttendanceScheduleTodayResponse> {
    const response = await this.context.request("/api/v1/tenant/attendance/me/schedule-today");
    return staffAttendanceScheduleTodayResponseSchema.parse(await response.json());
  }

  async getStaffAttendanceHistory(
    staffMemberId: string,
    params?: { from?: string; to?: string },
  ): Promise<StaffAttendanceHistoryResponse> {
    const search = new URLSearchParams();
    if (params?.from) {
      search.set("from", params.from);
    }
    if (params?.to) {
      search.set("to", params.to);
    }
    const qs = search.toString();
    const path = qs
      ? `/api/v1/tenant/attendance/staff/${encodeURIComponent(staffMemberId)}?${qs}`
      : `/api/v1/tenant/attendance/staff/${encodeURIComponent(staffMemberId)}`;
    const response = await this.context.request(path);
    return staffAttendanceHistoryResponseSchema.parse(await response.json());
  }

  async appendStaffAttendanceCorrection(staffMemberId: string, body: AppendStaffAttendanceCorrectionRequest) {
    const payload = appendStaffAttendanceCorrectionRequestSchema.parse(body);
    const response = await this.context.request(
      `/api/v1/tenant/attendance/staff/${encodeURIComponent(staffMemberId)}/corrections`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );
    return staffAttendanceEventResponseSchema.parse(await response.json());
  }

  async recordMyAttendanceEvent(body: RecordStaffAttendanceEventRequest) {
    const payload = recordStaffAttendanceEventRequestSchema.parse(body);
    const response = await this.context.request("/api/v1/tenant/attendance/me/events", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return staffAttendanceEventResponseSchema.parse(await response.json());
  }

  async getTeamAttendance(params?: { date?: string; locationId?: string }): Promise<StaffAttendanceTeamResponse> {
    const search = new URLSearchParams();
    if (params?.date) {
      search.set("date", params.date);
    }
    if (params?.locationId) {
      search.set("locationId", params.locationId);
    }
    const qs = search.toString();
    const path = qs ? `/api/v1/tenant/attendance/team?${qs}` : "/api/v1/tenant/attendance/team";
    const response = await this.context.request(path);
    return staffAttendanceTeamResponseSchema.parse(await response.json());
  }

  async recordManagerAttendanceOverride(staffMemberId: string, body: ManagerStaffAttendanceOverrideRequest) {
    const payload = managerStaffAttendanceOverrideRequestSchema.parse(body);
    const response = await this.context.request(
      `/api/v1/tenant/attendance/staff/${encodeURIComponent(staffMemberId)}/override`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );
    return staffAttendanceEventResponseSchema.parse(await response.json());
  }
}
