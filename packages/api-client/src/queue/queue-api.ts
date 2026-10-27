import {
  joinQueueRequestSchema,
  queueTicketResponseSchema,
  tenantQueueFloorResponseSchema,
  walkInSettingsSchema,
  updateWalkInSettingsRequestSchema,
  openWalkInBlockRequestSchema,
  staffAddQueueRequestSchema,
  kioskStatusResponseSchema,
  setKioskPinRequestSchema,
  verifyKioskPinRequestSchema,
  verifyKioskPinResponseSchema,
  type JoinQueueRequest,
  type QueueTicketResponse,
  type TenantQueueFloorResponse,
  type WalkInSettings,
  type UpdateWalkInSettingsRequest,
  type StaffAddQueueRequest,
  type KioskStatusResponse,
  type VerifyKioskPinResponse,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class QueueApi {
  constructor(private readonly client: ApiClientContext) {}

  async joinPublicQueue(
    slug: string,
    request: JoinQueueRequest,
  ): Promise<QueueTicketResponse> {
    const body = joinQueueRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/public/queue/${encodeURIComponent(slug)}/join`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async getPublicQueueTicket(token: string): Promise<QueueTicketResponse> {
    const response = await this.client.request(
      `/api/v1/public/queue/tickets/${encodeURIComponent(token)}`,
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async getTenantQueueFloor(locationId?: string): Promise<TenantQueueFloorResponse> {
    const query = locationId ? `?locationId=${encodeURIComponent(locationId)}` : "";
    const response = await this.client.request(`/api/v1/tenant/queue${query}`);
    return tenantQueueFloorResponseSchema.parse(await response.json());
  }

  async updateWalkInSettings(
    request: UpdateWalkInSettingsRequest,
  ): Promise<WalkInSettings> {
    const body = updateWalkInSettingsRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/queue/walk-in-settings", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return walkInSettingsSchema.parse(await response.json());
  }

  async openWalkInBlock(durationMinutes: number): Promise<WalkInSettings> {
    const body = openWalkInBlockRequestSchema.parse({ durationMinutes });
    const response = await this.client.request("/api/v1/tenant/queue/walk-in/open", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return walkInSettingsSchema.parse(await response.json());
  }

  async addTenantQueueTicket(
    request: StaffAddQueueRequest,
  ): Promise<QueueTicketResponse> {
    const body = staffAddQueueRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/queue", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return queueTicketResponseSchema.parse(await response.json());
  }

  async callTenantQueueTicket(ticketId: string): Promise<QueueTicketResponse> {
    const response = await this.client.request(
      `/api/v1/tenant/queue/${encodeURIComponent(ticketId)}/call`,
      { method: "POST" },
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async startTenantQueueTicket(
    ticketId: string,
    staffMemberId?: string,
  ): Promise<QueueTicketResponse> {
    const response = await this.client.request(
      `/api/v1/tenant/queue/${encodeURIComponent(ticketId)}/start`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ staffMemberId: staffMemberId ?? null }),
      },
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async completeTenantQueueTicket(ticketId: string): Promise<QueueTicketResponse> {
    const response = await this.client.request(
      `/api/v1/tenant/queue/${encodeURIComponent(ticketId)}/complete`,
      { method: "POST" },
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async noShowTenantQueueTicket(ticketId: string): Promise<QueueTicketResponse> {
    const response = await this.client.request(
      `/api/v1/tenant/queue/${encodeURIComponent(ticketId)}/no-show`,
      { method: "POST" },
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async cancelTenantQueueTicket(ticketId: string): Promise<QueueTicketResponse> {
    const response = await this.client.request(
      `/api/v1/tenant/queue/${encodeURIComponent(ticketId)}/cancel`,
      { method: "POST" },
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async getTenantKioskStatus(): Promise<KioskStatusResponse> {
    const response = await this.client.request("/api/v1/tenant/kiosk/status");
    return kioskStatusResponseSchema.parse(await response.json());
  }

  async setTenantKioskPin(pin: string | null): Promise<KioskStatusResponse> {
    const body = setKioskPinRequestSchema.parse({ pin });
    const response = await this.client.request("/api/v1/tenant/kiosk/pin", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return kioskStatusResponseSchema.parse(await response.json());
  }

  async verifyTenantKioskPin(pin: string): Promise<VerifyKioskPinResponse> {
    const body = verifyKioskPinRequestSchema.parse({ pin });
    const response = await this.client.request("/api/v1/tenant/kiosk/verify-pin", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return verifyKioskPinResponseSchema.parse(await response.json());
  }
}
