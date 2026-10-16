import { z } from "zod";
import { type PagedResult, pagedResultSchema } from "./paging";

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
  imageUrls: z.array(z.string().url()).nullish(),
  ratingAvg: z.number().nullable().optional(),
  reviewCount: z.number().int().nonnegative().optional(),
  distanceKm: z.number(),
  latitude: z.number(),
  longitude: z.number(),
  businessType: z.enum(["scheduled_appointment", "quote_request"]).optional(),
  discoveryCta: z.enum(["book_now", "get_quote"]).optional(),
  verificationBadges: z.array(z.string()).nullish(),
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

export const publicPageSectionsSchema = z.object({
  about: z.boolean(),
  services: z.boolean(),
  reviews: z.boolean(),
  visit: z.boolean(),
  book: z.boolean(),
  policies: z.boolean().optional().default(false),
});

export const publicPageConfigSchema = z.object({
  templateId: z.enum(["studio", "spa", "barber", "luxe"]),
  accentColor: z
    .string()
    .regex(/^#[0-9A-Fa-f]{6}$/)
    .nullable()
    .optional(),
  logoImageUrl: z
    .union([z.string().url(), z.literal("")])
    .nullable()
    .optional()
    .transform((value) => (value === "" ? null : value)),
  sections: publicPageSectionsSchema,
});

export const updatePublicPageRequestSchema = z
  .object({
    templateId: z.enum(["studio", "spa", "barber", "luxe"]),
    accentColor: z
      .union([z.string().regex(/^#[0-9A-Fa-f]{6}$/), z.literal(""), z.null()])
      .optional(),
    logoImageKey: z.union([z.string().max(512), z.literal(""), z.null()]).optional(),
    sections: publicPageSectionsSchema,
  })
  .superRefine((value, ctx) => {
    if (!value.sections.about && !value.sections.services && !value.sections.visit) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "At least one of about, services, or visit must be enabled.",
        path: ["sections"],
      });
    }
  });

export type PublicPageSections = z.infer<typeof publicPageSectionsSchema>;
export type PublicPageConfig = z.infer<typeof publicPageConfigSchema>;
export type UpdatePublicPageRequest = z.infer<typeof updatePublicPageRequestSchema>;

export const DEFAULT_PUBLIC_PAGE_CONFIG: PublicPageConfig = {
  templateId: "studio",
  accentColor: null,
  logoImageUrl: null,
  sections: {
    about: true,
    services: true,
    reviews: true,
    visit: true,
    book: true,
    policies: false,
  },
};

export const businessPoliciesSchema = z.object({
  booking: z.string().max(8000).nullable().optional(),
  payment: z.string().max(8000).nullable().optional(),
  cancellation: z.string().max(8000).nullable().optional(),
  terms: z.string().max(8000).nullable().optional(),
  requireAcceptance: z.boolean().optional().default(false),
});

export type BusinessPolicies = z.infer<typeof businessPoliciesSchema>;

export const updateBusinessPoliciesRequestSchema = z.object({
  booking: z.string().max(8000).nullable().optional(),
  payment: z.string().max(8000).nullable().optional(),
  cancellation: z.string().max(8000).nullable().optional(),
  terms: z.string().max(8000).nullable().optional(),
  requireAcceptance: z.boolean(),
});

export type UpdateBusinessPoliciesRequest = z.infer<
  typeof updateBusinessPoliciesRequestSchema
>;

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
  verificationBadges: z.array(z.string()).nullish(),
  verifiedSince: z.string().nullable().optional(),
  completionRate: z.number().min(0).max(1).nullable().optional(),
  publicPage: publicPageConfigSchema.optional(),
  /** Cover + gallery URLs for the public profile page. */
  imageUrls: z.array(z.string().url()).nullish(),
  policies: businessPoliciesSchema.nullish(),
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
  status: z.number().int(),
  createdAt: z.string(),
  documents: z.array(pendingVerificationDocumentSchema).optional(),
});

/** @deprecated Use pagedResultSchema from `./paging`. */
export const adminPagedResultSchema = pagedResultSchema;

export type AdminPagedResult<T> = PagedResult<T>;

export const pendingBusinessesResponseSchema = adminPagedResultSchema(pendingBusinessSchema);

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

export const adminCustomersResponseSchema = adminPagedResultSchema(adminCustomerSummarySchema);

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

export const adminMarketsResponseSchema = adminPagedResultSchema(adminMarketSchema);

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
  additionalCategorySlugs: z.array(z.string()).optional(),
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
  permissionRole: z.string().optional().default("owner"),
  permissions: z.array(z.string()).optional().default([]),
  staffMemberId: z.string().uuid().nullable().optional(),
});

export type BusinessContextResponse = z.infer<typeof businessContextResponseSchema>;

export const bookingDeliveryTypeSchema = z.enum([
  "appointment",
  "class",
  "session",
  "experience",
  "mobile_appointment",
]);

export type BookingDeliveryType = z.infer<typeof bookingDeliveryTypeSchema>;

export const serviceTemplateSchema = z.object({
  id: z.string(),
  name: z.string(),
  defaultDurationMinutes: z.number().int(),
  bookingDeliveryType: bookingDeliveryTypeSchema,
});

export type ServiceTemplate = z.infer<typeof serviceTemplateSchema>;

