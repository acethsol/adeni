import { z } from "zod";

export const categorySchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  parentSlug: z.string().nullable().optional(),
});

export const categoriesResponseSchema = z.object({
  items: z.array(categorySchema),
});

export type Category = z.infer<typeof categorySchema>;

export const discoveryBusinessItemSchema = z.object({
  locationId: z.string(),
  tenantId: z.string(),
  name: z.string(),
  locationName: z.string(),
  slug: z.string(),
  categorySlug: z.string(),
  area: z.string(),
  marketId: z.string(),
  coverImageUrl: z
    .union([z.string().url(), z.literal("")])
    .nullable()
    .optional()
    .transform((value) => (value === "" ? null : value)),
  ratingAvg: z.number().nullable().optional(),
  reviewCount: z.number().int().nonnegative().optional(),
  distanceKm: z.number(),
  latitude: z.number(),
  longitude: z.number(),
  businessType: z.enum(["scheduled_appointment", "quote_request"]).optional(),
  discoveryCta: z.enum(["book_now", "get_quote"]).optional(),
  verificationBadges: z.array(z.string()).optional(),
  verifiedSince: z.string().nullable().optional(),
  completionRate: z.number().min(0).max(1).nullable().optional(),
});

export const discoveryResponseSchema = z.object({
  items: z.array(discoveryBusinessItemSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
});

export type DiscoveryBusinessItem = z.infer<typeof discoveryBusinessItemSchema>;
export type DiscoveryResponse = z.infer<typeof discoveryResponseSchema>;

export const publicBusinessProfileSchema = z.object({
  locationId: z.string(),
  tenantId: z.string(),
  name: z.string(),
  locationName: z.string(),
  slug: z.string(),
  categorySlug: z.string(),
  area: z.string(),
  marketId: z.string(),
  addressLine: z.string(),
  description: z.string(),
  phoneMasked: z.string(),
  coverImageUrl: z
    .union([z.string().url(), z.literal("")])
    .nullable()
    .optional()
    .transform((value) => (value === "" ? null : value)),
  ratingAvg: z.number().nullable().optional(),
  reviewCount: z.number().int().nonnegative().optional(),
  latitude: z.number().nullable(),
  longitude: z.number().nullable(),
  businessType: z.enum(["scheduled_appointment", "quote_request"]).optional(),
  capabilities: z.array(z.string()).optional(),
  discoveryCta: z.enum(["book_now", "get_quote"]).optional(),
  depositPercent: z.number().int().min(0).max(100).optional(),
  verificationBadges: z.array(z.string()).optional(),
  verifiedSince: z.string().nullable().optional(),
  completionRate: z.number().min(0).max(1).nullable().optional(),
});

export type PublicBusinessProfile = z.infer<typeof publicBusinessProfileSchema>;

export const authSessionSchema = z.object({
  userId: z.string().nullable(),
  roles: z.array(z.string()),
  tenantId: z.string().uuid().nullable(),
  hasMfa: z.boolean(),
});

export type AuthSession = z.infer<typeof authSessionSchema>;

export const pendingVerificationDocumentSchema = z.object({
  documentType: z.string(),
  referenceNumber: z.string(),
  submittedAt: z.string(),
});

export const pendingBusinessSchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  marketId: z.string(),
  status: z.string(),
  createdAt: z.string(),
  documents: z.array(pendingVerificationDocumentSchema).optional(),
});

export const pendingBusinessesResponseSchema = z.object({
  items: z.array(pendingBusinessSchema),
});

export type PendingBusiness = z.infer<typeof pendingBusinessSchema>;

export const rejectBusinessRequestSchema = z.object({
  reason: z.string().min(10),
});

export const adminCustomerSummarySchema = z.object({
  id: z.string(),
  auth0Sub: z.string(),
  name: z.string(),
  email: z.string().nullable().optional(),
  createdAt: z.string(),
  erasureRequestedAt: z.string().nullable().optional(),
});

export type AdminCustomerSummary = z.infer<typeof adminCustomerSummarySchema>;

export const adminCustomersResponseSchema = z.object({
  items: z.array(adminCustomerSummarySchema),
});

