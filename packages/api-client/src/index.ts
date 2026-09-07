import {
  authSessionSchema,
  adminCustomersResponseSchema,
  adminMarketsResponseSchema,
  adminMarketSchema,
  adminBusinessesResponseSchema,
  setSubscriptionTierRequestSchema,
  subscriptionUsageSchema,
  availableSlotsResponseSchema,
  bookingResponseSchema,
  businessContextResponseSchema,
  businessLocationSchema,
  businessProfileSchema,
  categoriesResponseSchema,
  createBookingRequestSchema,
  createMarketRequestSchema,
  createServiceOfferingRequestSchema,
  customerBookingsResponseSchema,
  customerBookingResponseSchema,
  customerDataExportSchema,
  discoveryResponseSchema,
  marketsResponseSchema,
  mapApiMarketToConfig,
  pendingBusinessesResponseSchema,
  publicBusinessProfileSchema,
  registerBusinessRequestSchema,
  registerBusinessResponseSchema,
  rejectBusinessRequestSchema,
  setMarketLiveRequestSchema,
  serviceOfferingSchema,
  serviceOfferingsResponseSchema,
  submitVerificationRequestSchema,
  tenantBookingsResponseSchema,
  tenantLocationsResponseSchema,
  updateBusinessProfileRequestSchema,
  upsertBusinessLocationRequestSchema,
  mediaUploadUrlRequestSchema,
  mediaUploadUrlResponseSchema,
  updateCoverImageRequestSchema,
  updateCoverImageResponseSchema,
  createReviewRequestSchema,
  createQuoteRequestSchema,
  quoteRequestResponseSchema,
  joinWaitlistRequestSchema,
  initializePaymentRequestSchema,
  paymentIntentResponseSchema,
  createPaymentLinkRequestSchema,
  paymentLedgerResponseSchema,
  refundPaymentRequestSchema,
  updateBusinessSettingsRequestSchema,
  reviewResponseSchema,
  publicReviewsResponseSchema,
  updateMarketRequestSchema,
  updateServiceOfferingRequestSchema,
  weeklyAvailabilityResponseSchema,
  type AuthSession,
  type AdminCustomerSummary,
  type AdminMarket,
  type AdminBusinessSummary,
  type SubscriptionUsage,
  type AvailableSlot,
  type BookingResponse,
  type BusinessContextResponse,
  type BusinessProfile,
  type BusinessLocation,
  type Category,
  type CreateBookingRequest,
  type CreateServiceOfferingRequest,
  type CustomerBookingResponse,
  type CustomerDataExport,
  type DiscoveryResponse,
  type MarketConfig,
  type PendingBusiness,
  type PublicBusinessProfile,
  type RegisterBusinessRequest,
  type RegisterBusinessResponse,
  type ServiceOffering,
  type SubmitVerificationRequest,
  type UpdateBusinessProfileRequest,
  type UpdateBusinessSettingsRequest,
  type CreateQuoteRequest,
  type QuoteRequestResponse,
  type JoinWaitlistRequest,
  type InitializePaymentRequest,
  type PaymentIntentResponse,
  type CreatePaymentLinkRequest,
  type PaymentLedgerEntry,
  type RefundPaymentRequest,
  type CreateReviewRequest,
  type ReviewResponse,
  type PublicReviewsResponse,
  type UpdateCoverImageRequest,
  type MediaUploadUrlResponse,
  type UpdateServiceOfferingRequest,
  type UpsertBusinessLocationRequest,
  type WeeklyAvailabilityRule,
  localizeErrorResponse,
  type ApiErrorResponse,
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
  /** Locale for translating API error codes (defaults to en). */
  locale?: string;
  fetchImpl?: typeof fetch;
};

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

export class AdeniApiClient {
  private accessToken: string | null;
  private tenantId: string | null;
  private devAuth0Sub: string | null;
  private readonly locale: string;
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;