export const serviceTemplatesResponseSchema = z.object({
  items: z.array(serviceTemplateSchema),
});

export const serviceMenuGroupSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  sortOrder: z.number().int(),
});

export type ServiceMenuGroup = z.infer<typeof serviceMenuGroupSchema>;

export const serviceOfferingSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().nullable().optional(),
  priceAmount: z.number(),
  currency: z.string(),
  pricingType: z.enum(["fixed", "quote_request", "hourly"]).optional(),
  durationMinutes: z.number(),
  isActive: z.boolean(),
  categorySlug: z.string().nullable().optional(),
  catalogServiceId: z.string().nullable().optional(),
  bookingDeliveryType: bookingDeliveryTypeSchema.optional(),
  menuGroupId: z.string().uuid().nullable().optional(),
  sortOrder: z.number().int().optional(),
  isAddOn: z.boolean().optional().default(false),
});

export const serviceOfferingsResponseSchema = z.object({
  items: z.array(serviceOfferingSchema),
  groups: z.array(serviceMenuGroupSchema).optional().default([]),
});

export type ServiceOffering = z.infer<typeof serviceOfferingSchema>;
export type ServiceOfferingsResponse = z.infer<typeof serviceOfferingsResponseSchema>;

export const createServiceMenuGroupRequestSchema = z.object({
  name: z.string().min(2).max(120),
  sortOrder: z.number().int().optional(),
});

export type CreateServiceMenuGroupRequest = z.infer<
  typeof createServiceMenuGroupRequestSchema
>;

export const updateServiceMenuGroupRequestSchema = z.object({
  name: z.string().min(2).max(120),
  sortOrder: z.number().int(),
});

export type UpdateServiceMenuGroupRequest = z.infer<
  typeof updateServiceMenuGroupRequestSchema
>;

export const serviceMenuGroupsResponseSchema = z.object({
  items: z.array(serviceMenuGroupSchema),
});

export const availableSlotSchema = z.object({
  startAt: z.string(),
  endAt: z.string(),
});

export const availableSlotsResponseSchema = z.object({
  items: z.array(availableSlotSchema),
});

export type AvailableSlot = z.infer<typeof availableSlotSchema>;

export const bookingLineResponseSchema = z.object({
  serviceOfferingId: z.string().uuid(),
  serviceName: z.string(),
  priceAmount: z.number(),
  currency: z.string(),
  durationMinutes: z.number().int(),
  sortOrder: z.number().int(),
  isAddOn: z.boolean(),
});

export type BookingLineResponse = z.infer<typeof bookingLineResponseSchema>;

export const bookingGuestResponseSchema = z.object({
  displayName: z.string().nullable().optional(),
  sortOrder: z.number().int(),
});

export type BookingGuestResponse = z.infer<typeof bookingGuestResponseSchema>;

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
  staffMemberId: z.string().uuid().nullable().optional(),
  staffDisplayName: z.string().nullable().optional(),
  guestCount: z.number().int().optional().default(1),
  lines: z.array(bookingLineResponseSchema).nullable().optional(),
  guests: z.array(bookingGuestResponseSchema).nullable().optional(),
  totalPriceAmount: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
  settlementSource: z.string().nullable().optional(),
  paidAt: z.string().nullable().optional(),
  externalPaymentReference: z.string().nullable().optional(),
  remainingBalanceAmount: z.number().nullable().optional(),
  customerName: z.string().nullable().optional(),
  customerPhoneMasked: z.string().nullable().optional(),
});

export type BookingResponse = z.infer<typeof bookingResponseSchema>;

export const markBookingPaidRequestSchema = z.object({
  externalReference: z.string().max(200).nullable().optional(),
  note: z.string().max(1000).nullable().optional(),
});

export type MarkBookingPaidRequest = z.infer<typeof markBookingPaidRequestSchema>;

export const collectBalanceResponseSchema = z.object({
  bookingId: z.string().uuid(),
  paymentIntentId: z.string().uuid(),
  amount: z.number(),
  currency: z.string(),
  checkoutUrl: z.string(),
  providerReference: z.string(),
  booking: bookingResponseSchema,
});

export type CollectBalanceResponse = z.infer<typeof collectBalanceResponseSchema>;

export const paymentReceiptResponseSchema = z.object({
  paymentIntentId: z.string().uuid().optional(),
  bookingId: z.string().uuid().nullable().optional(),
  businessName: z.string(),
  description: z.string().nullable().optional(),
  amount: z.number().optional(),
  currency: z.string(),
  status: z.string(),
  paidAt: z.string().nullable().optional(),
  method: z.string().nullable().optional(),
  providerReference: z.string().optional(),
  // Billing document fields (Sprint 28) — receipt endpoint may return a document
  id: z.string().uuid().optional(),
  kind: z.string().optional(),
  documentNumber: z.string().optional(),
  total: z.number().optional(),
  publicUrl: z.string().optional(),
  publicToken: z.string().optional(),
});

export type PaymentReceiptResponse = z.infer<typeof paymentReceiptResponseSchema>;

export const billingDocumentLineSchema = z.object({
  description: z.string(),
  quantity: z.number(),
  unitAmount: z.number(),
  lineTotal: z.number(),
  sortOrder: z.number().int(),
});

