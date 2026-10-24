import {
  publicCountryPricingSchema,
  subscriptionUsageSchema,
  tenantEntitlementsSchema,
  type TenantEntitlements,
  subscriptionCheckoutRequestSchema,
  subscriptionCheckoutResponseSchema,
  completeStubSubscriptionCheckoutRequestSchema,
  tenantSubscriptionChargeSchema,
  type PublicCountryPricing,
  type SubscriptionUsage,
  type SubscriptionCheckoutRequest,
  type SubscriptionCheckoutResponse,
  type TenantSubscriptionCharge,
  type CompleteStubSubscriptionCheckoutRequest,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class SubscriptionsApi {
  constructor(private readonly client: ApiClientContext) {}

  async getTenantEntitlements(): Promise<TenantEntitlements> {
    const response = await this.client.request("/api/v1/tenant/entitlements");
    return tenantEntitlementsSchema.parse(await response.json());
  }

  async getTenantSubscriptionUsage(locationId?: string): Promise<SubscriptionUsage> {
    const query = new URLSearchParams();
    if (locationId) {
      query.set("locationId", locationId);
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/tenant/subscription${suffix}`);
    return subscriptionUsageSchema.parse(await response.json());
  }

  async createSubscriptionCheckout(
    body: SubscriptionCheckoutRequest,
  ): Promise<SubscriptionCheckoutResponse> {
    const payload = subscriptionCheckoutRequestSchema.parse(body);
    const response = await this.client.request("/api/v1/subscriptions/checkout", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return subscriptionCheckoutResponseSchema.parse(await response.json());
  }

  async completeStubSubscriptionCheckout(
    body: CompleteStubSubscriptionCheckoutRequest,
  ): Promise<TenantSubscriptionCharge> {
    const payload = completeStubSubscriptionCheckoutRequestSchema.parse(body);
    const response = await this.client.request("/api/v1/subscriptions/checkout/complete", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return tenantSubscriptionChargeSchema.parse(await response.json());
  }

  async getPublicPricing(countryCode: string): Promise<PublicCountryPricing> {
    const query = new URLSearchParams({ countryCode });
    const response = await this.client.request(`/api/v1/pricing?${query.toString()}`);
    return publicCountryPricingSchema.parse(await response.json());
  }
}
