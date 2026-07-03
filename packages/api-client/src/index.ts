import {
  authSessionSchema,
  availableSlotsResponseSchema,
  bookingResponseSchema,
  businessProfileSchema,
  categoriesResponseSchema,
  createBookingRequestSchema,
  createServiceOfferingRequestSchema,
  discoveryResponseSchema,
  pendingBusinessesResponseSchema,
  publicBusinessProfileSchema,
  serviceOfferingSchema,
  serviceOfferingsResponseSchema,
  submitVerificationRequestSchema,
  tenantBookingsResponseSchema,
  updateBusinessProfileRequestSchema,
  updateServiceOfferingRequestSchema,
  weeklyAvailabilityResponseSchema,
  type AuthSession,
  type AvailableSlot,
  type BookingResponse,
  type BusinessProfile,
  type Category,
  type CreateBookingRequest,
  type CreateServiceOfferingRequest,
  type DiscoveryResponse,
  type PendingBusiness,
  type PublicBusinessProfile,
  type ServiceOffering,
  type SubmitVerificationRequest,
  type UpdateBusinessProfileRequest,
  type UpdateServiceOfferingRequest,
  type WeeklyAvailabilityRule,
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
  /** Development only — maps to X-Dev-Auth0-Sub when Auth0 is disabled on the API. */
  devAuth0Sub?: string | null;
  fetchImpl?: typeof fetch;
};

export type DiscoverySearchParams = {
  lat: number;
  lng: number;
  category?: string | null;
  market?: string | null;
  page?: number;
  pageSize?: number;
};

export class AdeniApiClient {
  private accessToken: string | null;
  private tenantId: string | null;
  private devAuth0Sub: string | null;
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;

  constructor(options: AdeniApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.accessToken = options.accessToken ?? null;
    this.tenantId = options.tenantId ?? null;
    this.devAuth0Sub = options.devAuth0Sub ?? null;
    this.fetchImpl = options.fetchImpl ?? fetch;
  }

  setAccessToken(token: string | null) {
    this.accessToken = token;
  }

  setDevAuth0Sub(auth0Sub: string | null) {
    this.devAuth0Sub = auth0Sub;
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

    if (params.market) {
      query.set("market", params.market);
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

  async getBusinessServices(slug: string): Promise<ServiceOffering[]> {
    const response = await this.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/services`,
    );
    const payload = serviceOfferingsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getBusinessSlots(
    slug: string,
    params: { serviceId: string; from: string; to: string },
  ): Promise<AvailableSlot[]> {
    const query = new URLSearchParams({
      serviceId: params.serviceId,
      from: params.from,
      to: params.to,
    });
    const response = await this.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/slots?${query.toString()}`,
    );
    const payload = availableSlotsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async createBooking(request: CreateBookingRequest): Promise<BookingResponse> {
    const body = createBookingRequestSchema.parse(request);
    const response = await this.request("/api/v1/bookings", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return bookingResponseSchema.parse(await response.json());
  }

  async getTenantBookings(): Promise<BookingResponse[]> {
    const response = await this.request("/api/v1/tenant/bookings");
    const payload = tenantBookingsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async acceptTenantBooking(bookingId: string): Promise<BookingResponse> {
    const response = await this.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/accept`,
      { method: "POST" },
    );
    return bookingResponseSchema.parse(await response.json());
  }

  async rejectTenantBooking(
    bookingId: string,
    reason?: string,
  ): Promise<BookingResponse> {
    const response = await this.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/reject`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ reason: reason ?? null }),
      },
    );
    return bookingResponseSchema.parse(await response.json());
  }

  async getTenantProfile(): Promise<BusinessProfile> {
    const response = await this.request("/api/v1/tenant/profile");
    return businessProfileSchema.parse(await response.json());
  }

  async updateTenantProfile(
    request: UpdateBusinessProfileRequest,
  ): Promise<BusinessProfile> {
    const body = updateBusinessProfileRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/profile", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessProfileSchema.parse(await response.json());
  }

  async getTenantServices(): Promise<ServiceOffering[]> {
    const response = await this.request("/api/v1/tenant/services");
    const payload = serviceOfferingsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async createTenantService(
    request: CreateServiceOfferingRequest,
  ): Promise<ServiceOffering> {
    const body = createServiceOfferingRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/services", {
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
    const response = await this.request(
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
    await this.request(
      `/api/v1/tenant/services/${encodeURIComponent(serviceId)}`,
      { method: "DELETE" },
    );
  }

  async getTenantAvailability(): Promise<WeeklyAvailabilityRule[]> {
    const response = await this.request("/api/v1/tenant/availability");
    const payload = weeklyAvailabilityResponseSchema.parse(await response.json());
    return payload.items;
  }

  async replaceTenantAvailability(
    rules: WeeklyAvailabilityRule[],
  ): Promise<WeeklyAvailabilityRule[]> {
    const response = await this.request("/api/v1/tenant/availability", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ items: rules }),
    });
    const payload = weeklyAvailabilityResponseSchema.parse(await response.json());
    return payload.items;
  }

  async submitTenantVerification(
    request: SubmitVerificationRequest,
  ): Promise<void> {
    const body = submitVerificationRequestSchema.parse(request);
    await this.request("/api/v1/tenant/verification", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
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
    } else if (this.devAuth0Sub) {
      headers.set("X-Dev-Auth0-Sub", this.devAuth0Sub);
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
