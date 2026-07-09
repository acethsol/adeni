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
  coverImageUrl: z.string().url().nullable().optional(),
  ratingAvg: z.number().nullable().optional(),
  reviewCount: z.number().int().nonnegative().optional(),
  distanceKm: z.number(),
  latitude: z.number(),
  longitude: z.number(),
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
  coverImageUrl: z.string().url().nullable().optional(),
  ratingAvg: z.number().nullable().optional(),
  reviewCount: z.number().int().nonnegative().optional(),
  latitude: z.number().nullable(),
  longitude: z.number().nullable(),
});

export type PublicBusinessProfile = z.infer<typeof publicBusinessProfileSchema>;

export const authSessionSchema = z.object({
  userId: z.string().nullable(),
  roles: z.array(z.string()),
  tenantId: z.string().uuid().nullable(),
  hasMfa: z.boolean(),
});

export type AuthSession = z.infer<typeof authSessionSchema>;

export const pendingBusinessSchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  marketId: z.string(),
  status: z.string(),
  createdAt: z.string(),
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
  coverImageUrl: z.string().url().nullable().optional(),
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

export const mediaUploadPurposeSchema = z.enum(["cover", "Cover"]);

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
  priceAmount: z.number().positive(),
  currency: z.string().min(3).max(3),
  durationMinutes: z.number().int().positive(),
});

export type CreateServiceOfferingRequest = z.infer<
  typeof createServiceOfferingRequestSchema
>;

export const updateServiceOfferingRequestSchema = z.object({
  name: z.string().min(1),
  description: z.string().nullable().optional(),
  priceAmount: z.number().positive(),
  currency: z.string().min(3).max(3),
  durationMinutes: z.number().int().positive(),
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
};