export const billingDocumentResponseSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  kind: z.string(),
  status: z.string(),
  documentNumber: z.string(),
  bookingId: z.string().uuid().nullable().optional(),
  paymentIntentId: z.string().uuid().nullable().optional(),
  businessName: z.string(),
  customerName: z.string().nullable().optional(),
  customerEmail: z.string().nullable().optional(),
  currency: z.string(),
  subtotal: z.number(),
  taxTotal: z.number().nullable().optional(),
  total: z.number(),
  method: z.string().nullable().optional(),
  issuedAt: z.string().nullable().optional(),
  lines: z.array(billingDocumentLineSchema),
  publicUrl: z.string(),
  publicToken: z.string(),
});

export type BillingDocumentResponse = z.infer<typeof billingDocumentResponseSchema>;

export const billingDocumentsResponseSchema = z.object({
  items: z.array(billingDocumentResponseSchema),
});

export const createInvoiceRequestSchema = z.object({
  tenantId: z.string().uuid(),
  bookingId: z.string().uuid().nullable().optional(),
  amount: z.number().positive().optional(),
  currency: z.string().length(3).optional(),
  description: z.string().max(500).optional(),
  customerEmail: z.string().email().optional(),
  issue: z.boolean().optional(),
  initializePayment: z.boolean().optional(),
});

export type CreateInvoiceRequest = z.infer<typeof createInvoiceRequestSchema>;

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
  staffMemberId: z.string().uuid().nullable().optional(),
  staffDisplayName: z.string().nullable().optional(),
  guestCount: z.number().int().optional().default(1),
  lines: z.array(bookingLineResponseSchema).nullable().optional(),
  guests: z.array(bookingGuestResponseSchema).nullable().optional(),
  totalPriceAmount: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
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

export const tenantBookingsResponseSchema = pagedResultSchema(bookingResponseSchema);

export const tenantBookingListTabSchema = z.enum(["pending", "upcoming", "past"]);
export type TenantBookingListTab = z.infer<typeof tenantBookingListTabSchema>;

export const createBookingLineRequestSchema = z.object({
  serviceOfferingId: z.string().uuid(),
  staffMemberId: z.string().uuid().nullable().optional(),
});

export const createBookingGuestRequestSchema = z.object({
  displayName: z.string().max(120).nullable().optional(),
});

export const createBookingRequestSchema = z
  .object({
    tenantId: z.string().uuid(),
    serviceOfferingId: z.string().uuid().optional(),
    startAt: z.string(),
    customerNotes: z.string().max(1000).optional(),
    staffMemberId: z.string().uuid().nullable().optional(),
    lines: z.array(createBookingLineRequestSchema).min(1).max(10).optional(),
    guestCount: z.number().int().min(1).max(6).optional(),
    guests: z.array(createBookingGuestRequestSchema).max(6).optional(),
  })
  .refine((data) => (data.lines?.length ?? 0) > 0 || !!data.serviceOfferingId, {
    message: "serviceOfferingId or lines is required",
    path: ["serviceOfferingId"],
  });

export type CreateBookingRequest = z.infer<typeof createBookingRequestSchema>;

export const staffRoleKeySchema = z.enum([
  "stylist",
  "barber",
  "nail_tech",
  "esthetician",
  "therapist",
  "instructor",
  "trainer",
  "receptionist",
  "supervisor",
  "manager",
  "accountant",
  "inventory_manager",
  "marketing",
  "hr",
  "admin_staff",
  "other",
]);

export const portalPermissionRoleSchema = z.enum([
  "owner",
  "manager",
  "receptionist",
  "practitioner",
  "accountant",
  "ops",
]);

export const portalAccessStatusSchema = z.enum(["none", "invite_pending", "active"]);

export type PortalAccessStatus = z.infer<typeof portalAccessStatusSchema>;

export const staffMemberSchema = z.object({
  id: z.string().uuid(),
  firstName: z.string(),
  lastName: z.string(),
  displayName: z.string(),
  roleKey: staffRoleKeySchema,
  title: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  isActive: z.boolean(),
  sortOrder: z.number().int(),
  avatarImageUrl: z.string().nullable().optional(),
  serviceOfferingIds: z.array(z.string().uuid()),
  portalAccessStatus: portalAccessStatusSchema.optional().default("none"),
  portalInviteId: z.string().uuid().nullable().optional(),
  businessUserId: z.string().uuid().nullable().optional(),
  permissionRole: portalPermissionRoleSchema.nullable().optional(),
});

export type StaffMember = z.infer<typeof staffMemberSchema>;

export const publicStaffMemberSchema = z.object({
  id: z.string().uuid(),
  displayName: z.string(),
  roleKey: staffRoleKeySchema,
  title: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  sortOrder: z.number().int(),
  avatarImageUrl: z.string().nullable().optional(),
  serviceOfferingIds: z.array(z.string().uuid()),
});

export type PublicStaffMember = z.infer<typeof publicStaffMemberSchema>;

export const staffMembersResponseSchema = z.object({
  items: z.array(staffMemberSchema),
});

export const publicStaffMembersResponseSchema = z.object({
  items: z.array(publicStaffMemberSchema),
});

export const createStaffMemberRequestSchema = z.object({
  firstName: z.string().min(1).max(80),
  lastName: z.string().min(1).max(80),
  displayName: z.string().min(2).max(120).optional(),
  roleKey: staffRoleKeySchema.optional(),
  title: z.string().max(120).nullable().optional(),
  bio: z.string().max(500).nullable().optional(),
  sortOrder: z.number().int().optional(),
  serviceOfferingIds: z.array(z.string().uuid()).optional(),
});

