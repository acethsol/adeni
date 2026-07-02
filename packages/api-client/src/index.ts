import {
  authSessionSchema,
  categoriesResponseSchema,
  discoveryResponseSchema,
  pendingBusinessesResponseSchema,
  publicBusinessProfileSchema,
  type AuthSession,
  type Category,
  type DiscoveryResponse,
  type PendingBusiness,
  type PublicBusinessProfile,
} from "@adeni/shared";

export class AdeniApiError extends Error {
  constructor(
    message: string,
    readonly statusCode?: number,
  ) {
    super(message);
    this.name = "AdeniApiError";
  }
}

export type AdeniApiClientOptions = {
  baseUrl: string;
  accessToken?: string | null;
  tenantId?: string | null;
  fetchImpl?: typeof fetch;
};

export type DiscoverySearchParams = {
  lat: number;
  lng: number;
  category?: string | null;
  page?: number;
  pageSize?: number;
};

export class AdeniApiClient {
  private accessToken: string | null;
  private tenantId: string | null;
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;

  constructor(options: AdeniApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.accessToken = options.accessToken ?? null;
    this.tenantId = options.tenantId ?? null;
    this.fetchImpl = options.fetchImpl ?? fetch;
  }

  setAccessToken(token: string | null) {
    this.accessToken = token;
  }

  setTenantId(tenantId: string | null) {
    this.tenantId = tenantId;
  }

  async getCategories(): Promise<Category[]> {
    const response = await this.request("/api/v1/categories");
    const payload = categoriesResponseSchema.parse(await response.json());
    return payload.items;
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

    const response = await this.request(`/api/v1/discovery?${query.toString()}`);
    return discoveryResponseSchema.parse(await response.json());
  }

  async getBusinessProfile(slug: string): Promise<PublicBusinessProfile> {
    const response = await this.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}`,
    );
    return publicBusinessProfileSchema.parse(await response.json());
  }

  async getMe(): Promise<AuthSession> {
    const response = await this.request("/api/v1/auth/me");
    return authSessionSchema.parse(await response.json());
  }

  async getPendingBusinesses(): Promise<PendingBusiness[]> {
    const response = await this.request("/api/v1/admin/businesses/pending");
    const payload = pendingBusinessesResponseSchema.parse(await response.json());
    return payload.items;
  }

  private async request(path: string, init: RequestInit = {}): Promise<Response> {
    const headers = new Headers(init.headers);
    headers.set("Accept", "application/json");

    if (this.accessToken) {
      headers.set("Authorization", `Bearer ${this.accessToken}`);
    }

    if (this.tenantId) {
      headers.set("X-Tenant-Id", this.tenantId);
    }

    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      ...init,
      headers,
    });

    if (!response.ok) {
      throw new AdeniApiError(`Request failed: ${path}`, response.status);
    }

    return response;
  }
}
