import {
  verifiedPresenceSessionResponseSchema,
  verifyPresenceRequestSchema,
  type VerifiedPresenceSessionResponse,
  type VerifyPresenceRequest,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class PresenceApi {
  constructor(private readonly context: ApiClientContext) {}

  async getMyPresenceSession(): Promise<VerifiedPresenceSessionResponse | null> {
    const response = await this.context.request("/api/v1/tenant/presence/me");
    if (response.status === 204) {
      return null;
    }
    const text = await response.text();
    if (!text || text === "null") {
      return null;
    }
    return verifiedPresenceSessionResponseSchema.parse(JSON.parse(text));
  }

  async verifyPresence(body: VerifyPresenceRequest): Promise<VerifiedPresenceSessionResponse> {
    const payload = verifyPresenceRequestSchema.parse(body);
    const response = await this.context.request("/api/v1/tenant/presence/verify", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return verifiedPresenceSessionResponseSchema.parse(await response.json());
  }

  async invalidateMyPresence(): Promise<void> {
    await this.context.request("/api/v1/tenant/presence/invalidate", { method: "POST" });
  }
}