export type CreateStaffMemberRequest = z.infer<typeof createStaffMemberRequestSchema>;

export const updateStaffMemberRequestSchema = z.object({
  firstName: z.string().min(1).max(80),
  lastName: z.string().min(1).max(80),
  displayName: z.string().min(2).max(120),
  roleKey: staffRoleKeySchema,
  title: z.string().max(120).nullable().optional(),
  bio: z.string().max(500).nullable().optional(),
  sortOrder: z.number().int(),
  isActive: z.boolean(),
});

export type UpdateStaffMemberRequest = z.infer<typeof updateStaffMemberRequestSchema>;

export const replaceStaffServicesRequestSchema = z.object({
  serviceOfferingIds: z.array(z.string().uuid()),
});

export type ReplaceStaffServicesRequest = z.infer<
  typeof replaceStaffServicesRequestSchema
>;

export const staffLeaveSchema = z.object({
  id: z.string().uuid(),
  staffMemberId: z.string().uuid(),
  startAt: z.string(),
  endAt: z.string(),
  reason: z.string().nullable().optional(),
  createdAt: z.string(),
  conflictingBookingIds: z.array(z.string().uuid()),
});

export type StaffLeave = z.infer<typeof staffLeaveSchema>;

export const staffLeaveListResponseSchema = z.object({
  items: z.array(staffLeaveSchema),
});

export const createStaffLeaveRequestSchema = z.object({
  startAt: z.string(),
  endAt: z.string(),
  reason: z.string().max(200).nullable().optional(),
});

export type CreateStaffLeaveRequest = z.infer<typeof createStaffLeaveRequestSchema>;

export const createStaffInviteRequestSchema = z.object({
  email: z.string().email().max(320),
  permissionRole: portalPermissionRoleSchema.exclude(["owner"]).optional(),
});

export type CreateStaffInviteRequest = z.infer<typeof createStaffInviteRequestSchema>;

export const staffPortalInviteSchema = z.object({
  inviteId: z.string().uuid(),
  email: z.string(),
  permissionRole: portalPermissionRoleSchema,
  staffMemberId: z.string().uuid().nullable().optional(),
  status: z.enum(["pending", "accepted", "revoked", "expired"]),
  expiresAt: z.string(),
});

export type StaffPortalInvite = z.infer<typeof staffPortalInviteSchema>;

export const staffPortalInvitesResponseSchema = z.object({
  items: z.array(staffPortalInviteSchema),
});

export const tenantAccessUserSchema = z.object({
  businessUserId: z.string().uuid(),
  permissionRole: portalPermissionRoleSchema,
  staffMemberId: z.string().uuid().nullable().optional(),
  createdAt: z.string(),
});

export type TenantAccessUser = z.infer<typeof tenantAccessUserSchema>;

export const tenantAccessUsersResponseSchema = z.object({
  items: z.array(tenantAccessUserSchema),
});

export const updateAccessUserRequestSchema = z.object({
  permissionRole: portalPermissionRoleSchema.exclude(["owner"]),
});

export type UpdateAccessUserRequest = z.infer<typeof updateAccessUserRequestSchema>;

export const acceptStaffInviteRequestSchema = z.object({
  token: z.string().min(8).max(200),
});

export type AcceptStaffInviteRequest = z.infer<typeof acceptStaffInviteRequestSchema>;

export const acceptStaffInviteResponseSchema = z.object({
  platformUserId: z.string().uuid(),
  auth0Sub: z.string(),
  tenantId: z.string().uuid(),
  permissionRole: portalPermissionRoleSchema,
  staffMemberId: z.string().uuid().nullable().optional(),
});

export type AcceptStaffInviteResponse = z.infer<typeof acceptStaffInviteResponseSchema>;

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

export const subscriptionSkuSchema = z.enum(["starter", "professional", "business"]);

export const tenantBillingStatusSchema = z.enum([
  "trialing",
  "active",
  "past_due",
  "suspended",
  "canceled",
]);

export type TenantBillingStatus = z.infer<typeof tenantBillingStatusSchema>;

export const subscriptionUsageSchema = z.object({
  tier: subscriptionTierSchema,
  entitlements: tenantEntitlementsSchema,
  bookingsUsedThisMonth: z.number().int().nonnegative(),
  bookingsLimitThisMonth: z.number().int().positive().nullable(),
  subscriptionSku: subscriptionSkuSchema.optional(),
  billingStatus: tenantBillingStatusSchema.optional(),
  trialEndsAt: z.string().nullable().optional(),
  paidThrough: z.string().nullable().optional(),
});

export type SubscriptionUsage = z.infer<typeof subscriptionUsageSchema>;

export const subscriptionCheckoutRequestSchema = z.object({
  tenantId: z.string().uuid(),
  targetTier: subscriptionTierSchema,
  customerEmail: z.string().email(),
  successUrl: z.string().url(),
  cancelUrl: z.string().url(),
});

export type SubscriptionCheckoutRequest = z.infer<typeof subscriptionCheckoutRequestSchema>;

