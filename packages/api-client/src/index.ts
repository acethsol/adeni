import {
  authSessionSchema,
  adminCustomersResponseSchema,
  adminMarketsResponseSchema,
  adminMarketSchema,
  adminBusinessesResponseSchema,
  adminBusinessDirectoryStatsSchema,
  adminMarketCatalogStatsSchema,
  type AdminBusinessDirectoryStats,
  type AdminMarketCatalogStats,
  type AdminPagedResult,
  type PagedResult,
  setSubscriptionTierRequestSchema,
  setTenantCommercialTermsRequestSchema,
  adminTenantCommercialTermsSchema,
  subscriptionUsageSchema,
  subscriptionCheckoutRequestSchema,
  subscriptionCheckoutResponseSchema,
  tenantBillingHistoryResponseSchema,
  completeStubSubscriptionCheckoutRequestSchema,
  tenantSubscriptionChargeSchema,
  tenantDashboardSummarySchema,
  tenantDashboardSeriesSchema,
  tenantDashboardBreakdownSchema,
  tenantDashboardLayoutSchema,
  type TenantDashboardSummary,
  type TenantDashboardSeries,
  type TenantDashboardBreakdown,
  type TenantDashboardLayout,
  adminSubscriptionMetricsSchema,
  adminBusinessSummarySchema,
  adminRevenueMetricsSchema,
  adminTransactionsSearchResponseSchema,
  availableSlotsResponseSchema,
  bookingResponseSchema,
  billingDocumentResponseSchema,
  billingDocumentsResponseSchema,
  collectBalanceResponseSchema,
  createInvoiceRequestSchema,
  markBookingPaidRequestSchema,
  paymentReceiptResponseSchema,
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
  serviceTemplatesResponseSchema,
  type ServiceTemplate,
  submitVerificationRequestSchema,
  tenantBookingsResponseSchema,
  tenantLocationsResponseSchema,
  updateBusinessProfileRequestSchema,
  upsertBusinessLocationRequestSchema,
  mediaUploadUrlRequestSchema,
  mediaUploadUrlResponseSchema,
  updateCoverImageRequestSchema,
  updateCoverImageResponseSchema,
  addGalleryImageRequestSchema,
  removeGalleryImageRequestSchema,
  galleryImagesResponseSchema,
  createReviewRequestSchema,
  createQuoteRequestSchema,
  quoteRequestResponseSchema,
  quoteRequestsResponseSchema,
  submitQuoteOfferRequestSchema,
  replyToReviewRequestSchema,
  requestVerificationBadgeRequestSchema,
  verificationBadgeSchema,
  pendingBadgesResponseSchema,
  adminReviewsListResultSchema,
  type PendingBadgeRequest,
  type AdminReviewListItem,
  type AdminReviewsListResult,
  publicReviewItemSchema,
  joinWaitlistRequestSchema,
  joinQueueRequestSchema,
  queueTicketResponseSchema,
  queueTicketsResponseSchema,
  tenantQueueFloorResponseSchema,
  walkInSettingsSchema,
  updateWalkInSettingsRequestSchema,
  openWalkInBlockRequestSchema,
  staffAddQueueRequestSchema,
  tenantCustomerListResponseSchema,
  tenantCustomerDetailSchema,
  type TenantCustomerListResponse,
  type TenantCustomerDetail,
  kioskStatusResponseSchema,
  setKioskPinRequestSchema,
  verifyKioskPinRequestSchema,
  verifyKioskPinResponseSchema,
  initializePaymentRequestSchema,
  paymentIntentResponseSchema,
  createPaymentLinkRequestSchema,
  paymentLedgerResponseSchema,
  refundPaymentRequestSchema,
  updateBusinessSettingsRequestSchema,
  updateBusinessPoliciesRequestSchema,
  updatePublicPageRequestSchema,
  createServiceMenuGroupRequestSchema,
  updateServiceMenuGroupRequestSchema,
  serviceMenuGroupsResponseSchema,
  serviceMenuGroupSchema,
  staffMemberSchema,
  staffMembersResponseSchema,
  publicStaffMembersResponseSchema,
  createStaffMemberRequestSchema,
  updateStaffMemberRequestSchema,
  replaceStaffServicesRequestSchema,
  staffLeaveSchema,
  staffLeaveListResponseSchema,
  createStaffLeaveRequestSchema,
  staffCalendarResponseSchema,
  staffTeamCalendarResponseSchema,
  createStaffInviteRequestSchema,
  staffPortalInviteSchema,
  staffPortalInvitesResponseSchema,
  tenantAccessUserSchema,
  tenantAccessUsersResponseSchema,
  updateAccessUserRequestSchema,
  updateStaffAvatarRequestSchema,
  acceptStaffInviteRequestSchema,
  acceptStaffInviteResponseSchema,
  reviewResponseSchema,
  publicReviewsResponseSchema,
  createConversationRequestSchema,
  sendMessageRequestSchema,
  conversationSummarySchema,
  conversationDetailSchema,
  conversationsResponseSchema,
  conversationListResponseSchema,
  conversationMessageSchema,
  unreadCountResponseSchema,
  whatsAppLinkResponseSchema,
  messageTemplatesResponseSchema,
  notificationPreferencesSchema,
  updateNotificationPreferencesRequestSchema,
  messagingSettingsSchema,
  updateMessagingSettingsRequestSchema,
  updateMarketRequestSchema,
  updateServiceOfferingRequestSchema,
  weeklyAvailabilityResponseSchema,
  businessClosedDateSchema,
  businessClosedDatesResponseSchema,
  createBusinessClosedDateRequestSchema,
  type AuthSession,
  type AdminCustomerSummary,
  type AdminMarket,
  type AdminBusinessSummary,
  type AdminTenantCommercialTerms,
  type SetTenantCommercialTermsRequest,
  type SubscriptionUsage,
  type SubscriptionCheckoutRequest,
  type SubscriptionCheckoutResponse,
  type TenantSubscriptionCharge,
  type CompleteStubSubscriptionCheckoutRequest,
  type AdminSubscriptionMetrics,
  type AdminRevenueMetrics,
  type AdminTransactionSearchItem,
  type AvailableSlot,
  type BillingDocumentResponse,
  type BookingResponse,
  type CollectBalanceResponse,
  type CreateInvoiceRequest,
  type MarkBookingPaidRequest,
  type PaymentReceiptResponse,
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
  type UpdateBusinessPoliciesRequest,
  type UpdatePublicPageRequest,
  type ServiceMenuGroup,
  type CreateServiceMenuGroupRequest,
  type UpdateServiceMenuGroupRequest,
  type StaffMember,
  type PublicStaffMember,
  type CreateStaffMemberRequest,
  type UpdateStaffMemberRequest,
  type ReplaceStaffServicesRequest,
  type StaffLeave,
  type CreateStaffLeaveRequest,
  type CreateStaffInviteRequest,
  type StaffPortalInvite,
  type TenantAccessUser,
  type UpdateAccessUserRequest,
  type UpdateStaffAvatarRequest,
  type AcceptStaffInviteRequest,
  type AcceptStaffInviteResponse,
  type StaffCalendarResponse,
  type StaffTeamCalendarResponse,
  type WeeklyAvailabilityRule,
  type BusinessClosedDate,
  type CreateBusinessClosedDateRequest,
  type ServiceOfferingsResponse,
  type CreateQuoteRequest,
  type QuoteRequestResponse,
  type SubmitQuoteOfferRequest,
  type RequestVerificationBadgeRequest,
  type VerificationBadge,
  type PublicReviewItem,
  type JoinWaitlistRequest,
  type JoinQueueRequest,
  type QueueTicketResponse,
  type TenantQueueFloorResponse,
  type WalkInSettings,
  type UpdateWalkInSettingsRequest,
  type QueueExpectedArrival,
  type WalkInStatus,
  type StaffAddQueueRequest,
  type KioskStatusResponse,
  type VerifyKioskPinResponse,
  type InitializePaymentRequest,
  type PaymentIntentResponse,
  type CreatePaymentLinkRequest,
  type PaymentLedgerEntry,
  type RefundPaymentRequest,
  type CreateReviewRequest,
  type ReviewResponse,
  type PublicReviewsResponse,
  type CreateConversationRequest,
  type SendMessageRequest,
  type ConversationSummary,
  type ConversationDetail,
  type ConversationMessage,
  type UnreadCountResponse,
  type WhatsAppLinkResponse,
  type MessageTemplate,
  type NotificationPreferences,
  type UpdateNotificationPreferencesRequest,
  type MessagingSettings,
  type UpdateMessagingSettingsRequest,
  type UpdateCoverImageRequest,
  type AddGalleryImageRequest,
  type RemoveGalleryImageRequest,
  type GalleryImage,
  type MediaUploadUrlResponse,
  type MediaUploadUrlRequest,
  type UpdateServiceOfferingRequest,
  type UpsertBusinessLocationRequest,
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
    const response = await this.request(`/api/v1/categories${suffix}`);
    const payload = categoriesResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getCategoryServiceTemplates(categorySlug: string): Promise<ServiceTemplate[]> {
    const response = await this.request(
      `/api/v1/categories/${encodeURIComponent(categorySlug)}/service-templates`,
    );
    const payload = serviceTemplatesResponseSchema.parse(await response.json());
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

  async getBusinessServiceCatalog(slug: string): Promise<ServiceOfferingsResponse> {
    const response = await this.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/services`,
    );
    return serviceOfferingsResponseSchema.parse(await response.json());
  }

  async getBusinessServices(slug: string): Promise<ServiceOffering[]> {
    const catalog = await this.getBusinessServiceCatalog(slug);
    return catalog.items;
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
    const response = await this.request(
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

  async listAdminReviews(options: {
    q?: string;
    page?: number;
    pageSize?: number;
    includeHidden?: boolean;
  } = {}): Promise<AdminReviewsListResult> {
    const query = new URLSearchParams();
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    if (options.includeHidden) {
      query.set("includeHidden", "true");
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(`/api/v1/admin/reviews${suffix}`);
    return adminReviewsListResultSchema.parse(await response.json());
  }

  async hideAdminReview(reviewId: string): Promise<void> {
    await this.request(`/api/v1/admin/reviews/${encodeURIComponent(reviewId)}`, {
      method: "DELETE",
    });
  }

  async listAdminPendingBadges(
    options: { q?: string; page?: number; pageSize?: number } = {},
  ): Promise<AdminPagedResult<PendingBadgeRequest>> {
    const query = new URLSearchParams();
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(`/api/v1/admin/verification/pending-badges${suffix}`);
    return pendingBadgesResponseSchema.parse(await response.json());
  }

  async grantAdminVerificationBadge(tenantId: string, badgeType: string): Promise<void> {
    await this.request(
      `/api/v1/admin/verification/businesses/${encodeURIComponent(tenantId)}/badges/${encodeURIComponent(badgeType)}/grant`,
      { method: "POST" },
    );
  }

  async denyAdminVerificationBadge(tenantId: string, badgeType: string): Promise<void> {
    await this.request(
      `/api/v1/admin/verification/businesses/${encodeURIComponent(tenantId)}/badges/${encodeURIComponent(badgeType)}/revoke`,
      { method: "POST" },
    );
  }

  async listTenantBookings(options: {
    tab?: "pending" | "upcoming" | "past";
    page?: number;
    pageSize?: number;
  } = {}): Promise<PagedResult<BookingResponse>> {
    const query = new URLSearchParams();
    if (options.tab) {
      query.set("tab", options.tab);
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(`/api/v1/tenant/bookings${suffix}`);
    return tenantBookingsResponseSchema.parse(await response.json());
  }

  /** @deprecated Use listTenantBookings — returns first page of pending tab only. */
  async getTenantBookings(): Promise<BookingResponse[]> {
    return (await this.listTenantBookings({ tab: "pending", page: 1, pageSize: 100 })).items;
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

  async checkInTenantBooking(bookingId: string): Promise<BookingResponse> {
    const response = await this.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/check-in`,
      { method: "POST" },
    );
    return bookingResponseSchema.parse(await response.json());
  }

  async completeTenantBooking(bookingId: string): Promise<BookingResponse> {
    const response = await this.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/complete`,
      { method: "POST" },
    );
    return bookingResponseSchema.parse(await response.json());
  }

  async markTenantBookingPaid(
    bookingId: string,
    request: MarkBookingPaidRequest = {},
  ): Promise<BookingResponse> {
    const body = markBookingPaidRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/mark-paid`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return bookingResponseSchema.parse(await response.json());
  }

  async collectTenantBookingBalance(
    bookingId: string,
  ): Promise<CollectBalanceResponse> {
    const response = await this.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/collect-balance`,
      { method: "POST" },
    );
    return collectBalanceResponseSchema.parse(await response.json());
  }

  async getPaymentReceipt(paymentIntentId: string): Promise<PaymentReceiptResponse> {
    const response = await this.request(
      `/api/v1/payments/${encodeURIComponent(paymentIntentId)}/receipt`,
    );
    return paymentReceiptResponseSchema.parse(await response.json());
  }

  async listBillingDocuments(options?: {
    kind?: string;
    bookingId?: string;
    page?: number;
    pageSize?: number;
  }): Promise<BillingDocumentResponse[]> {
    const params = new URLSearchParams();
    if (options?.kind) params.set("kind", options.kind);
    if (options?.bookingId) params.set("bookingId", options.bookingId);
    if (options?.page) params.set("page", String(options.page));
    if (options?.pageSize) params.set("pageSize", String(options.pageSize));
    const query = params.toString();
    const response = await this.request(
      `/api/v1/payments/documents${query ? `?${query}` : ""}`,
    );
    return billingDocumentsResponseSchema.parse(await response.json()).items;
  }

  async getBillingDocument(documentId: string): Promise<BillingDocumentResponse> {
    const response = await this.request(
      `/api/v1/payments/documents/${encodeURIComponent(documentId)}`,
    );
    return billingDocumentResponseSchema.parse(await response.json());
  }

  async getBillingDocumentPdfUrl(documentId: string): Promise<string> {
    return `${this.baseUrl}/api/v1/payments/documents/${encodeURIComponent(documentId)}/pdf`;
  }

  async emailBillingDocument(
    documentId: string,
    toEmail?: string,
  ): Promise<BillingDocumentResponse> {
    const response = await this.request(
      `/api/v1/payments/documents/${encodeURIComponent(documentId)}/email`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ toEmail: toEmail ?? null }),
      },
    );
    return billingDocumentResponseSchema.parse(await response.json());
  }

  async createInvoice(request: CreateInvoiceRequest): Promise<BillingDocumentResponse> {
    const body = createInvoiceRequestSchema.parse(request);
    const response = await this.request("/api/v1/payments/documents/invoices", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return billingDocumentResponseSchema.parse(await response.json());
  }

  async getPublicBillingDocument(token: string): Promise<BillingDocumentResponse> {
    const response = await this.request(
      `/api/v1/public/documents/${encodeURIComponent(token)}`,
    );
    return billingDocumentResponseSchema.parse(await response.json());
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

  async updateTenantPublicPage(
    request: UpdatePublicPageRequest,
  ): Promise<BusinessProfile> {
    const body = updatePublicPageRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/public-page", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessProfileSchema.parse(await response.json());
  }

  async updateTenantPolicies(
    request: UpdateBusinessPoliciesRequest,
  ): Promise<BusinessProfile> {
    const body = updateBusinessPoliciesRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/policies", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessProfileSchema.parse(await response.json());
  }

  async createQuotePhotoUploadUrl(
    request: MediaUploadUrlRequest,
  ): Promise<MediaUploadUrlResponse> {
    const body = mediaUploadUrlRequestSchema.parse({
      ...request,
      purpose: "quote_photo",
    });
    const response = await this.request("/api/v1/customer/media/upload-url", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return mediaUploadUrlResponseSchema.parse(await response.json());
  }

  async listTenantVerificationBadges(): Promise<VerificationBadge[]> {
    const response = await this.request("/api/v1/tenant/verification/badges");
    const payload = (await response.json()) as { items: VerificationBadge[] };
    return payload.items ?? [];
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

  async listCustomerQuotes(): Promise<QuoteRequestResponse[]> {
    const response = await this.request("/api/v1/quotes");
    const payload = quoteRequestsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async acceptQuote(quoteRequestId: string): Promise<QuoteRequestResponse> {
    const response = await this.request(
      `/api/v1/quotes/${encodeURIComponent(quoteRequestId)}/accept`,
      { method: "POST" },
    );
    return quoteRequestResponseSchema.parse(await response.json());
  }

  async declineQuote(quoteRequestId: string): Promise<QuoteRequestResponse> {
    const response = await this.request(
      `/api/v1/quotes/${encodeURIComponent(quoteRequestId)}/decline`,
      { method: "POST" },
    );
    return quoteRequestResponseSchema.parse(await response.json());
  }

  async listTenantQuotes(): Promise<QuoteRequestResponse[]> {
    const response = await this.request("/api/v1/tenant/quotes");
    const payload = quoteRequestsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async submitQuoteOffer(
    quoteRequestId: string,
    request: SubmitQuoteOfferRequest,
  ): Promise<QuoteRequestResponse> {
    const body = submitQuoteOfferRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/tenant/quotes/${encodeURIComponent(quoteRequestId)}/offer`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return quoteRequestResponseSchema.parse(await response.json());
  }

  async listTenantReviews(): Promise<PublicReviewItem[]> {
    const response = await this.request("/api/v1/tenant/reviews");
    const payload = (await response.json()) as { items: PublicReviewItem[] };
    return payload.items ?? [];
  }

  async replyToReview(reviewId: string, reply: string): Promise<PublicReviewItem> {
    const body = replyToReviewRequestSchema.parse({ reply });
    const response = await this.request(
      `/api/v1/tenant/reviews/${encodeURIComponent(reviewId)}/reply`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return publicReviewItemSchema.parse(await response.json());
  }

  async requestVerificationBadge(
    request: RequestVerificationBadgeRequest,
  ): Promise<VerificationBadge> {
    const body = requestVerificationBadgeRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/verification/badges", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return verificationBadgeSchema.parse(await response.json());
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

  async joinPublicQueue(
    slug: string,
    request: JoinQueueRequest,
  ): Promise<QueueTicketResponse> {
    const body = joinQueueRequestSchema.parse(request);
    const response = await this.request(
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
    const response = await this.request(
      `/api/v1/public/queue/tickets/${encodeURIComponent(token)}`,
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async getTenantQueueFloor(): Promise<TenantQueueFloorResponse> {
    const response = await this.request("/api/v1/tenant/queue");
    return tenantQueueFloorResponseSchema.parse(await response.json());
  }

  /** @deprecated Use getTenantQueueFloor */
  async getTenantQueue(): Promise<QueueTicketResponse[]> {
    const floor = await this.getTenantQueueFloor();
    return floor.items;
  }

  async updateWalkInSettings(
    request: UpdateWalkInSettingsRequest,
  ): Promise<WalkInSettings> {
    const body = updateWalkInSettingsRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/queue/walk-in-settings", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return walkInSettingsSchema.parse(await response.json());
  }

  async openWalkInBlock(durationMinutes: number): Promise<WalkInSettings> {
    const body = openWalkInBlockRequestSchema.parse({ durationMinutes });
    const response = await this.request("/api/v1/tenant/queue/walk-in/open", {
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
    const response = await this.request("/api/v1/tenant/queue", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return queueTicketResponseSchema.parse(await response.json());
  }

  async callTenantQueueTicket(ticketId: string): Promise<QueueTicketResponse> {
    const response = await this.request(
      `/api/v1/tenant/queue/${encodeURIComponent(ticketId)}/call`,
      { method: "POST" },
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async startTenantQueueTicket(
    ticketId: string,
    staffMemberId?: string,
  ): Promise<QueueTicketResponse> {
    const response = await this.request(
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
    const response = await this.request(
      `/api/v1/tenant/queue/${encodeURIComponent(ticketId)}/complete`,
      { method: "POST" },
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async noShowTenantQueueTicket(ticketId: string): Promise<QueueTicketResponse> {
    const response = await this.request(
      `/api/v1/tenant/queue/${encodeURIComponent(ticketId)}/no-show`,
      { method: "POST" },
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async cancelTenantQueueTicket(ticketId: string): Promise<QueueTicketResponse> {
    const response = await this.request(
      `/api/v1/tenant/queue/${encodeURIComponent(ticketId)}/cancel`,
      { method: "POST" },
    );
    return queueTicketResponseSchema.parse(await response.json());
  }

  async listTenantCustomers(params?: {
    search?: string;
    page?: number;
    pageSize?: number;
  }): Promise<TenantCustomerListResponse> {
    const query = new URLSearchParams();
    if (params?.search) {
      query.set("search", params.search);
    }
    if (params?.page) {
      query.set("page", String(params.page));
    }
    if (params?.pageSize) {
      query.set("pageSize", String(params.pageSize));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(`/api/v1/tenant/customers${suffix}`);
    return tenantCustomerListResponseSchema.parse(await response.json());
  }

  async getTenantCustomer(tenantCustomerId: string): Promise<TenantCustomerDetail> {
    const response = await this.request(
      `/api/v1/tenant/customers/${encodeURIComponent(tenantCustomerId)}`,
    );
    return tenantCustomerDetailSchema.parse(await response.json());
  }

  async getTenantKioskStatus(): Promise<KioskStatusResponse> {
    const response = await this.request("/api/v1/tenant/kiosk/status");
    return kioskStatusResponseSchema.parse(await response.json());
  }

  async setTenantKioskPin(pin: string | null): Promise<KioskStatusResponse> {
    const body = setKioskPinRequestSchema.parse({ pin });
    const response = await this.request("/api/v1/tenant/kiosk/pin", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return kioskStatusResponseSchema.parse(await response.json());
  }

  async verifyTenantKioskPin(pin: string): Promise<VerifyKioskPinResponse> {
    const body = verifyKioskPinRequestSchema.parse({ pin });
    const response = await this.request("/api/v1/tenant/kiosk/verify-pin", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return verifyKioskPinResponseSchema.parse(await response.json());
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
  ): Promise<PagedResult<PaymentLedgerEntry>> {
    const query = new URLSearchParams({
      tenantId,
      page: String(page),
      pageSize: String(pageSize),
    });
    const response = await this.request(`/api/v1/payments/ledger?${query.toString()}`);
    return paymentLedgerResponseSchema.parse(await response.json());
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

  async createGalleryUploadUrl(
    contentType: string,
    contentLength: number,
  ): Promise<MediaUploadUrlResponse> {
    const body = mediaUploadUrlRequestSchema.parse({
      purpose: "gallery",
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

  async createStaffAvatarUploadUrl(
    contentType: string,
    contentLength: number,
  ): Promise<MediaUploadUrlResponse> {
    const body = mediaUploadUrlRequestSchema.parse({
      purpose: "staff_avatar",
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

  async updateTenantStaffAvatar(
    staffMemberId: string,
    request: UpdateStaffAvatarRequest,
  ): Promise<StaffMember> {
    const body = updateStaffAvatarRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/avatar`,
      {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return staffMemberSchema.parse(await response.json());
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

  async addTenantGalleryImage(request: AddGalleryImageRequest): Promise<GalleryImage[]> {
    const body = addGalleryImageRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/profile/gallery", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return galleryImagesResponseSchema.parse(await response.json()).items;
  }

  async removeTenantGalleryImage(request: RemoveGalleryImageRequest): Promise<GalleryImage[]> {
    const body = removeGalleryImageRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/profile/gallery", {
      method: "DELETE",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return galleryImagesResponseSchema.parse(await response.json()).items;
  }

  async getTenantServiceCatalog(): Promise<ServiceOfferingsResponse> {
    const response = await this.request("/api/v1/tenant/services");
    return serviceOfferingsResponseSchema.parse(await response.json());
  }

  async getTenantServices(): Promise<ServiceOffering[]> {
    const catalog = await this.getTenantServiceCatalog();
    return catalog.items;
  }

  async listTenantServiceMenuGroups(): Promise<ServiceMenuGroup[]> {
    const response = await this.request("/api/v1/tenant/service-menu-groups");
    return serviceMenuGroupsResponseSchema.parse(await response.json()).items;
  }

  async createTenantServiceMenuGroup(
    request: CreateServiceMenuGroupRequest,
  ): Promise<ServiceMenuGroup> {
    const body = createServiceMenuGroupRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/service-menu-groups", {
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
    const response = await this.request(
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
    await this.request(
      `/api/v1/tenant/service-menu-groups/${encodeURIComponent(groupId)}`,
      { method: "DELETE" },
    );
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

  async listTenantStaff(): Promise<StaffMember[]> {
    const response = await this.request("/api/v1/tenant/staff");
    return staffMembersResponseSchema.parse(await response.json()).items;
  }

  async createTenantStaff(request: CreateStaffMemberRequest): Promise<StaffMember> {
    const body = createStaffMemberRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/staff", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return staffMemberSchema.parse(await response.json());
  }

  async updateTenantStaff(
    staffMemberId: string,
    request: UpdateStaffMemberRequest,
  ): Promise<StaffMember> {
    const body = updateStaffMemberRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return staffMemberSchema.parse(await response.json());
  }

  async deactivateTenantStaff(staffMemberId: string): Promise<void> {
    await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/deactivate`,
      { method: "POST" },
    );
  }

  async inviteTenantStaff(
    staffMemberId: string,
    request: CreateStaffInviteRequest,
  ): Promise<StaffPortalInvite> {
    const body = createStaffInviteRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/invite`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return staffPortalInviteSchema.parse(await response.json());
  }

  async inviteTenantAccess(request: CreateStaffInviteRequest): Promise<StaffPortalInvite> {
    const body = createStaffInviteRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/access/invites", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return staffPortalInviteSchema.parse(await response.json());
  }

  async listTenantAccessInvites(): Promise<StaffPortalInvite[]> {
    const response = await this.request("/api/v1/tenant/access/invites");
    return staffPortalInvitesResponseSchema.parse(await response.json()).items;
  }

  async resendTenantAccessInvite(inviteId: string): Promise<StaffPortalInvite> {
    const response = await this.request(
      `/api/v1/tenant/access/invites/${encodeURIComponent(inviteId)}/resend`,
      { method: "POST" },
    );
    return staffPortalInviteSchema.parse(await response.json());
  }

  async revokeTenantAccessInvite(inviteId: string): Promise<void> {
    await this.request(
      `/api/v1/tenant/access/invites/${encodeURIComponent(inviteId)}`,
      { method: "DELETE" },
    );
  }

  async listTenantAccessUsers(): Promise<TenantAccessUser[]> {
    const response = await this.request("/api/v1/tenant/access/users");
    return tenantAccessUsersResponseSchema.parse(await response.json()).items;
  }

  async updateTenantAccessUser(
    businessUserId: string,
    request: UpdateAccessUserRequest,
  ): Promise<TenantAccessUser> {
    const body = updateAccessUserRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/tenant/access/users/${encodeURIComponent(businessUserId)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return tenantAccessUserSchema.parse(await response.json());
  }

  async revokeTenantAccessUser(businessUserId: string): Promise<void> {
    await this.request(
      `/api/v1/tenant/access/users/${encodeURIComponent(businessUserId)}`,
      { method: "DELETE" },
    );
  }

  async acceptStaffInvite(request: AcceptStaffInviteRequest): Promise<AcceptStaffInviteResponse> {
    const body = acceptStaffInviteRequestSchema.parse(request);
    const response = await this.request("/api/v1/auth/accept-staff-invite", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return acceptStaffInviteResponseSchema.parse(await response.json());
  }

  async replaceTenantStaffServices(
    staffMemberId: string,
    request: ReplaceStaffServicesRequest,
  ): Promise<StaffMember> {
    const body = replaceStaffServicesRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/services`,
      {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return staffMemberSchema.parse(await response.json());
  }

  async getTenantStaffHours(staffMemberId: string): Promise<WeeklyAvailabilityRule[]> {
    const response = await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/hours`,
    );
    return weeklyAvailabilityResponseSchema.parse(await response.json()).items;
  }

  async replaceTenantStaffHours(
    staffMemberId: string,
    items: WeeklyAvailabilityRule[],
  ): Promise<WeeklyAvailabilityRule[]> {
    const response = await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/hours`,
      {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ items }),
      },
    );
    return weeklyAvailabilityResponseSchema.parse(await response.json()).items;
  }

  async listTenantStaffLeave(
    staffMemberId: string,
    params?: { from?: string; to?: string },
  ): Promise<StaffLeave[]> {
    const query = new URLSearchParams();
    if (params?.from) query.set("from", params.from);
    if (params?.to) query.set("to", params.to);
    const qs = query.toString();
    const response = await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/leave${qs ? `?${qs}` : ""}`,
    );
    return staffLeaveListResponseSchema.parse(await response.json()).items;
  }

  async createTenantStaffLeave(
    staffMemberId: string,
    request: CreateStaffLeaveRequest,
  ): Promise<StaffLeave> {
    const body = createStaffLeaveRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/leave`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return staffLeaveSchema.parse(await response.json());
  }

  async deleteTenantStaffLeave(staffMemberId: string, leaveId: string): Promise<void> {
    await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/leave/${encodeURIComponent(leaveId)}`,
      { method: "DELETE" },
    );
  }

  async getTenantStaffCalendar(
    staffMemberId: string,
    params: { from: string; to: string },
  ): Promise<StaffCalendarResponse> {
    const query = new URLSearchParams({ from: params.from, to: params.to });
    const response = await this.request(
      `/api/v1/tenant/staff/${encodeURIComponent(staffMemberId)}/calendar?${query}`,
    );
    return staffCalendarResponseSchema.parse(await response.json());
  }

  async getTenantTeamCalendar(params: {
    from: string;
    to: string;
  }): Promise<StaffTeamCalendarResponse> {
    const query = new URLSearchParams({ from: params.from, to: params.to });
    const response = await this.request(`/api/v1/tenant/staff/team-calendar?${query}`);
    return staffTeamCalendarResponseSchema.parse(await response.json());
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

  async getTenantClosedDates(): Promise<BusinessClosedDate[]> {
    const response = await this.request("/api/v1/tenant/closed-dates");
    return businessClosedDatesResponseSchema.parse(await response.json()).items;
  }

  async createTenantClosedDate(
    request: CreateBusinessClosedDateRequest,
  ): Promise<BusinessClosedDate> {
    const body = createBusinessClosedDateRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/closed-dates", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessClosedDateSchema.parse(await response.json());
  }

  async deleteTenantClosedDate(id: string): Promise<void> {
    await this.request(`/api/v1/tenant/closed-dates/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });
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

  async getTenantDashboardSummary(from: string, to: string): Promise<TenantDashboardSummary> {
    const query = new URLSearchParams({ from, to });
    const response = await this.request(`/api/v1/tenant/dashboard/summary?${query.toString()}`);
    return tenantDashboardSummarySchema.parse(await response.json());
  }

  async getTenantDashboardSeries(
    metric: "appointments" | "collected" | "booked_value",
    from: string,
    to: string,
  ): Promise<TenantDashboardSeries> {
    const query = new URLSearchParams({ metric, from, to });
    const response = await this.request(`/api/v1/tenant/dashboard/series?${query.toString()}`);
    return tenantDashboardSeriesSchema.parse(await response.json());
  }

  async getTenantDashboardBreakdown(
    dimension: "status" | "service" | "staff",
    from: string,
    to: string,
  ): Promise<TenantDashboardBreakdown> {
    const query = new URLSearchParams({ dimension, from, to });
    const response = await this.request(`/api/v1/tenant/dashboard/breakdown?${query.toString()}`);
    return tenantDashboardBreakdownSchema.parse(await response.json());
  }

  async getTenantDashboardLayout(): Promise<TenantDashboardLayout> {
    const response = await this.request("/api/v1/tenant/dashboard/layout");
    return tenantDashboardLayoutSchema.parse(await response.json());
  }

  async putTenantDashboardLayout(layout: TenantDashboardLayout): Promise<TenantDashboardLayout> {
    const payload = tenantDashboardLayoutSchema.parse(layout);
    const response = await this.request("/api/v1/tenant/dashboard/layout", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return tenantDashboardLayoutSchema.parse(await response.json());
  }

  async getTenantBillingHistory(limit = 20): Promise<TenantSubscriptionCharge[]> {
    const response = await this.request(
      `/api/v1/tenant/billing?limit=${encodeURIComponent(String(limit))}`,
    );
    return tenantBillingHistoryResponseSchema.parse(await response.json()).items;
  }

  async createSubscriptionCheckout(
    body: SubscriptionCheckoutRequest,
  ): Promise<SubscriptionCheckoutResponse> {
    const payload = subscriptionCheckoutRequestSchema.parse(body);
    const response = await this.request("/api/v1/subscriptions/checkout", {
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
    const response = await this.request("/api/v1/subscriptions/checkout/complete", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return tenantSubscriptionChargeSchema.parse(await response.json());
  }

  async getMe(): Promise<AuthSession> {
    const response = await this.request("/api/v1/auth/me");
    return authSessionSchema.parse(await response.json());
  }

  async listPendingBusinesses(
    options: { q?: string; page?: number; pageSize?: number } = {},
  ): Promise<AdminPagedResult<PendingBusiness>> {
    const query = new URLSearchParams();
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(`/api/v1/admin/businesses/pending${suffix}`);
    return pendingBusinessesResponseSchema.parse(await response.json());
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

  async listAdminBusinesses(
    options: { q?: string; page?: number; pageSize?: number } = {},
  ): Promise<AdminPagedResult<AdminBusinessSummary>> {
    const query = new URLSearchParams();
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(`/api/v1/admin/businesses${suffix}`);
    return adminBusinessesResponseSchema.parse(await response.json());
  }

  async getAdminBusinessDirectoryStats(): Promise<AdminBusinessDirectoryStats> {
    const response = await this.request("/api/v1/admin/businesses/stats");
    return adminBusinessDirectoryStatsSchema.parse(await response.json());
  }

  async getAdminSubscriptionMetrics(): Promise<AdminSubscriptionMetrics> {
    const response = await this.request("/api/v1/admin/metrics/subscription");
    return adminSubscriptionMetricsSchema.parse(await response.json());
  }

  async getAdminRevenueMetrics(
    granularity: "month" | "year" = "month",
  ): Promise<AdminRevenueMetrics> {
    const query = new URLSearchParams({ granularity });
    const response = await this.request(
      `/api/v1/admin/metrics/revenue?${query.toString()}`,
    );
    return adminRevenueMetricsSchema.parse(await response.json());
  }

  async searchAdminTransactions(
    queryText: string,
    options: { kind?: "all" | "commerce" | "saas"; page?: number; pageSize?: number } = {},
  ): Promise<AdminPagedResult<AdminTransactionSearchItem>> {
    const query = new URLSearchParams({ q: queryText.trim() });
    query.set("kind", options.kind ?? "all");
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const response = await this.request(`/api/v1/admin/transactions?${query.toString()}`);
    return adminTransactionsSearchResponseSchema.parse(await response.json());
  }

  async getAdminBusiness(tenantId: string): Promise<AdminBusinessSummary> {
    const response = await this.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}`,
    );
    return adminBusinessSummarySchema.parse(await response.json());
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

  async getAdminTenantCommercialTerms(tenantId: string): Promise<AdminTenantCommercialTerms> {
    const response = await this.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/commercial-terms`,
    );
    return adminTenantCommercialTermsSchema.parse(await response.json());
  }

  async setAdminTenantCommercialTerms(
    tenantId: string,
    body: SetTenantCommercialTermsRequest,
  ): Promise<void> {
    const payload = setTenantCommercialTermsRequestSchema.parse(body);
    await this.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/commercial-terms`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );
  }

  async listAdminTenantSubscriptionCharges(
    tenantId: string,
    limit = 20,
  ): Promise<TenantSubscriptionCharge[]> {
    const query = new URLSearchParams();
    if (limit > 0) {
      query.set("limit", String(limit));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/subscription-charges${suffix}`,
    );
    return tenantBillingHistoryResponseSchema.parse(await response.json()).items;
  }

  async searchAdminCustomers(
    queryText: string,
    options: { page?: number; pageSize?: number } = {},
  ): Promise<AdminPagedResult<AdminCustomerSummary>> {
    const query = new URLSearchParams({ q: queryText.trim() });
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const response = await this.request(`/api/v1/admin/customers?${query.toString()}`);
    return adminCustomersResponseSchema.parse(await response.json());
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

  async getAdminMarketCatalogStats(): Promise<AdminMarketCatalogStats> {
    const response = await this.request("/api/v1/admin/markets/stats");
    return adminMarketCatalogStatsSchema.parse(await response.json());
  }

  async listAdminMarkets(
    options: { q?: string; page?: number; pageSize?: number } = {},
  ): Promise<AdminPagedResult<AdminMarket>> {
    const query = new URLSearchParams();
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(`/api/v1/admin/markets${suffix}`);
    return adminMarketsResponseSchema.parse(await response.json());
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

  async createConversation(
    request: CreateConversationRequest,
  ): Promise<ConversationSummary> {
    const body = createConversationRequestSchema.parse(request);
    const response = await this.request("/api/v1/messages/conversations", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return conversationSummarySchema.parse(await response.json());
  }

  async listCustomerConversations(): Promise<ConversationSummary[]> {
    const response = await this.request("/api/v1/messages/conversations");
    const payload = conversationListResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getCustomerConversation(
    id: string,
    options: { beforeId?: string; take?: number } = {},
  ): Promise<ConversationDetail> {
    const query = new URLSearchParams();
    if (options.beforeId) query.set("beforeId", options.beforeId);
    if (options.take != null) query.set("take", String(options.take));
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(
      `/api/v1/messages/conversations/${encodeURIComponent(id)}${suffix}`,
    );
    return conversationDetailSchema.parse(await response.json());
  }

  async sendCustomerMessage(
    conversationId: string,
    request: SendMessageRequest,
  ): Promise<ConversationMessage> {
    const body = sendMessageRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/messages/conversations/${encodeURIComponent(conversationId)}/messages`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return conversationMessageSchema.parse(await response.json());
  }

  async markCustomerConversationRead(
    conversationId: string,
    messageId?: string,
  ): Promise<void> {
    const query = messageId ? `?messageId=${encodeURIComponent(messageId)}` : "";
    await this.request(
      `/api/v1/messages/conversations/${encodeURIComponent(conversationId)}/read${query}`,
      { method: "POST" },
    );
  }

  async listTenantConversations(options: {
    page?: number;
    pageSize?: number;
  } = {}): Promise<PagedResult<ConversationSummary>> {
    const query = new URLSearchParams();
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(`/api/v1/tenant/messages/conversations${suffix}`);
    return conversationsResponseSchema.parse(await response.json());
  }

  async getTenantMessageUnreadCount(): Promise<number> {
    const response = await this.request("/api/v1/tenant/messages/unread-count");
    const payload = unreadCountResponseSchema.parse(await response.json());
    return payload.count;
  }

  async getTenantConversation(
    id: string,
    options: { beforeId?: string; take?: number } = {},
  ): Promise<ConversationDetail> {
    const query = new URLSearchParams();
    if (options.beforeId) query.set("beforeId", options.beforeId);
    if (options.take != null) query.set("take", String(options.take));
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.request(
      `/api/v1/tenant/messages/conversations/${encodeURIComponent(id)}${suffix}`,
    );
    return conversationDetailSchema.parse(await response.json());
  }

  async sendTenantMessage(
    conversationId: string,
    request: SendMessageRequest,
  ): Promise<ConversationMessage> {
    const body = sendMessageRequestSchema.parse(request);
    const response = await this.request(
      `/api/v1/tenant/messages/conversations/${encodeURIComponent(conversationId)}/messages`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return conversationMessageSchema.parse(await response.json());
  }

  async markTenantConversationRead(
    conversationId: string,
    messageId?: string,
  ): Promise<void> {
    const query = messageId ? `?messageId=${encodeURIComponent(messageId)}` : "";
    await this.request(
      `/api/v1/tenant/messages/conversations/${encodeURIComponent(conversationId)}/read${query}`,
      { method: "POST" },
    );
  }

  async getTenantMessageTemplates(): Promise<MessageTemplate[]> {
    const response = await this.request("/api/v1/tenant/messages/templates");
    const payload = messageTemplatesResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getNotificationPreferences(): Promise<NotificationPreferences> {
    const response = await this.request("/api/v1/tenant/notification-preferences");
    return notificationPreferencesSchema.parse(await response.json());
  }

  async updateNotificationPreferences(
    request: UpdateNotificationPreferencesRequest,
  ): Promise<NotificationPreferences> {
    const body = updateNotificationPreferencesRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/notification-preferences", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return notificationPreferencesSchema.parse(await response.json());
  }

  async getMessagingSettings(): Promise<MessagingSettings> {
    const response = await this.request("/api/v1/tenant/messages/settings");
    return messagingSettingsSchema.parse(await response.json());
  }

  async updateMessagingSettings(
    request: UpdateMessagingSettingsRequest,
  ): Promise<MessagingSettings> {
    const body = updateMessagingSettingsRequestSchema.parse(request);
    const response = await this.request("/api/v1/tenant/messages/settings", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return messagingSettingsSchema.parse(await response.json());
  }

  async getBusinessWhatsAppLink(slug: string): Promise<WhatsAppLinkResponse> {
    const response = await this.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/whatsapp-link`,
    );
    return whatsAppLinkResponseSchema.parse(await response.json());
  }

  async getBookingWhatsAppLink(bookingId: string): Promise<WhatsAppLinkResponse> {
    const response = await this.request(
      `/api/v1/bookings/${encodeURIComponent(bookingId)}/whatsapp-link`,
    );
    return whatsAppLinkResponseSchema.parse(await response.json());
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

    // Native fetch throws "Illegal invocation" when called as a method (`this.fetchImpl(...)`).
    const response = await this.fetchImpl.call(globalThis, `${this.baseUrl}${path}`, {
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
