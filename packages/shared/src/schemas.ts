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

export const createBookingRequestSchema = z.object({
  tenantId: z.string().uuid(),
  serviceOfferingId: z.string().uuid(),
  startAt: z.string(),
  customerNotes: z.string().max(1000).optional(),
});

export type CreateBookingRequest = z.infer<typeof createBookingRequestSchema>;