export const subscriptionCheckoutResponseSchema = z.object({
  checkoutUrl: z.string(),
  providerReference: z.string(),
  status: z.string(),
});

export type SubscriptionCheckoutResponse = z.infer<typeof subscriptionCheckoutResponseSchema>;

export const tenantSubscriptionChargeSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  providerReference: z.string(),
  eventType: z.string(),
  subscriptionSku: subscriptionSkuSchema.nullable().optional(),
  amountMinor: z.number().int().nullable().optional(),
  currency: z.string().nullable().optional(),
  status: z.string(),
  occurredAt: z.string(),
});

export type TenantSubscriptionCharge = z.infer<typeof tenantSubscriptionChargeSchema>;

export const tenantBillingHistoryResponseSchema = z.object({
  items: z.array(tenantSubscriptionChargeSchema),
});

export type TenantBillingHistoryResponse = z.infer<typeof tenantBillingHistoryResponseSchema>;

export const adminSubscriptionMetricsCurrencyTotalSchema = z.object({
  currency: z.string().length(3),
  amountMinor: z.number().int().nonnegative(),
});

export const adminSubscriptionSkuCountSchema = z.object({
  sku: subscriptionSkuSchema,
  count: z.number().int().nonnegative(),
});

export const adminSubscriptionMetricsSchema = z.object({
  paidTenantCount: z.number().int().nonnegative(),
  pastDueTenantCount: z.number().int().nonnegative(),
  trialingTenantCount: z.number().int().nonnegative(),
  skuMix: z.array(adminSubscriptionSkuCountSchema),
  syncedMrrByCurrency: z.array(adminSubscriptionMetricsCurrencyTotalSchema),
  estimatedMrrByCurrency: z.array(adminSubscriptionMetricsCurrencyTotalSchema),
  mrrSource: z.enum(["synced_charges", "estimated_list_price"]),
  asOf: z.string(),
});

export type AdminSubscriptionMetrics = z.infer<typeof adminSubscriptionMetricsSchema>;

export const completeStubSubscriptionCheckoutRequestSchema = z.object({
  tenantId: z.string().uuid(),
  providerReference: z.string().min(1),
});

export type CompleteStubSubscriptionCheckoutRequest = z.infer<
  typeof completeStubSubscriptionCheckoutRequestSchema
>;

export const adminBusinessSummarySchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  status: z.number(),
  subscriptionTier: subscriptionTierSchema,
  subscriptionSku: subscriptionSkuSchema.optional(),
  billingStatus: tenantBillingStatusSchema.optional(),
  trialEndsAt: z.string().nullable().optional(),
  createdAt: z.string(),
});

export type AdminBusinessSummary = z.infer<typeof adminBusinessSummarySchema>;

export const adminBusinessesResponseSchema = adminPagedResultSchema(adminBusinessSummarySchema);

export const adminBusinessDirectoryStatsSchema = z.object({
  totalCount: z.number().int().nonnegative(),
  verifiedCount: z.number().int().nonnegative(),
  statusMix: z.array(
    z.object({
      status: z.number().int(),
      count: z.number().int().nonnegative(),
    }),
  ),
  tierMix: z.array(
    z.object({
      tier: subscriptionTierSchema,
      count: z.number().int().nonnegative(),
    }),
  ),
});

export type AdminBusinessDirectoryStats = z.infer<typeof adminBusinessDirectoryStatsSchema>;

export const adminMarketCatalogStatsSchema = z.object({
  totalCount: z.number().int().nonnegative(),
  liveCount: z.number().int().nonnegative(),
});

export type AdminMarketCatalogStats = z.infer<typeof adminMarketCatalogStatsSchema>;

export const setSubscriptionTierRequestSchema = z.object({
  tier: subscriptionTierSchema,
});

export type SetSubscriptionTierRequest = z.infer<typeof setSubscriptionTierRequestSchema>;

export const adminTenantCommercialTermsSchema = z.object({
  tenantId: z.string(),
  subscriptionSku: subscriptionSkuSchema,
  billingStatus: tenantBillingStatusSchema,
  trialSku: subscriptionSkuSchema.nullable().optional(),
  trialEndsAt: z.string().nullable().optional(),
  paidThrough: z.string().nullable().optional(),
  discountPercent: z.number().min(0).max(100).nullable().optional(),
  discountFixedAmount: z.number().nonnegative().nullable().optional(),
  discountCurrency: z.string().length(3).nullable().optional(),
  discountCyclesRemaining: z.number().int().nonnegative().nullable().optional(),
  billingCompReason: z.string().max(500).nullable().optional(),
  customSaasMonthlyAmount: z.number().positive().nullable().optional(),
  customSaasCurrency: z.string().length(3).nullable().optional(),
  customSaasSku: subscriptionSkuSchema.nullable().optional(),
});

export type AdminTenantCommercialTerms = z.infer<typeof adminTenantCommercialTermsSchema>;

