import {
  tenantCustomerListResponseSchema,
  tenantCustomerDetailSchema,
  type TenantCustomerListResponse,
  type TenantCustomerDetail,
  appendListSortParams,
  type ListSortParams,
  customerProfileSchema,
  customerActivitySchema,
  type CustomerProfile,
  type CustomerActivity,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class CustomersApi {
  constructor(private readonly client: ApiClientContext) {}

  async getTenantCustomerProfile(id: string, locationId?: string, signal?: AbortSignal): Promise<CustomerProfile> {
    const query = locationId ? `?locationId=${encodeURIComponent(locationId)}` : "";
    const response = await this.client.request(`/api/v1/tenant/customers/${encodeURIComponent(id)}/profile${query}`, { signal });
    return customerProfileSchema.parse(await response.json());
  }

  async getTenantCustomerActivity(id: string, params?: { locationId?: string; pageSize?: number; cursor?: string }, signal?: AbortSignal): Promise<CustomerActivity> {
    const query = new URLSearchParams();
    if (params?.locationId) query.set("locationId", params.locationId);
    if (params?.pageSize !== undefined) query.set("pageSize", String(params.pageSize));
    if (params?.cursor) query.set("cursor", params.cursor);
    const suffix = query.size ? `?${query}` : "";
    const response = await this.client.request(`/api/v1/tenant/customers/${encodeURIComponent(id)}/activity${suffix}`, { signal });
    return customerActivitySchema.parse(await response.json());
  }

  async listTenantCustomers(params?: {
    search?: string;
    page?: number;
    pageSize?: number;
    discover?: "discover" | "walk_in";
    sort?: ListSortParams;
    locationId?: string;
  }): Promise<TenantCustomerListResponse> {
    const query = new URLSearchParams();
    if (params?.search) {
      query.set("search", params.search);
    }
    if (params?.locationId) {
      query.set("locationId", params.locationId);
    }
    if (params?.page) {
      query.set("page", String(params.page));
    }
    if (params?.pageSize) {
      query.set("pageSize", String(params.pageSize));
    }
    if (params?.discover) {
      query.set("discover", params.discover);
    }
    appendListSortParams(query, params?.sort);
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/tenant/customers${suffix}`);
    return tenantCustomerListResponseSchema.parse(await response.json());
  }

  async getTenantCustomer(
    tenantCustomerId: string,
    locationId?: string,
  ): Promise<TenantCustomerDetail> {
    const query = locationId ? `?locationId=${encodeURIComponent(locationId)}` : "";
    const response = await this.client.request(
      `/api/v1/tenant/customers/${encodeURIComponent(tenantCustomerId)}${query}`,
    );
    const json = (await response.json()) as Record<string, unknown>;
    return tenantCustomerDetailSchema.parse({
      ...json,
      recentBookingIds: json["recentBookingIds"] ?? [],
      recentQueueTicketIds: json["recentQueueTicketIds"] ?? [],
      recentBookings: json["recentBookings"] ?? [],
      recentQueueVisits: json["recentQueueVisits"] ?? [],
    });
  }
}