export const marketConfigSchema = z.object({
  id: z.string(),
  name: z.string(),
  countryCode: z.string(),
  currency: z.string(),
  timeZoneId: z.string(),
  defaultLocation: z.object({
    lat: z.number(),
    lng: z.number(),
  }),
  languages: z.array(z.string()),
  isLive: z.boolean(),
  launchNote: z.string().nullable().optional(),
});

export const marketsResponseSchema = z.object({
  items: z.array(marketConfigSchema),
});

export type MarketApiItem = z.infer<typeof marketConfigSchema>;

export const adminMarketSchema = z.object({
  id: z.string(),
  name: z.string(),
  countryCode: z.string(),
  currency: z.string(),
  timeZoneId: z.string(),
  defaultLat: z.number(),
  defaultLng: z.number(),
  languages: z.array(z.string()),
  isLive: z.boolean(),
  launchNote: z.string().nullable().optional(),
  updatedAt: z.string(),
});

export const adminMarketsResponseSchema = z.object({
  items: z.array(adminMarketSchema),
});

export const createMarketRequestSchema = z.object({
  id: z.string().min(2).max(32),
  name: z.string().min(2),
  countryCode: z.string().length(2),
  currency: z.string().length(3),
  timeZoneId: z.string().min(1),
  defaultLat: z.number().min(-90).max(90),
  defaultLng: z.number().min(-180).max(180),
  languages: z.array(z.string()).min(1),
  isLive: z.boolean(),
  launchNote: z.string().nullable().optional(),
});

export const updateMarketRequestSchema = z.object({
  name: z.string().min(2),
  countryCode: z.string().length(2),
  currency: z.string().length(3),
  timeZoneId: z.string().min(1),
  defaultLat: z.number().min(-90).max(90),
  defaultLng: z.number().min(-180).max(180),
  languages: z.array(z.string()).min(1),
  launchNote: z.string().nullable().optional(),
});

export const setMarketLiveRequestSchema = z.object({
  isLive: z.boolean(),
});

export type AdminMarket = z.infer<typeof adminMarketSchema>;

export const customerBookingExportItemSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  serviceName: z.string(),
  startAt: z.string(),
  endAt: z.string(),
  status: z.string(),
  customerNotes: z.string().nullable().optional(),
  createdAt: z.string(),
});

export const customerDataExportSchema = z.object({
  customerId: z.string(),
  auth0Sub: z.string(),
  name: z.string(),
  email: z.string().nullable().optional(),
  phone: z.string().nullable().optional(),
  createdAt: z.string(),
  erasureRequestedAt: z.string().nullable().optional(),
  bookings: z.array(customerBookingExportItemSchema),
});

export type CustomerDataExport = z.infer<typeof customerDataExportSchema>;

export const registerBusinessLocationSchema = z.object({
  slug: z.string().min(3).max(64),
  name: z.string().optional(),
  addressLine: z.string().min(5),
  area: z.string().min(2),
  marketId: z.string().min(1),
  latitude: z.number().nullable().optional(),
  longitude: z.number().nullable().optional(),
  timeZoneId: z.string().nullable().optional(),
});

export const registerBusinessRequestSchema = z.object({
  businessName: z.string().min(2),
  categorySlug: z.string().min(1),
  phone: z.string().min(10),
  location: registerBusinessLocationSchema,
  description: z.string().optional(),
});

export type RegisterBusinessRequest = z.infer<typeof registerBusinessRequestSchema>;

export const registerBusinessResponseSchema = z.object({
  tenantId: z.string(),
  slug: z.string(),
  status: z.number(),
});

export type RegisterBusinessResponse = z.infer<typeof registerBusinessResponseSchema>;

export const businessContextResponseSchema = z.object({
  tenantId: z.string(),
  slug: z.string(),
  status: z.number(),
});

export type BusinessContextResponse = z.infer<typeof businessContextResponseSchema>;

export const serviceOfferingSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().nullable().optional(),
  priceAmount: z.number(),
  currency: z.string(),
  pricingType: z.enum(["fixed", "quote_request", "hourly"]).optional(),
  durationMinutes: z.number(),
  isActive: z.boolean(),
});