export const setTenantCommercialTermsRequestSchema = z.object({
  billingStatus: tenantBillingStatusSchema.optional(),
  subscriptionSku: subscriptionSkuSchema.optional(),
  trialSku: subscriptionSkuSchema.optional(),
  trialDays: z.number().int().min(0).max(730).optional(),
  endTrial: z.boolean().optional(),
  trialEndsAt: z.string().nullable().optional(),
  paidThrough: z.string().nullable().optional(),
  discountPercent: z.number().min(0).max(100).nullable().optional(),
  discountFixedAmount: z.number().nonnegative().nullable().optional(),
  discountCurrency: z.string().length(3).nullable().optional(),
  discountCyclesRemaining: z.number().int().nonnegative().nullable().optional(),
  clearDiscounts: z.boolean().optional(),
  billingCompReason: z.string().max(500).nullable().optional(),
  customSaasMonthlyAmount: z.number().positive().nullable().optional(),
  customSaasCurrency: z.string().length(3).nullable().optional(),
  customSaasSku: subscriptionSkuSchema.optional(),
  clearCustomSaasPrice: z.boolean().optional(),
});

export type SetTenantCommercialTermsRequest = z.infer<
  typeof setTenantCommercialTermsRequestSchema
>;

export const walkInSettingsSchema = z.object({
  walkInsEnabled: z.boolean(),
  walkInAcceptingUntil: z.string().nullable().optional(),
  maxWalkInWaiting: z.number().int().positive().nullable().optional(),
  calendarPolicy: z.enum(["relaxed", "strict"]),
  acceptingNow: z.boolean(),
});

export type WalkInSettings = z.infer<typeof walkInSettingsSchema>;

export const updateWalkInSettingsRequestSchema = z.object({
  walkInsEnabled: z.boolean(),
  maxWalkInWaiting: z.number().int().positive().nullable().optional(),
  calendarPolicy: z.enum(["relaxed", "strict"]),
});

export type UpdateWalkInSettingsRequest = z.infer<
  typeof updateWalkInSettingsRequestSchema
>;

export const openWalkInBlockRequestSchema = z.object({
  durationMinutes: z.number().int().min(15).max(480),
});

export type OpenWalkInBlockRequest = z.infer<typeof openWalkInBlockRequestSchema>;

export const businessProfileSchema = z.object({
  tenantId: z.string(),
  businessName: z.string(),
  status: z.number(),
  categorySlug: z.string(),
  additionalCategorySlugs: z.array(z.string()).optional(),
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
  subscriptionSku: subscriptionSkuSchema.optional(),
  billingStatus: tenantBillingStatusSchema.optional(),
  trialEndsAt: z.string().nullable().optional(),
  paidThrough: z.string().nullable().optional(),
  discountPercent: z.number().min(0).max(100).nullable().optional(),
  discountFixedAmount: z.number().nonnegative().nullable().optional(),
  discountCurrency: z.string().length(3).nullable().optional(),
  discountCyclesRemaining: z.number().int().nonnegative().nullable().optional(),
  customSaasMonthlyAmount: z.number().positive().nullable().optional(),
  customSaasCurrency: z.string().length(3).nullable().optional(),
  customSaasSku: subscriptionSkuSchema.nullable().optional(),
  entitlements: tenantEntitlementsSchema.optional(),
  publicPage: publicPageConfigSchema.optional(),
  galleryImages: z
    .array(
      z.object({
        storageKey: z.string().min(1),
        url: z.string().url(),
      }),
    )
    .optional(),
  policies: businessPoliciesSchema.optional(),
  walkInSettings: walkInSettingsSchema.optional(),
});

export type BusinessProfile = z.infer<typeof businessProfileSchema>;

export const updateBusinessProfileRequestSchema = z.object({
  businessName: z.string().min(1),
  categorySlug: z.string().min(1),
  additionalCategorySlugs: z.array(z.string()).optional(),
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

export const verificationBadgeTypeSchema = z.enum(["phone", "cac", "address", "license"]);

export const pendingBadgeRequestSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  businessName: z.string(),
  badgeType: verificationBadgeTypeSchema,
  referenceNumber: z.string().nullable().optional(),
  requestedAt: z.string(),
});

export const pendingBadgesResponseSchema = adminPagedResultSchema(pendingBadgeRequestSchema);

export type PendingBadgeRequest = z.infer<typeof pendingBadgeRequestSchema>;

export const adminReviewListItemSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  businessName: z.string(),
  locationSlug: z.string().nullable().optional(),
  rating: z.number().int().min(1).max(5),
  comment: z.string(),
  createdAt: z.string(),
  isHidden: z.boolean(),
  customerDisplayName: z.string(),
});

export const adminReviewsListResultSchema = pagedResultSchema(adminReviewListItemSchema);

export type AdminReviewListItem = z.infer<typeof adminReviewListItemSchema>;
export type AdminReviewsListResult = z.infer<typeof adminReviewsListResultSchema>;

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

export const queueTicketResponseSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  status: z.string(),
  position: z.number().int(),
  etaMinutes: z.number().int().nullable().optional(),
  displayName: z.string(),
  phone: z.string(),
  email: z.string().email().nullable().optional(),
  serviceName: z.string().nullable().optional(),
  serviceOfferingId: z.string().uuid().nullable().optional(),
  staffDisplayName: z.string().nullable().optional(),
  staffMemberId: z.string().uuid().nullable().optional(),
  partySize: z.number().int(),
  notes: z.string().nullable().optional(),
  joinedAt: z.string(),
  publicToken: z.string(),
  bookingId: z.string().uuid().nullable().optional(),
  businessName: z.string().nullable().optional(),
  source: z.enum(["walk_in", "appointment"]).optional(),
  tenantCustomerId: z.string().uuid().nullable().optional(),
});