  constructor(options: AdeniApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.accessToken = options.accessToken ?? null;
    this.tenantId = options.tenantId ?? null;
    this.devAuth0Sub = options.devAuth0Sub ?? null;
    this.locale = options.locale ?? "en";
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

  async getMarkets(): Promise<MarketConfig[]> {
    const response = await this.request("/api/v1/markets");
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

  async createBooking(
    request: CreateBookingRequest,
    idempotencyKey?: string,
  ): Promise<BookingResponse> {
    const body = createBookingRequestSchema.parse(request);
    const response = await this.request("/api/v1/bookings", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      idempotencyKey,
    });
    return bookingResponseSchema.parse(await response.json());
  }

  async getMyBookings(): Promise<CustomerBookingResponse[]> {
    const response = await this.request("/api/v1/bookings");
    const payload = customerBookingsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async cancelMyBooking(bookingId: string): Promise<CustomerBookingResponse> {
    const response = await this.request(
      `/api/v1/bookings/${encodeURIComponent(bookingId)}/cancel`,
      { method: "POST" },
    );
    return customerBookingResponseSchema.parse(await response.json());
  }

  async createBookingReview(
    bookingId: string,
    request: CreateReviewRequest,
  ): Promise<ReviewResponse> {
    const body = createReviewRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/bookings/${encodeURIComponent(bookingId)}/review`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return reviewResponseSchema.parse(await response.json());
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
    const response = await this.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/reviews?${query.toString()}`,
    );
    return publicReviewsResponseSchema.parse(await response.json());
  }

  async hideAdminReview(reviewId: string): Promise<void> {
    await this.request(`/api/v1/admin/reviews/${encodeURIComponent(reviewId)}`, {
      method: "DELETE",
    });
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

  async getBusinessContext(): Promise<BusinessContextResponse> {
    const response = await this.request("/api/v1/tenant/context");
    return businessContextResponseSchema.parse(await response.json());
  }

  async registerBusiness(
    request: RegisterBusinessRequest,
  ): Promise<RegisterBusinessResponse> {
    const body = registerBusinessRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return registerBusinessResponseSchema.parse(await response.json());
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

  async updateTenantSettings(
    request: UpdateBusinessSettingsRequest,
  ): Promise<BusinessProfile> {
    const body = updateBusinessSettingsRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/settings", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessProfileSchema.parse(await response.json());
  }

  async createQuoteRequest(
    slug: string,
    request: CreateQuoteRequest,
  ): Promise<QuoteRequestResponse> {
    const body = createQuoteRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/quote-requests`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return quoteRequestResponseSchema.parse(await response.json());
  }

  async joinWaitlist(request: JoinWaitlistRequest): Promise<{ id: string }> {
    const body = joinWaitlistRequestSchema.parse(request);
    const response = await this.request("/api/v1/bookings/waitlist", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return (await response.json()) as { id: string };
  }

  async initializePayment(
    request: InitializePaymentRequest,
    idempotencyKey?: string,
  ): Promise<PaymentIntentResponse> {
    const body = initializePaymentRequestSchema.parse(request);
    const response = await this.request("/api/v1/payments/initialize", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      idempotencyKey,
    });
    return paymentIntentResponseSchema.parse(await response.json());
  }

  async getPayment(id: string): Promise<PaymentIntentResponse> {
    const response = await this.request(`/api/v1/payments/${id}`);
    return paymentIntentResponseSchema.parse(await response.json());
  }

  async createPaymentLink(
    request: CreatePaymentLinkRequest,
  ): Promise<PaymentIntentResponse> {
    const body = createPaymentLinkRequestSchema.parse(request);
    const response = await this.request("/api/v1/payments/links", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return paymentIntentResponseSchema.parse(await response.json());
  }

  async listPaymentLedger(
    tenantId: string,
    page = 1,
    pageSize = 50,
  ): Promise<PaymentLedgerEntry[]> {
    const query = new URLSearchParams({
      tenantId,
      page: String(page),
      pageSize: String(pageSize),
    });
    const response = await this.request(`/api/v1/payments/ledger?${query.toString()}`);
    const payload = paymentLedgerResponseSchema.parse(await response.json());
    return payload.items;
  }

  async refundPayment(
    paymentIntentId: string,
    request: RefundPaymentRequest,
  ): Promise<PaymentIntentResponse> {
    const body = refundPaymentRequestSchema.parse(request);
    const response = await this.request(`/api/v1/payments/${paymentIntentId}/refund`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return paymentIntentResponseSchema.parse(await response.json());
  }

  async confirmStubPayment(reference: string): Promise<PaymentIntentResponse> {
    const response = await this.request("/api/v1/payments/stub/confirm", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ reference }),
    });
    return paymentIntentResponseSchema.parse(await response.json());
  }

  async createCoverUploadUrl(
    contentType: string,
    contentLength: number,
  ): Promise<MediaUploadUrlResponse> {
    const body = mediaUploadUrlRequestSchema.parse({
      purpose: "cover",
      contentType,
      contentLength,
    });
    const response = await this.request("/api/v1/tenant/media/upload-url", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return mediaUploadUrlResponseSchema.parse(await response.json());
  }

  async updateTenantCoverImage(request: UpdateCoverImageRequest): Promise<string> {
    const body = updateCoverImageRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/profile/cover", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const payload = updateCoverImageResponseSchema.parse(await response.json());
    return payload.coverImageUrl;
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

  async getTenantLocations(): Promise<BusinessLocation[]> {
    const response = await this.request("/api/v1/tenant/locations");
    const payload = tenantLocationsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async addTenantLocation(
    request: UpsertBusinessLocationRequest,
  ): Promise<BusinessLocation> {
    const body = upsertBusinessLocationRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/locations", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessLocationSchema.parse(await response.json());
  }

  async updateTenantLocation(
    locationId: string,
    request: UpsertBusinessLocationRequest,
  ): Promise<BusinessLocation> {
    const body = upsertBusinessLocationRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/tenant/locations/${encodeURIComponent(locationId)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return businessLocationSchema.parse(await response.json());
  }

  async deactivateTenantLocation(locationId: string): Promise<void> {
    await this.request(
      `/api/v1/tenant/locations/${encodeURIComponent(locationId)}`,
      { method: "DELETE" },
    );
  }

  async getTenantSubscriptionUsage(): Promise<SubscriptionUsage> {
    const response = await this.request("/api/v1/tenant/subscription");
    return subscriptionUsageSchema.parse(await response.json());
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

  async approvePendingBusiness(tenantId: string): Promise<void> {
    await this.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/approve`,
      { method: "POST" },
    );
  }

  async rejectPendingBusiness(tenantId: string, reason: string): Promise<void> {
    const body = rejectBusinessRequestSchema.parse({ reason });
    await this.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/reject`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
  }

  async getAdminBusinesses(): Promise<AdminBusinessSummary[]> {
    const response = await this.request("/api/v1/admin/businesses");
    const payload = adminBusinessesResponseSchema.parse(await response.json());
    return payload.items;
  }

  async setAdminBusinessSubscriptionTier(
    tenantId: string,
    tier: "free" | "pro" | "business",
  ): Promise<void> {
    const body = setSubscriptionTierRequestSchema.parse({ tier });
    await this.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/subscription-tier`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
  }

  async searchAdminCustomers(email: string): Promise<AdminCustomerSummary[]> {
    const query = new URLSearchParams({ email });
    const response = await this.request(`/api/v1/admin/customers?${query.toString()}`);
    const payload = adminCustomersResponseSchema.parse(await response.json());
    return payload.items;
  }

  async exportAdminCustomer(customerId: string): Promise<CustomerDataExport> {
    const response = await this.request(
      `/api/v1/admin/customers/${encodeURIComponent(customerId)}/export`,
    );
    return customerDataExportSchema.parse(await response.json());
  }

  async initiateAdminCustomerDelete(customerId: string): Promise<void> {
    await this.request(
      `/api/v1/admin/customers/${encodeURIComponent(customerId)}/delete`,
      { method: "POST" },
    );
  }

  async getAdminMarkets(): Promise<AdminMarket[]> {
    const response = await this.request("/api/v1/admin/markets");
    const payload = adminMarketsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async createAdminMarket(
    body: Parameters<typeof createMarketRequestSchema.parse>[0],
  ): Promise<AdminMarket> {
    const payload = createMarketRequestSchema.parse(body);
    const response = await this.request("/api/v1/admin/markets", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return adminMarketSchema.parse(await response.json());
  }

  async updateAdminMarket(
    id: string,
    body: Parameters<typeof updateMarketRequestSchema.parse>[0],
  ): Promise<AdminMarket> {
    const payload = updateMarketRequestSchema.parse(body);
    const response = await this.request(`/api/v1/admin/markets/${encodeURIComponent(id)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return adminMarketSchema.parse(await response.json());
  }

  async setAdminMarketLive(id: string, isLive: boolean): Promise<void> {
    const body = setMarketLiveRequestSchema.parse({ isLive });
    await this.request(`/api/v1/admin/markets/${encodeURIComponent(id)}/live`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
  }

  private async request(
    path: string,
    init: RequestInit & { idempotencyKey?: string } = {},
  ): Promise<Response> {
    const { idempotencyKey, ...requestInit } = init;
    const headers = new Headers(requestInit.headers);
    headers.set("Accept", "application/json");

    if (this.accessToken) {
      headers.set("Authorization", `Bearer ${this.accessToken}`);
    } else if (this.devAuth0Sub) {
      headers.set("X-Dev-Auth0-Sub", this.devAuth0Sub);
    }

    if (this.tenantId) {
      headers.set("X-Tenant-Id", this.tenantId);
    }

    if (idempotencyKey) {
      headers.set("Idempotency-Key", idempotencyKey);
    }

    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      ...requestInit,
      headers,
    });

    if (!response.ok) {
      const payload = (await response.json().catch(() => ({}))) as ApiErrorResponse;
      throw new AdeniApiError(localizeErrorResponse(this.locale, payload), response.status);
    }

    return response;
  }
}