export const serviceOfferingsResponseSchema = z.object({
  items: z.array(serviceOfferingSchema),
});

export type ServiceOffering = z.infer<typeof serviceOfferingSchema>;

export const availableSlotSchema = z.object({
  startAt: z.string(),
  endAt: z.string(),
});

export const availableSlotsResponseSchema = z.object({
  items: z.array(availableSlotSchema),
});

export type AvailableSlot = z.infer<typeof availableSlotSchema>;

export const bookingResponseSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  serviceOfferingId: z.string(),
  serviceName: z.string(),
  customerId: z.string(),
  startAt: z.string(),
  endAt: z.string(),
  status: z.number(),
  customerNotes: z.string().nullable().optional(),
  createdAt: z.string(),
});

export type BookingResponse = z.infer<typeof bookingResponseSchema>;

export const customerBookingResponseSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  businessName: z.string(),
  businessSlug: z.string(),
  serviceOfferingId: z.string(),
  serviceName: z.string(),
  startAt: z.string(),
  endAt: z.string(),
  status: z.number(),
  customerNotes: z.string().nullable().optional(),
  createdAt: z.string(),
  canReview: z.boolean().optional(),
  hasReview: z.boolean().optional(),
  reviewRating: z.number().int().min(1).max(5).nullable().optional(),
});

export type CustomerBookingResponse = z.infer<typeof customerBookingResponseSchema>;

export const createReviewRequestSchema = z.object({
  rating: z.number().int().min(1).max(5),
  comment: z.string().max(1000).optional(),
});

export const reviewResponseSchema = z.object({
  id: z.string(),
  bookingId: z.string(),
  tenantId: z.string(),
  rating: z.number().int().min(1).max(5),
  comment: z.string(),
  createdAt: z.string(),
});

export const publicReviewItemSchema = z.object({
  id: z.string(),
  rating: z.number().int().min(1).max(5),
  comment: z.string(),
  createdAt: z.string(),
  customerDisplayName: z.string(),
  ownerReply: z.string().nullable().optional(),
  ownerReplyAt: z.string().nullable().optional(),
});

