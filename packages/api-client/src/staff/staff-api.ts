import {
  mediaUploadUrlRequestSchema,
  mediaUploadUrlResponseSchema,
  staffMemberSchema,
  staffMembersResponseSchema,
  createStaffMemberRequestSchema,
  updateStaffMemberRequestSchema,
  replaceStaffServicesRequestSchema,
  staffLeaveSchema,
  staffLeaveListResponseSchema,
  createStaffLeaveRequestSchema,
  staffCalendarResponseSchema,
  staffTeamCalendarResponseSchema,
  createStaffInviteRequestSchema,
  staffPortalInviteSchema,
  staffPortalInvitesResponseSchema,
  tenantAccessUserSchema,
  tenantAccessUsersResponseSchema,
  updateAccessUserRequestSchema,
  updateStaffAvatarRequestSchema,
  acceptStaffInviteRequestSchema,
  acceptStaffInviteResponseSchema,
  weeklyAvailabilityResponseSchema,
  type StaffMember,
  type CreateStaffMemberRequest,
  type UpdateStaffMemberRequest,
  type ReplaceStaffServicesRequest,
  type StaffLeave,
  type CreateStaffLeaveRequest,
  type CreateStaffInviteRequest,
  type StaffPortalInvite,
  type TenantAccessUser,
  type UpdateAccessUserRequest,
  type UpdateStaffAvatarRequest,
  type AcceptStaffInviteRequest,
  type AcceptStaffInviteResponse,
  type StaffCalendarResponse,
  type StaffTeamCalendarResponse,
  type WeeklyAvailabilityRule,
  type MediaUploadUrlResponse,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class StaffApi {
  constructor(private readonly client: ApiClientContext) {}

  async createStaffAvatarUploadUrl(
    contentType: string,
    contentLength: number,
  ): Promise<MediaUploadUrlResponse> {
    const body = mediaUploadUrlRequestSchema.parse({
      purpose: "staff_avatar",
      contentType,
      contentLength,
    });
    const response = await this.client.request("/api/v1/tenant/media/upload-url", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return mediaUploadUrlResponseSchema.parse(await response.json());
  }

  async updateTenantStaffAvatar(
    staffMemberId: string,
    request: UpdateStaffAvatarRequest,
  ): Promise<StaffMember> {
    const body = updateStaffAvatarRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/avatar`,
      {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return staffMemberSchema.parse(await response.json());
  }

  async listTenantStaff(params?: { locationId?: string }): Promise<StaffMember[]> {
    const query = new URLSearchParams();
    if (params?.locationId) {
      query.set("locationId", params.locationId);
    }
    const qs = query.toString();
    const response = await this.client.request(`/api/v1/tenant/staff${qs ? `?${qs}` : ""}`);
    return staffMembersResponseSchema.parse(await response.json()).items;
  }

  async createTenantStaff(request: CreateStaffMemberRequest): Promise<StaffMember> {
    const body = createStaffMemberRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/staff", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return staffMemberSchema.parse(await response.json());
  }

  async updateTenantStaff(
    staffMemberId: string,
    request: UpdateStaffMemberRequest,
  ): Promise<StaffMember> {
    const body = updateStaffMemberRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return staffMemberSchema.parse(await response.json());
  }

  async deactivateTenantStaff(staffMemberId: string): Promise<void> {
    await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/deactivate`,
      { method: "POST" },
    );
  }

  async inviteTenantStaff(
    staffMemberId: string,
    request: CreateStaffInviteRequest,
  ): Promise<StaffPortalInvite> {
    const body = createStaffInviteRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/invite`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return staffPortalInviteSchema.parse(await response.json());
  }

  async inviteTenantAccess(request: CreateStaffInviteRequest): Promise<StaffPortalInvite> {
    const body = createStaffInviteRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/access/invites", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return staffPortalInviteSchema.parse(await response.json());
  }

  async listTenantAccessInvites(): Promise<StaffPortalInvite[]> {
    const response = await this.client.request("/api/v1/tenant/access/invites");
    return staffPortalInvitesResponseSchema.parse(await response.json()).items;
  }

  async resendTenantAccessInvite(inviteId: string): Promise<StaffPortalInvite> {
    const response = await this.client.request(
      `/api/v1/tenant/access/invites/${encodeURIComponent(inviteId)}/resend`,
      { method: "POST" },
    );
    return staffPortalInviteSchema.parse(await response.json());
  }

  async revokeTenantAccessInvite(inviteId: string): Promise<void> {
    await this.client.request(
      `/api/v1/tenant/access/invites/${encodeURIComponent(inviteId)}`,
      { method: "DELETE" },
    );
  }

  async listTenantAccessUsers(): Promise<TenantAccessUser[]> {
    const response = await this.client.request("/api/v1/tenant/access/users");
    return tenantAccessUsersResponseSchema.parse(await response.json()).items;
  }

  async updateTenantAccessUser(
    businessUserId: string,
    request: UpdateAccessUserRequest,
  ): Promise<TenantAccessUser> {
    const body = updateAccessUserRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/access/users/${encodeURIComponent(businessUserId)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return tenantAccessUserSchema.parse(await response.json());
  }

  async revokeTenantAccessUser(businessUserId: string): Promise<void> {
    await this.client.request(
      `/api/v1/tenant/access/users/${encodeURIComponent(businessUserId)}`,
      { method: "DELETE" },
    );
  }

  async acceptStaffInvite(request: AcceptStaffInviteRequest): Promise<AcceptStaffInviteResponse> {
    const body = acceptStaffInviteRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/auth/accept-staff-invite", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return acceptStaffInviteResponseSchema.parse(await response.json());
  }

  async replaceTenantStaffServices(
    staffMemberId: string,
    request: ReplaceStaffServicesRequest,
  ): Promise<StaffMember> {
    const body = replaceStaffServicesRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/services`,
      {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return staffMemberSchema.parse(await response.json());
  }

  async getTenantStaffHours(staffMemberId: string): Promise<WeeklyAvailabilityRule[]> {
    const response = await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/hours`,
    );
    return weeklyAvailabilityResponseSchema.parse(await response.json()).items;
  }

  async replaceTenantStaffHours(
    staffMemberId: string,
    items: WeeklyAvailabilityRule[],
  ): Promise<WeeklyAvailabilityRule[]> {
    const response = await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/hours`,
      {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ items }),
      },
    );
    return weeklyAvailabilityResponseSchema.parse(await response.json()).items;
  }

  async listTenantStaffLeave(
    staffMemberId: string,
    params?: { from?: string; to?: string },
  ): Promise<StaffLeave[]> {
    const query = new URLSearchParams();
    if (params?.from) query.set("from", params.from);
    if (params?.to) query.set("to", params.to);
    const qs = query.toString();
    const response = await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/leave${qs ? `?${qs}` : ""}`,
    );
    return staffLeaveListResponseSchema.parse(await response.json()).items;
  }

  async createTenantStaffLeave(
    staffMemberId: string,
    request: CreateStaffLeaveRequest,
  ): Promise<StaffLeave> {
    const body = createStaffLeaveRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/leave`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return staffLeaveSchema.parse(await response.json());
  }

  async deleteTenantStaffLeave(staffMemberId: string, leaveId: string): Promise<void> {
    await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/leave/${encodeURIComponent(leaveId)}`,
      { method: "DELETE" },
    );
  }

  async getTenantStaffCalendar(
    staffMemberId: string,
    params: { from: string; to: string; locationId?: string },
  ): Promise<StaffCalendarResponse> {
    const query = new URLSearchParams({ from: params.from, to: params.to });
    if (params.locationId) {
      query.set("locationId", params.locationId);
    }
    const response = await this.client.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/calendar?${query}`,
    );
    return staffCalendarResponseSchema.parse(await response.json());
  }

  async getTenantTeamCalendar(params: {
    from: string;
    to: string;
    locationId?: string;
  }): Promise<StaffTeamCalendarResponse> {
    const query = new URLSearchParams({ from: params.from, to: params.to });
    if (params.locationId) {
      query.set("locationId", params.locationId);
    }
    const response = await this.client.request(`/api/v1/tenant/staff/team-calendar?${query}`);
    return staffTeamCalendarResponseSchema.parse(await response.json());
  }
}
