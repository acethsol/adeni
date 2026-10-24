import {
  createServiceOfferingRequestSchema,
  serviceOfferingSchema,
  serviceOfferingsResponseSchema,
  createServiceMenuGroupRequestSchema,
  updateServiceMenuGroupRequestSchema,
  serviceMenuGroupsResponseSchema,
  serviceMenuGroupSchema,
  updateServiceOfferingRequestSchema,
  type CreateServiceOfferingRequest,
  type ServiceOffering,
  type ServiceMenuGroup,
  type CreateServiceMenuGroupRequest,
  type UpdateServiceMenuGroupRequest,
  type ServiceOfferingsResponse,
  type UpdateServiceOfferingRequest,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class CatalogApi {
  constructor(private readonly client: ApiClientContext) {}

  async getTenantServiceCatalog(): Promise<ServiceOfferingsResponse> {
    const response = await this.client.request("/api/v1/tenant/services");
    return serviceOfferingsResponseSchema.parse(await response.json());
  }

  async listTenantServiceMenuGroups(): Promise<ServiceMenuGroup[]> {
    const response = await this.client.request("/api/v1/tenant/service-menu-groups");
    return serviceMenuGroupsResponseSchema.parse(await response.json()).items;
  }

  async createTenantServiceMenuGroup(
    request: CreateServiceMenuGroupRequest,
  ): Promise<ServiceMenuGroup> {
    const body = createServiceMenuGroupRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/service-menu-groups", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return serviceMenuGroupSchema.parse(await response.json());
  }

  async updateTenantServiceMenuGroup(
    groupId: string,
    request: UpdateServiceMenuGroupRequest,
  ): Promise<ServiceMenuGroup> {
    const body = updateServiceMenuGroupRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/service-menu-groups/${encodeURIComponent(groupId)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return serviceMenuGroupSchema.parse(await response.json());
  }

  async deleteTenantServiceMenuGroup(groupId: string): Promise<void> {
    await this.client.request(
      `/api/v1/tenant/service-menu-groups/${encodeURIComponent(groupId)}`,
      { method: "DELETE" },
    );
  }

  async createTenantService(
    request: CreateServiceOfferingRequest,
  ): Promise<ServiceOffering> {
    const body = createServiceOfferingRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/services", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return serviceOfferingSchema.parse(await response.json());
  }

  async updateTenantService(
    serviceId: string,
    request: UpdateServiceOfferingRequest,
  ): Promise<ServiceOffering> {
    const body = updateServiceOfferingRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/services/${encodeURIComponent(serviceId)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return serviceOfferingSchema.parse(await response.json());
  }

  async deactivateTenantService(serviceId: string): Promise<void> {
    await this.client.request(
      `/api/v1/tenant/services/${encodeURIComponent(serviceId)}`,
      { method: "DELETE" },
    );
  }
}