export const publicReviewsResponseSchema = z.object({
  items: z.array(publicReviewItemSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
});

export type CreateReviewRequest = z.infer<typeof createReviewRequestSchema>;
export type ReviewResponse = z.infer<typeof reviewResponseSchema>;
export type PublicReviewItem = z.infer<typeof publicReviewItemSchema>;
export type PublicReviewsResponse = z.infer<typeof publicReviewsResponseSchema>;

export function formatRatingSummary(
  ratingAvg?: number | null,
  reviewCount?: number | null,
): string {
  if (!reviewCount) {
    return "New";
  }

  const avg = ratingAvg ?? 0;
  return `${avg.toFixed(1)} (${reviewCount})`;
}

export function formatReviewCount(
  reviewCount?: number | null,
  emptyLabel = "—",
): string {
  if (!reviewCount) {
    return emptyLabel;
  }

  const label = reviewCount === 1 ? "review" : "reviews";
  return `${reviewCount.toLocaleString()} ${label}`;
}

export const customerBookingsResponseSchema = z.object({
  items: z.array(customerBookingResponseSchema),
});

export const tenantBookingsResponseSchema = z.object({
  items: z.array(bookingResponseSchema),
});

export const createBookingRequestSchema = z.object({
  tenantId: z.string().uuid(),
  serviceOfferingId: z.string().uuid(),
  startAt: z.string(),
  customerNotes: z.string().max(1000).optional(),
});

export type CreateBookingRequest = z.infer<typeof createBookingRequestSchema>;

export const businessLocationSchema = z.object({
  id: z.string(),
  slug: z.string(),
  name: z.string(),
  addressLine: z.string(),
  area: z.string(),
  marketId: z.string(),
  latitude: z.number().nullable().optional(),
  longitude: z.number().nullable().optional(),
  timeZoneId: z.string().nullable().optional(),
  isPrimary: z.boolean(),
  isActive: z.boolean(),
});

export type BusinessLocation = z.infer<typeof businessLocationSchema>;

export const tenantLocationsResponseSchema = z.object({
  items: z.array(businessLocationSchema),
});

export const upsertBusinessLocationRequestSchema = z.object({
  slug: z.string().min(3).max(64),
  name: z.string().optional(),
  addressLine: z.string().min(5),
  area: z.string().min(2),
  marketId: z.string().min(1),
  latitude: z.number().nullable().optional(),
  longitude: z.number().nullable().optional(),
  timeZoneId: z.string().nullable().optional(),
  isPrimary: z.boolean().optional(),
});

export type UpsertBusinessLocationRequest = z.infer<
  typeof upsertBusinessLocationRequestSchema
>;

export const verificationDocumentSchema = z.object({
  documentType: z.number(),
  submittedAt: z.string(),
});

export const tenantEntitlementsSchema = z.object({
  monthlyBookingLimit: z.number().nullable(),
  basicCalendar: z.boolean(),
  messaging: z.boolean(),
  analytics: z.boolean(),
  reminders: z.boolean(),
  multiLocation: z.boolean(),
  staffManagement: z.boolean(),
  prioritySupport: z.boolean(),
});

export type TenantEntitlements = z.infer<typeof tenantEntitlementsSchema>;

export const subscriptionTierSchema = z.enum(["free", "pro", "business"]);

export type SubscriptionTier = z.infer<typeof subscriptionTierSchema>;

export const subscriptionUsageSchema = z.object({
  tier: subscriptionTierSchema,
  entitlements: tenantEntitlementsSchema,
  bookingsUsedThisMonth: z.number().int().nonnegative(),
  bookingsLimitThisMonth: z.number().int().positive().nullable(),
});

export type SubscriptionUsage = z.infer<typeof subscriptionUsageSchema>;

export const adminBusinessSummarySchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  status: z.number(),
  subscriptionTier: subscriptionTierSchema,
  createdAt: z.string(),
});

export type AdminBusinessSummary = z.infer<typeof adminBusinessSummarySchema>;

export const adminBusinessesResponseSchema = z.object({
  items: z.array(adminBusinessSummarySchema),
});

export const setSubscriptionTierRequestSchema = z.object({
  tier: subscriptionTierSchema,
});

export type SetSubscriptionTierRequest = z.infer<typeof setSubscriptionTierRequestSchema>;

export const businessProfileSchema = z.object({
  tenantId: z.string(),
  businessName: z.string(),
  status: z.number(),
  categorySlug: z.string(),
  phone: z.string(),
  description: z.string(),
  createdAt: z.string(),
  verifiedAt: z.string().nullable().optional(),
  locations: z.array(businessLocationSchema),
  verificationDocuments: z.array(verificationDocumentSchema),
  coverImageUrl: z
    .union([z.string().url(), z.literal("")])
    .nullable()
    .optional()
    .transform((value) => (value === "" ? null : value)),
  businessType: z.enum(["scheduled_appointment", "quote_request"]).optional(),
  capabilities: z.array(z.string()).optional(),
  autoConfirmBookings: z.boolean().optional(),
  depositPercent: z.number().int().min(0).max(100).optional(),
  subscriptionTier: subscriptionTierSchema.optional(),
  entitlements: tenantEntitlementsSchema.optional(),
});

export type BusinessProfile = z.infer<typeof businessProfileSchema>;

export const updateBusinessProfileRequestSchema = z.object({
  businessName: z.string().min(1),
  categorySlug: z.string().min(1),
  phone: z.string().min(1),
  description: z.string().optional(),
});

export type UpdateBusinessProfileRequest = z.infer<
  typeof updateBusinessProfileRequestSchema
>;

export const updateBusinessSettingsRequestSchema = z.object({
  autoConfirmBookings: z.boolean(),
  depositPercent: z.number().int().min(0).max(100).optional(),
});

export type UpdateBusinessSettingsRequest = z.infer<
  typeof updateBusinessSettingsRequestSchema
>;

