import { customerVerificationResponseSchema, type CustomerVerificationResponse } from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class MeVerificationApi {
  constructor(private readonly client: ApiClientContext) {}

  async getVerification(): Promise<CustomerVerificationResponse> {
    const response = await this.client.request("/api/v1/me/verification");
    return customerVerificationResponseSchema.parse(await response.json());
  }

  async refreshVerification(): Promise<CustomerVerificationResponse> {
    const response = await this.client.request("/api/v1/me/verification/refresh", { method: "POST" });
    return customerVerificationResponseSchema.parse(await response.json());
  }
}