export type QueueTicketResponse = z.infer<typeof queueTicketResponseSchema>;

export const tenantCustomerListItemSchema = z.object({
  id: z.string().uuid(),
  displayName: z.string(),
  phoneMasked: z.string(),
  email: z.string().email().nullable().optional(),
  lastSeenAt: z.string(),
  platformCustomerId: z.string().uuid().nullable().optional(),
});

export type TenantCustomerListItem = z.infer<typeof tenantCustomerListItemSchema>;

export const tenantCustomerListResponseSchema = pagedResultSchema(tenantCustomerListItemSchema);

export type TenantCustomerListResponse = z.infer<typeof tenantCustomerListResponseSchema>;

export const tenantCustomerDetailSchema = z.object({
  id: z.string().uuid(),
  displayName: z.string(),
  phoneMasked: z.string(),
  email: z.string().email().nullable().optional(),
  lastSeenAt: z.string(),
  lastBookingAt: z.string().nullable().optional(),
  lastQueueJoinAt: z.string().nullable().optional(),
  platformCustomerId: z.string().uuid().nullable().optional(),
  recentBookingIds: z.array(z.string().uuid()),
  recentQueueTicketIds: z.array(z.string().uuid()),
});

export type TenantCustomerDetail = z.infer<typeof tenantCustomerDetailSchema>;

export const queueExpectedArrivalSchema = z.object({
  bookingId: z.string().uuid(),
  startAt: z.string(),
  displayName: z.string(),
  phone: z.string().nullable().optional(),
  serviceName: z.string().nullable().optional(),
  staffDisplayName: z.string().nullable().optional(),
  staffMemberId: z.string().uuid().nullable().optional(),
});

export type QueueExpectedArrival = z.infer<typeof queueExpectedArrivalSchema>;

export const walkInStatusSchema = z.object({
  acceptingNow: z.boolean(),
  walkInsEnabled: z.boolean(),
  acceptingUntil: z.string().nullable().optional(),
  maxWaiting: z.number().int().positive().nullable().optional(),
  calendarPolicy: z.enum(["relaxed", "strict"]),
});

export type WalkInStatus = z.infer<typeof walkInStatusSchema>;

export const tenantQueueFloorResponseSchema = z.object({
  items: z.array(queueTicketResponseSchema),
  expectedArrivals: z.array(queueExpectedArrivalSchema),
  walkInStatus: walkInStatusSchema,
});

export type TenantQueueFloorResponse = z.infer<typeof tenantQueueFloorResponseSchema>;

export const queueTicketsResponseSchema = tenantQueueFloorResponseSchema;

export const joinQueueRequestSchema = z.object({
  displayName: z.string().min(1).max(200),
  phone: z.string().min(7).max(40),
  email: z.string().email().max(320).optional(),
  serviceOfferingId: z.string().uuid().nullable().optional(),
  partySize: z.number().int().min(1).max(6).optional(),
  notes: z.string().max(500).optional(),
});

export type JoinQueueRequest = z.infer<typeof joinQueueRequestSchema>;

export const staffAddQueueRequestSchema = joinQueueRequestSchema.extend({
  staffMemberId: z.string().uuid().nullable().optional(),
});

export type StaffAddQueueRequest = z.infer<typeof staffAddQueueRequestSchema>;

export const kioskStatusResponseSchema = z.object({
  hasPin: z.boolean(),
  slug: z.string().nullable().optional(),
  queueEnabled: z.boolean(),
});

export type KioskStatusResponse = z.infer<typeof kioskStatusResponseSchema>;

export const setKioskPinRequestSchema = z.object({
  pin: z.string().regex(/^\d{4,8}$/).nullable(),
});

export type SetKioskPinRequest = z.infer<typeof setKioskPinRequestSchema>;

export const verifyKioskPinRequestSchema = z.object({
  pin: z.string().min(1).max(8),
});

export type VerifyKioskPinRequest = z.infer<typeof verifyKioskPinRequestSchema>;

export const verifyKioskPinResponseSchema = z.object({
  ok: z.boolean(),
});

export type VerifyKioskPinResponse = z.infer<typeof verifyKioskPinResponseSchema>;

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

export const paymentLedgerResponseSchema = pagedResultSchema(paymentLedgerEntrySchema);

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

export const messageThreadsResponseSchema = pagedResultSchema(messageThreadSummarySchema);

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

export const mediaUploadPurposeSchema = z.enum([
  "cover",
  "Cover",
  "gallery",
  "Gallery",
  "staff_avatar",
  "staffAvatar",
  "quote_photo",
]);

export const updateStaffAvatarRequestSchema = z.object({
  avatarImageKey: z.string().min(1),
});

export type UpdateStaffAvatarRequest = z.infer<typeof updateStaffAvatarRequestSchema>;

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

export const galleryImageSchema = z.object({
  storageKey: z.string().min(1),
  url: z.string().url(),
});

export const galleryImagesResponseSchema = z.object({
  items: z.array(galleryImageSchema),
});

export const addGalleryImageRequestSchema = z.object({
  galleryImageKey: z.string().min(1),
});

export const removeGalleryImageRequestSchema = z.object({
  galleryImageKey: z.string().min(1),
});