export const createQuoteRequestSchema = z.object({
  description: z.string().min(10).max(2000),
  serviceAddress: z.string().max(500).optional(),
  photoKeys: z.array(z.string()).max(5).optional(),
});

export type CreateQuoteRequest = z.infer<typeof createQuoteRequestSchema>;

export const quoteRequestResponseSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  description: z.string(),
  serviceAddress: z.string().nullable().optional(),
  photoKeys: z.array(z.string()).optional(),
  photoUrls: z.array(z.string()).optional(),
  status: z.string(),
  quotedAmount: z.number().nullable().optional(),
  quotedCurrency: z.string().nullable().optional(),
  quoteNotes: z.string().nullable().optional(),
  serviceOfferingId: z.string().nullable().optional(),
  proposedStartAt: z.string().nullable().optional(),
  proposedEndAt: z.string().nullable().optional(),
  expiresAt: z.string().nullable().optional(),
  bookingId: z.string().nullable().optional(),
  createdAt: z.string(),
});

export type QuoteRequestResponse = z.infer<typeof quoteRequestResponseSchema>;

export const submitQuoteOfferRequestSchema = z.object({
  amount: z.number().positive(),
  currency: z.string().length(3),
  notes: z.string().max(2000).optional(),
  serviceOfferingId: z.string().uuid(),
  proposedStartAt: z.string(),
  proposedEndAt: z.string(),
  expiresAt: z.string().optional(),
});

export type SubmitQuoteOfferRequest = z.infer<typeof submitQuoteOfferRequestSchema>;

export const quoteRequestsResponseSchema = z.object({
  items: z.array(quoteRequestResponseSchema),
});

export const verificationBadgeSchema = z.object({
  badgeType: z.string(),
  status: z.string(),
  grantedAt: z.string().nullable().optional(),
});

export const requestVerificationBadgeRequestSchema = z.object({
  badgeType: z.enum(["phone", "cac", "address", "license"]),
  referenceNumber: z.string().max(128).optional(),
});

export type RequestVerificationBadgeRequest = z.infer<typeof requestVerificationBadgeRequestSchema>;
export type VerificationBadge = z.infer<typeof verificationBadgeSchema>;

export const QUOTE_STATUS_LABELS: Record<string, string> = {
  submitted: "Awaiting quote",
  quoted: "Quote received",
  accepted: "Accepted",
  declined: "Declined",
  expired: "Expired",
};

export const replyToReviewRequestSchema = z.object({
  reply: z.string().min(1).max(1000),
});

export const tenantReviewItemSchema = publicReviewItemSchema;

export const joinWaitlistRequestSchema = z.object({
  tenantId: z.string().uuid(),
  serviceOfferingId: z.string().uuid(),
  preferredFrom: z.string().optional(),
  preferredTo: z.string().optional(),
});

export type JoinWaitlistRequest = z.infer<typeof joinWaitlistRequestSchema>;

export const initializePaymentRequestSchema = z.object({
  tenantId: z.string().uuid(),
  bookingId: z.string().uuid().optional(),
  amount: z.number().positive().optional(),
  currency: z.string().length(3),
  type: z.enum(["deposit", "link", "invoice"]).optional(),
  description: z.string().max(500).optional(),
  customerEmail: z.string().email().optional(),
  callbackUrl: z.string().url().optional(),
});

export type InitializePaymentRequest = z.infer<typeof initializePaymentRequestSchema>;

export const paymentIntentResponseSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  bookingId: z.string().nullable().optional(),
  type: z.string(),
  amount: z.number(),
  platformFeeAmount: z.number(),
  currency: z.string(),
  status: z.string(),
  checkoutUrl: z.string(),
  providerReference: z.string(),
  description: z.string().nullable().optional(),
  createdAt: z.string().optional(),
});

export const createPaymentLinkRequestSchema = z.object({
  tenantId: z.string().uuid(),
  amount: z.number().positive(),
  currency: z.string().length(3),
  description: z.string().min(1).max(500),
  customerEmail: z.string().email().optional(),
  callbackUrl: z.string().url().optional(),
});

export type CreatePaymentLinkRequest = z.infer<typeof createPaymentLinkRequestSchema>;

