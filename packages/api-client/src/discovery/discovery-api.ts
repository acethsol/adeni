import {
  availableSlotsResponseSchema,
  categoriesResponseSchema,
  discoveryResponseSchema,
  marketsResponseSchema,
  mapApiMarketToConfig,
  publicBusinessProfileSchema,
  serviceOfferingsResponseSchema,
  serviceTemplatesResponseSchema,
  type ServiceTemplate,
  createQuoteRequestSchema,
  quoteRequestResponseSchema,
  publicStaffMembersResponseSchema,
  publicReviewsResponseSchema,
  type AvailableSlot,
  type Category,
  type DiscoveryResponse,
  type MarketConfig,
  type PublicBusinessProfile,
  type PublicStaffMember,
  type ServiceOfferingsResponse,
  type CreateQuoteRequest,
  type QuoteRequestResponse,
  type PublicReviewsResponse,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";
export type DiscoverySearchParams = {
  lat: number;
  lng: number;
  category?: string | null;
  market?: string | null;
  q?: string | null;
  page?: number;
  pageSize?: number;
  sort?: "distance" | "featured";
  minRating?: number | null;
};

export class DiscoveryApi {
  constructor(private readonly client: ApiClientContext) {}

  async getCategories(options?: {
    market?: string;
    wellness?: boolean;
    includeDisabled?: boolean;
  }): Promise<Category[]> {
    const query = new URLSearchParams();
    if (options?.market) {
      query.set("market", options.market);
    }
    if (options?.wellness === false) {
      query.set("wellness", "false");
    }
    if (options?.includeDisabled) {
      query.set("includeDisabled", "true");
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/categories${suffix}`);
    const payload = categoriesResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getCategoryServiceTemplates(categorySlug: string): Promise<ServiceTemplate[]> {
    const response = await this.client.request(
      `/api/v1/categories/${encodeURIComponent(categorySlug)}/service-templates`,
    );
    const payload = serviceTemplatesResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getMarkets(): Promise<MarketConfig[]> {
    const response = await this.client.request("/api/v1/markets");
    const payload = marketsResponseSchema.parse(await response.json());
    return payload.items.map(mapApiMarketToConfig);
  }

  async searchDiscovery(params: DiscoverySearchParams): Promise<DiscoveryResponse> {
    const query = new URLSearchParams({
      lat: String(params.lat),
      lng: String(params.lng),
      page: String(params.page ?? 1),
      pageSize: String(params.pageSize ?? 20),
    });

    if (params.category) {
      query.set("category", params.category);
    }

    if (params.market) {
      query.set("market", params.market);
    }

    if (params.q) {
      query.set("q", params.q);
    }

    if (params.sort) {
      query.set("sort", params.sort);
    }

    if (params.minRating) {
      query.set("minRating", String(params.minRating));
    }

    const response = await this.client.request(`/api/v1/discovery?${query.toString()}`);
    return discoveryResponseSchema.parse(await response.json());
  }

  async getBusinessProfile(slug: string): Promise<PublicBusinessProfile> {
    const response = await this.client.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}`,
    );
    return publicBusinessProfileSchema.parse(await response.json());
  }

  async getBusinessServiceCatalog(slug: string): Promise<ServiceOfferingsResponse> {
    const response = await this.client.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/services`,
    );
    return serviceOfferingsResponseSchema.parse(await response.json());
  }

  async getBusinessStaff(
    slug: string,
    params?: { serviceId?: string },
  ): Promise<PublicStaffMember[]> {
    const query = new URLSearchParams();
    if (params?.serviceId) {
      query.set("serviceId", params.serviceId);
    }
    const qs = query.toString();
    const response = await this.client.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/staff${qs ? `?${qs}` : ""}`,
    );
    return publicStaffMembersResponseSchema.parse(await response.json()).items;
  }

  async getBusinessSlots(
    slug: string,
    params: {
      serviceId: string;
      from: string;
      to: string;
      staffMemberId?: string | null;
      serviceIds?: string[];
      guestCount?: number;
    },
  ): Promise<AvailableSlot[]> {
    const query = new URLSearchParams({
      serviceId: params.serviceId,
      from: params.from,
      to: params.to,
    });
    if (params.staffMemberId) {
      query.set("staffMemberId", params.staffMemberId);
    }
    if (params.serviceIds?.length) {
      query.set("serviceIds", params.serviceIds.join(","));
    }
    if (params.guestCount && params.guestCount > 1) {
      query.set("guestCount", String(params.guestCount));
    }
    const response = await this.client.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/slots?${query.toString()}`,
    );
    const payload = availableSlotsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getBusinessReviews(
    slug: string,
    page = 1,
    pageSize = 10,
  ): Promise<PublicReviewsResponse> {
    const query = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    });
    const response = await this.client.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/reviews?${query.toString()}`,
    );
    return publicReviewsResponseSchema.parse(await response.json());
  }

  async createQuoteRequest(
    slug: string,
    request: CreateQuoteRequest,
  ): Promise<QuoteRequestResponse> {
    const body = createQuoteRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/quote-requests`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return quoteRequestResponseSchema.parse(await response.json());
  }
}