export type MediaUploadUrlRequest = z.infer<typeof mediaUploadUrlRequestSchema>;
export type MediaUploadUrlResponse = z.infer<typeof mediaUploadUrlResponseSchema>;
export type UpdateCoverImageRequest = z.infer<typeof updateCoverImageRequestSchema>;
export type GalleryImage = z.infer<typeof galleryImageSchema>;
export type AddGalleryImageRequest = z.infer<typeof addGalleryImageRequestSchema>;
export type RemoveGalleryImageRequest = z.infer<typeof removeGalleryImageRequestSchema>;

export const weeklyAvailabilityRuleSchema = z.object({
  dayOfWeek: z.number().int().min(0).max(6),
  openTime: z.string(),
  closeTime: z.string(),
});

export type WeeklyAvailabilityRule = z.infer<typeof weeklyAvailabilityRuleSchema>;

export const weeklyAvailabilityResponseSchema = z.object({
  items: z.array(weeklyAvailabilityRuleSchema),
});

export const businessClosedDateSchema = z.object({
  id: z.string().uuid(),
  startDate: z.string(),
  endDate: z.string(),
  reason: z.string().nullable().optional(),
  createdAt: z.string(),
  conflictingBookingIds: z.array(z.string().uuid()).nullable().optional(),
});

export type BusinessClosedDate = z.infer<typeof businessClosedDateSchema>;

export const businessClosedDatesResponseSchema = z.object({
  items: z.array(businessClosedDateSchema),
});

export const createBusinessClosedDateRequestSchema = z.object({
  startDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/),
  endDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/),
  reason: z.string().max(200).nullable().optional(),
});

export type CreateBusinessClosedDateRequest = z.infer<
  typeof createBusinessClosedDateRequestSchema
>;

export const staffCalendarBookingItemSchema = z.object({
  id: z.string().uuid(),
  serviceName: z.string(),
  startAt: z.string(),
  endAt: z.string(),
  status: z.number().int(),
  customerNotes: z.string().nullable().optional(),
  /** Customer asked for this person (vs “any available”). */
  customerSelectedStaff: z.boolean().optional().default(true),
  staffMemberId: z.string().uuid().nullable().optional(),
  staffDisplayName: z.string().nullable().optional(),
});

export const staffCalendarLeaveItemSchema = z.object({
  id: z.string().uuid(),
  startAt: z.string(),
  endAt: z.string(),
  reason: z.string().nullable().optional(),
  staffMemberId: z.string().uuid().nullable().optional(),
  staffDisplayName: z.string().nullable().optional(),
});

export const staffCalendarResponseSchema = z.object({
  staffMemberId: z.string().uuid(),
  displayName: z.string(),
  hours: z.array(weeklyAvailabilityRuleSchema),
  inheritsBusinessHours: z.boolean(),
  bookings: z.array(staffCalendarBookingItemSchema),
  leave: z.array(staffCalendarLeaveItemSchema),
});

export const staffTeamCalendarStaffSummarySchema = z.object({
  staffMemberId: z.string().uuid(),
  displayName: z.string(),
  bookingCount: z.number().int(),
  confirmedCount: z.number().int(),
  bookedMinutes: z.number().int(),
  hours: z.array(weeklyAvailabilityRuleSchema),
  inheritsBusinessHours: z.boolean(),
});

export const staffTeamCalendarResponseSchema = z.object({
  staff: z.array(staffTeamCalendarStaffSummarySchema),
  bookings: z.array(staffCalendarBookingItemSchema),
  leave: z.array(staffCalendarLeaveItemSchema),
});

export type StaffCalendarBookingItem = z.infer<typeof staffCalendarBookingItemSchema>;
export type StaffCalendarLeaveItem = z.infer<typeof staffCalendarLeaveItemSchema>;
export type StaffCalendarResponse = z.infer<typeof staffCalendarResponseSchema>;
export type StaffTeamCalendarStaffSummary = z.infer<typeof staffTeamCalendarStaffSummarySchema>;
export type StaffTeamCalendarResponse = z.infer<typeof staffTeamCalendarResponseSchema>;

export const createServiceOfferingRequestSchema = z.object({
  name: z.string().min(1),
  description: z.string().nullable().optional(),
  priceAmount: z.number().nonnegative(),
  currency: z.string().min(3).max(3),
  durationMinutes: z.number().int().positive(),
  pricingType: z.enum(["fixed", "quote_request", "hourly"]).optional(),
  categorySlug: z.string().optional(),
  catalogServiceId: z.string().optional(),
  bookingDeliveryType: bookingDeliveryTypeSchema.optional(),
  menuGroupId: z.string().uuid().nullable().optional(),
  sortOrder: z.number().int().optional(),
  isAddOn: z.boolean().optional(),
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
  categorySlug: z.string().optional(),
  catalogServiceId: z.string().optional(),
  bookingDeliveryType: bookingDeliveryTypeSchema.optional(),
  menuGroupId: z.string().uuid().nullable().optional(),
  sortOrder: z.number().int().optional(),
  isAddOn: z.boolean().optional(),
});

export type UpdateServiceOfferingRequest = z.infer<
  typeof updateServiceOfferingRequestSchema
>;

export const BOOKING_STATUS_LABELS: Record<number, string> = {
  0: "Pending",
  1: "Confirmed",
  2: "Rejected",
  3: "Cancelled",
  4: "Checked in",
  5: "Completed",
  6: "Paid",
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