export const paymentLedgerEntrySchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  bookingId: z.string().nullable().optional(),
  type: z.string(),
  amount: z.number(),
  platformFeeAmount: z.number(),
  currency: z.string(),
  status: z.string(),
  providerReference: z.string(),
  description: z.string().nullable().optional(),
  createdAt: z.string(),
  updatedAt: z.string(),
});

export const paymentLedgerResponseSchema = z.object({
  items: z.array(paymentLedgerEntrySchema),
});

export type PaymentLedgerEntry = z.infer<typeof paymentLedgerEntrySchema>;

export const refundPaymentRequestSchema = z.object({
  tenantId: z.string().uuid(),
  amount: z.number().positive().optional(),
  reason: z.string().max(500).optional(),
});

export type RefundPaymentRequest = z.infer<typeof refundPaymentRequestSchema>;

export type PaymentIntentResponse = z.infer<typeof paymentIntentResponseSchema>;

export const createMessageThreadRequestSchema = z.object({
  tenantId: z.string().uuid(),
  bookingId: z.string().uuid().optional(),
});

export type CreateMessageThreadRequest = z.infer<typeof createMessageThreadRequestSchema>;

export const sendMessageRequestSchema = z.object({
  body: z.string().min(1).max(4000),
});

export type SendMessageRequest = z.infer<typeof sendMessageRequestSchema>;

export const messageThreadSummarySchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  bookingId: z.string().uuid().nullable().optional(),
  customerDisplayName: z.string(),
  businessName: z.string().nullable().optional(),
  preview: z.string().nullable().optional(),
  lastMessageAt: z.string(),
  unreadCount: z.number().int().nonnegative(),
});

export type MessageThreadSummary = z.infer<typeof messageThreadSummarySchema>;

export const messageResponseSchema = z.object({
  id: z.string().uuid(),
  threadId: z.string().uuid(),
  senderType: z.enum(["customer", "business"]),
  body: z.string(),
  createdAt: z.string(),
});

export type MessageResponse = z.infer<typeof messageResponseSchema>;

export const messageThreadDetailSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  bookingId: z.string().uuid().nullable().optional(),
  status: z.string(),
  customerDisplayName: z.string(),
  businessName: z.string().nullable().optional(),
  messages: z.array(messageResponseSchema),
});

export type MessageThreadDetail = z.infer<typeof messageThreadDetailSchema>;

export const messageThreadsResponseSchema = z.object({
  items: z.array(messageThreadSummarySchema),
});

export const unreadCountResponseSchema = z.object({
  count: z.number().int().nonnegative(),
});

export type UnreadCountResponse = z.infer<typeof unreadCountResponseSchema>;

export const whatsAppLinkResponseSchema = z.object({
  url: z.string().url(),
  phoneMasked: z.string().nullable().optional(),
});

export type WhatsAppLinkResponse = z.infer<typeof whatsAppLinkResponseSchema>;

export const messageTemplateSchema = z.object({
  key: z.string(),
  label: z.string(),
  body: z.string(),
});

export type MessageTemplate = z.infer<typeof messageTemplateSchema>;

export const messageTemplatesResponseSchema = z.object({
  items: z.array(messageTemplateSchema),
});

export const notificationPreferencesSchema = z.object({
  emailEnabled: z.boolean(),
  pushEnabled: z.boolean(),
  smsWhatsAppReminderEnabled: z.boolean(),
});

export type NotificationPreferences = z.infer<typeof notificationPreferencesSchema>;

export const updateNotificationPreferencesRequestSchema = notificationPreferencesSchema;

export type UpdateNotificationPreferencesRequest = z.infer<
  typeof updateNotificationPreferencesRequestSchema
>;

export const messagingSettingsSchema = z.object({
  faqAutoResponderEnabled: z.boolean(),
});

export type MessagingSettings = z.infer<typeof messagingSettingsSchema>;

export const updateMessagingSettingsRequestSchema = messagingSettingsSchema;

export type UpdateMessagingSettingsRequest = z.infer<
  typeof updateMessagingSettingsRequestSchema
>;

export const mediaUploadPurposeSchema = z.enum(["cover", "Cover", "quote_photo"]);

export const mediaUploadUrlRequestSchema = z.object({
  purpose: mediaUploadPurposeSchema,
  contentType: z.string().min(1),
  contentLength: z.number().int().positive(),
});

export const mediaUploadUrlResponseSchema = z.object({
  uploadUrl: z.string().url(),
  storageKey: z.string().min(1),
  expiresAt: z.string(),
});

export const updateCoverImageRequestSchema = z.object({
  coverImageKey: z.string().min(1),
});

export const updateCoverImageResponseSchema = z.object({
  coverImageUrl: z.string().url(),
});

export type MediaUploadUrlRequest = z.infer<typeof mediaUploadUrlRequestSchema>;
export type MediaUploadUrlResponse = z.infer<typeof mediaUploadUrlResponseSchema>;
export type UpdateCoverImageRequest = z.infer<typeof updateCoverImageRequestSchema>;

export const weeklyAvailabilityRuleSchema = z.object({
  dayOfWeek: z.number().int().min(0).max(6),
  openTime: z.string(),
  closeTime: z.string(),
});

export type WeeklyAvailabilityRule = z.infer<typeof weeklyAvailabilityRuleSchema>;

export const weeklyAvailabilityResponseSchema = z.object({
  items: z.array(weeklyAvailabilityRuleSchema),
});

export const createServiceOfferingRequestSchema = z.object({
  name: z.string().min(1),
  description: z.string().nullable().optional(),
  priceAmount: z.number().nonnegative(),
  currency: z.string().min(3).max(3),
  durationMinutes: z.number().int().positive(),
  pricingType: z.enum(["fixed", "quote_request", "hourly"]).optional(),
});

export type CreateServiceOfferingRequest = z.infer<
  typeof createServiceOfferingRequestSchema
>;

export const updateServiceOfferingRequestSchema = z.object({
  name: z.string().min(1),
  description: z.string().nullable().optional(),
  priceAmount: z.number().nonnegative(),
  currency: z.string().min(3).max(3),
  durationMinutes: z.number().int().positive(),
  pricingType: z.enum(["fixed", "quote_request", "hourly"]).optional(),
  isActive: z.boolean(),
});

export type UpdateServiceOfferingRequest = z.infer<
  typeof updateServiceOfferingRequestSchema
>;

export const BOOKING_STATUS_LABELS: Record<number, string> = {
  0: "Pending",
  1: "Confirmed",
  2: "Rejected",
  3: "Cancelled",
};

export const TENANT_STATUS_LABELS: Record<number, string> = {
  0: "Draft",
  1: "Pending verification",
  2: "Verified",
  3: "Rejected",
  4: "Suspended",
};

export function formatBookingStatus(status: number): string {
  return BOOKING_STATUS_LABELS[status] ?? "Unknown";
}

export function formatTenantStatus(status: number): string {
  return TENANT_STATUS_LABELS[status] ?? "Unknown";
}

export const verificationDocumentRequestSchema = z.object({
  documentType: z.number().int(),
  referenceNumber: z.string().min(1),
});

export const submitVerificationRequestSchema = z.object({
  documents: z.array(verificationDocumentRequestSchema).min(1),
});

export type SubmitVerificationRequest = z.infer<typeof submitVerificationRequestSchema>;

export const DAY_OF_WEEK_LABELS: Record<number, string> = {
  0: "Sunday",
  1: "Monday",
  2: "Tuesday",
  3: "Wednesday",
  4: "Thursday",
  5: "Friday",
  6: "Saturday",
};

export const VERIFICATION_DOCUMENT_LABELS: Record<number, string> = {
  0: "CAC registration",
  1: "National ID",
  2: "Address proof",
  3: "Trade license",
};

export const VERIFICATION_BADGE_LABELS: Record<string, string> = {
  phone: "Phone verified",
  cac: "CAC verified",
  address: "Address verified",
  license: "Licensed professional",
};

export const CATEGORY_REQUIRED_BADGES: Record<string, string[]> = {
  plumbers: ["license"],
  electricians: ["license"],
};